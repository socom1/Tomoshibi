using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The shell view model. Owns the chrome (nav sidebar, header, zen mode,
/// settings flyout) and routes the main content area to the active
/// destination's view model. Each destination is its own view model + view
/// pair and lives independently of this one.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly AppState _state;
    private readonly DispatcherTimer _dayWatcher;
    private readonly ReminderService _reminders = new(new NotificationService());

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExitZenCommand))]
    private bool _isZenMode;

    [ObservableProperty]
    private bool _isNavOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsTodayActive))]
    [NotifyPropertyChangedFor(nameof(IsTimetableActive))]
    [NotifyPropertyChangedFor(nameof(IsTodoActive))]
    [NotifyPropertyChangedFor(nameof(IsSubjectsActive))]
    [NotifyPropertyChangedFor(nameof(IsStatsActive))]
    [NotifyPropertyChangedFor(nameof(IsReviewActive))]
    [NotifyPropertyChangedFor(nameof(IsShopActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    [NotifyPropertyChangedFor(nameof(ActiveContent))]
    private Destination _activeDestination;

    /// <summary>Window title: the app name when idle, the countdown while the
    /// timer runs so the dock/taskbar shows progress at a glance.</summary>
    [ObservableProperty]
    private string _windowTitle = "灯火 · tomoshibi";

    // ---- Welcome modal (launch greeting) ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseWelcomeCommand))]
    private bool _isWelcomeOpen;

    /// <summary>"greet me on launch" — persisted preference.</summary>
    [ObservableProperty]
    private bool _showWelcomeOnLaunch;

    /// <summary>The Cmd/Ctrl+K command palette overlay.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommandPaletteCommand))]
    private bool _isCommandPaletteOpen;

    /// <summary>The launch splash overlay; flipped off shortly after open.</summary>
    [ObservableProperty]
    private bool _isBooting = true;

    public CommandPaletteViewModel CommandPalette { get; }

    public string WelcomeDateLabel =>
        $"{DateTime.Now:dddd, MMMM d}".ToLowerInvariant();

    /// <summary>The dashboard landing page.</summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>The "today" destination's content. Owns the pomodoro settings
    /// flyout since the settings only matter for the timer.</summary>
    public TodayViewModel Today { get; }

    /// <summary>The timetable destination's content.</summary>
    public TimetableViewModel Timetable { get; }

    /// <summary>The todo backlog destination's content.</summary>
    public TodoViewModel Todo { get; }

    /// <summary>The subjects + grades destination's content.</summary>
    public SubjectsViewModel Subjects { get; }

    /// <summary>The streak-calendar stats destination's content.</summary>
    public StatsViewModel Stats { get; }

    /// <summary>The flashcards / spaced-repetition review destination.</summary>
    public ReviewViewModel Review { get; }

    /// <summary>The floating music player (bubble + panel, all pages).</summary>
    public MusicPlayerViewModel Music { get; }

    /// <summary>The settings destination's content.</summary>
    public SettingsPageViewModel SettingsPage { get; }

    /// <summary>The shared embers wallet.</summary>
    public WalletViewModel Wallet { get; }

    /// <summary>The themes shop.</summary>
    public ShopViewModel Shop { get; }

    /// <summary>Read live by the window's close handler.</summary>
    public bool CloseToTray => _state.CloseToTray;

    /// <summary>Read by App at startup to apply the saved theme.</summary>
    public string ActiveThemeId => _state.ActiveThemeId;

    /// <summary>The view model the main content area is currently bound to.
    /// Resolved through <see cref="ViewLocator"/> to the right view.</summary>
    public ViewModelBase ActiveContent => ActiveDestination switch
    {
        Destination.Timetable => Timetable,
        Destination.Todo => Todo,
        Destination.Subjects => Subjects,
        Destination.Stats => Stats,
        Destination.Review => Review,
        Destination.Shop => Shop,
        Destination.Settings => SettingsPage,
        Destination.Today => Today,
        _ => Dashboard
    };

    public bool IsDashboardActive => ActiveDestination == Destination.Dashboard;
    public bool IsTodayActive => ActiveDestination == Destination.Today;
    public bool IsTimetableActive => ActiveDestination == Destination.Timetable;
    public bool IsTodoActive => ActiveDestination == Destination.Todo;
    public bool IsSubjectsActive => ActiveDestination == Destination.Subjects;
    public bool IsStatsActive => ActiveDestination == Destination.Stats;
    public bool IsReviewActive => ActiveDestination == Destination.Review;
    public bool IsShopActive => ActiveDestination == Destination.Shop;
    public bool IsSettingsActive => ActiveDestination == Destination.Settings;

    public MainWindowViewModel(IStorageService storage)
    {
        _storage = storage;
        _state = storage.Load();

        MigrateDeadlinesToTickets();
        MigrateTheme();
        ApplyDailyReset(DateOnly.FromDateTime(DateTime.Now));

        _isNavOpen = _state.IsNavOpen;
        _activeDestination = _state.ActiveDestination;

        var settings = new SettingsViewModel(_state.Settings, OnSettingsChanged);
        Wallet = new WalletViewModel(_state, Save);
        Shop = new ShopViewModel(_state, Save, Wallet);
        Today = new TodayViewModel(_state, Save, () => IsZenMode = !IsZenMode, settings, Wallet,
                                   new SoundService(), new NotificationService());
        Timetable = new TimetableViewModel(_state, Save);
        Todo = new TodoViewModel(_state, Save, SendTodoToToday);
        Subjects = new SubjectsViewModel(_state, Save, OpenUrl);
        Stats = new StatsViewModel(_state);
        Review = new ReviewViewModel(_state, Save, Wallet);
        Music = new MusicPlayerViewModel(_state, Save, new MusicService());
        SettingsPage = new SettingsPageViewModel(_state, Save, settings, Music, Subjects,
                                                 storage.Location);
        Dashboard = new DashboardViewModel(_state, Save, Today, Todo, Subjects, Review,
            openSubject: s => { Subjects.OpenDetail(s); ActiveDestination = Destination.Subjects; },
            goToday: () => ActiveDestination = Destination.Today,
            goReview: () => { ActiveDestination = Destination.Review; Review.ReviewAllCommand.Execute(null); },
            openUrl: OpenUrl);

        CommandPalette = new CommandPaletteViewModel(() => IsCommandPaletteOpen = false);

        _showWelcomeOnLaunch = _state.ShowWelcome;
        _isWelcomeOpen = _state.ShowWelcome;

        Today.Pomodoro.PropertyChanged += OnPomodoroPropertyChanged;

        // Catches the date rollover when the app is left running through midnight.
        _dayWatcher = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _dayWatcher.Tick += OnDayWatcherTick;
        _dayWatcher.Start();

        CheckReminders();
        Save();
    }

    /// <summary>Fire any due deadline/exam reminders and persist the fired-keys
    /// set if it changed. Cheap to call often — it just reads the dedup set.</summary>
    private void CheckReminders()
    {
        if (_reminders.Check(_state))
            Save();
    }

    private void OnPomodoroPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PomodoroViewModel.TimeDisplay) or nameof(PomodoroViewModel.IsRunning))
        {
            WindowTitle = Today.Pomodoro.IsRunning
                ? $"{Today.Pomodoro.TimeDisplay} · {Today.Pomodoro.PhaseShortLabel} — tomoshibi"
                : "灯火 · tomoshibi";
        }
    }

    /// <summary>Parameterless ctor for the XAML designer preview only.</summary>
    public MainWindowViewModel() : this(new JsonStorageService())
    {
    }

    /// <summary>One-time migration: standalone deadlines became todo tickets
    /// with due dates. Old state files may still carry a deadlines list —
    /// convert each to a ticket (skipping ones that already exist) and empty
    /// the legacy list.</summary>
    /// <summary>Map the old light-theme bool onto the named-theme system, and
    /// make sure the two free themes are always owned.</summary>
    private void MigrateTheme()
    {
        if (string.IsNullOrEmpty(_state.ActiveThemeId))
            _state.ActiveThemeId = _state.LightTheme ? "light" : "dark";

        foreach (var free in new[] { "dark", "light" })
            if (!_state.OwnedThemeIds.Contains(free))
                _state.OwnedThemeIds.Add(free);
    }

    private void MigrateDeadlinesToTickets()
    {
        if (_state.Deadlines.Count == 0)
            return;

        foreach (var d in _state.Deadlines)
        {
            var exists = _state.Todos.Any(t => t.Due == d.Date && t.Title == d.Title);
            if (!exists)
            {
                _state.Todos.Add(new TodoItem
                {
                    Number = _state.NextTodoNumber++,
                    Title = d.Title,
                    Course = d.Course,
                    Due = d.Date
                });
            }
        }

        _state.Deadlines.Clear();
    }

    private void OnSettingsChanged()
    {
        Today.Pomodoro.ApplySettings();
        Save();
    }

    [RelayCommand(CanExecute = nameof(CanCloseWelcome))]
    private void CloseWelcome() => IsWelcomeOpen = false;

    private bool CanCloseWelcome() => IsWelcomeOpen;

    /// <summary>"start focusing" on the welcome card: close it and run the
    /// timer if it isn't already going.</summary>
    [RelayCommand]
    private void WelcomeStartFocus()
    {
        IsWelcomeOpen = false;
        if (!Today.Pomodoro.IsRunning)
            Today.Pomodoro.ToggleRunCommand.Execute(null);
    }

    partial void OnShowWelcomeOnLaunchChanged(bool value)
    {
        _state.ShowWelcome = value;
        Save();
    }

    [RelayCommand]
    private void ToggleZen() => IsZenMode = !IsZenMode;

    [RelayCommand(CanExecute = nameof(CanExitZen))]
    private void ExitZen() => IsZenMode = false;

    private bool CanExitZen() => IsZenMode;

    [RelayCommand]
    private void ToggleNav() => IsNavOpen = !IsNavOpen;

    /// <summary>The destinations in nav order, for Cmd/Ctrl+1…8 jumps.</summary>
    private static readonly Destination[] NavOrder =
    {
        Destination.Dashboard, Destination.Today, Destination.Timetable,
        Destination.Todo, Destination.Subjects, Destination.Stats,
        Destination.Review, Destination.Shop, Destination.Settings
    };

    /// <summary>Jump to the nth destination (1-based) — wired to Cmd/Ctrl+digit.
    /// Ignored in zen mode, which has no navigation.</summary>
    public void NavigateByIndex(int oneBased)
    {
        if (IsZenMode || oneBased < 1 || oneBased > NavOrder.Length)
            return;
        ActiveDestination = NavOrder[oneBased - 1];
    }

    /// <summary>Open the command palette with a fresh candidate list.</summary>
    public void OpenCommandPalette()
    {
        if (IsZenMode)
            return;
        CommandPalette.Load(BuildCommands());
        IsCommandPaletteOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanCloseCommandPalette))]
    private void CloseCommandPalette() => IsCommandPaletteOpen = false;

    private bool CanCloseCommandPalette() => IsCommandPaletteOpen;

    /// <summary>Pages, quick actions, every subject, and the user's own
    /// content — todo tickets, flashcard decks and journal reflections — all
    /// the candidates the palette searches over.</summary>
    private List<PaletteItemViewModel> BuildCommands()
    {
        var items = new List<PaletteItemViewModel>();

        void Page(string title, Destination dest) =>
            items.Add(new PaletteItemViewModel
            {
                Title = title, Kind = "page", Run = () => ActiveDestination = dest
            });

        Page("dashboard", Destination.Dashboard);
        Page("today", Destination.Today);
        Page("timetable", Destination.Timetable);
        Page("todo", Destination.Todo);
        Page("subjects", Destination.Subjects);
        Page("stats", Destination.Stats);
        Page("review", Destination.Review);
        Page("shop", Destination.Shop);
        Page("settings", Destination.Settings);

        void Action(string title, Action run) =>
            items.Add(new PaletteItemViewModel { Title = title, Kind = "action", Run = run });

        Action("start / pause timer", () => Today.Pomodoro.ToggleRunCommand.Execute(null));
        Action("zen mode", () => IsZenMode = true);
        Action("new task", () =>
        {
            ActiveDestination = Destination.Today;
            Today.Tasks.OpenAddTaskCommand.Execute(null);
        });
        Action("new todo", () =>
        {
            ActiveDestination = Destination.Todo;
            Todo.OpenAddCommand.Execute(null);
        });
        Action("new subject", () =>
        {
            ActiveDestination = Destination.Subjects;
            Subjects.OpenAddCommand.Execute(null);
        });
        Action("review due cards", () =>
        {
            ActiveDestination = Destination.Review;
            Review.ReviewAllCommand.Execute(null);
        });

        foreach (var subject in Subjects.Items)
        {
            var captured = subject;
            items.Add(new PaletteItemViewModel
            {
                Title = subject.Name,
                Kind = "subject",
                Run = () => { Subjects.OpenDetail(captured); ActiveDestination = Destination.Subjects; }
            });
        }

        // ---- Content: jump straight to a ticket, deck or reflection ----

        foreach (var todo in _state.Todos)
        {
            var captured = todo;
            var title = string.IsNullOrWhiteSpace(todo.Title) ? "(untitled)" : todo.Title;
            items.Add(new PaletteItemViewModel
            {
                Title = $"#{todo.Number} · {title}",
                Kind = "todo",
                // Navigate first (the page rebuilds on arrival), then reveal so
                // the search + expansion survive.
                Run = () => { ActiveDestination = Destination.Todo; Todo.Reveal(captured); }
            });
        }

        foreach (var deck in Review.Decks)
        {
            if (string.IsNullOrWhiteSpace(deck.Name))
                continue;
            var captured = deck;
            items.Add(new PaletteItemViewModel
            {
                Title = deck.Name,
                Kind = "deck",
                Run = () => { ActiveDestination = Destination.Review; Review.OpenDeckCommand.Execute(captured); }
            });
        }

        foreach (var note in JournalCandidates())
        {
            var date = note.Date;
            var snippet = string.IsNullOrWhiteSpace(note.Reflection) ? note.Intention : note.Reflection;
            items.Add(new PaletteItemViewModel
            {
                Title = $"{JournalDateLabel(date)} · {Snippet(snippet)}",
                Kind = "journal",
                Run = () => { ActiveDestination = Destination.Stats; Stats.RevealJournal(date); }
            });
        }

        return items;
    }

    /// <summary>The look-back days the palette can jump to: today's live note
    /// (when it has anything written) plus the banked journal, newest first.</summary>
    private IEnumerable<DayNote> JournalCandidates()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (!string.IsNullOrWhiteSpace(_state.DailyIntention) ||
            !string.IsNullOrWhiteSpace(_state.DailyReflection))
        {
            yield return new DayNote
            {
                Date = today,
                Intention = _state.DailyIntention,
                Reflection = _state.DailyReflection
            };
        }

        foreach (var note in _state.Journal
                     .Where(n => n.Date != today &&
                                 (!string.IsNullOrWhiteSpace(n.Intention) ||
                                  !string.IsNullOrWhiteSpace(n.Reflection)))
                     .OrderByDescending(n => n.Date))
        {
            yield return note;
        }
    }

    private static string JournalDateLabel(DateOnly date) =>
        date == DateOnly.FromDateTime(DateTime.Now)
            ? "today"
            : $"{date:ddd MMM d}".ToLowerInvariant();

    /// <summary>One-line preview of a journal note for the palette row.</summary>
    private static string Snippet(string text)
    {
        text = text.Trim();
        return text.Length <= 48 ? text : text[..47].TrimEnd() + "…";
    }

    [RelayCommand]
    private void NavigateToDashboard() => ActiveDestination = Destination.Dashboard;

    [RelayCommand]
    private void NavigateToToday() => ActiveDestination = Destination.Today;

    [RelayCommand]
    private void NavigateToTimetable() => ActiveDestination = Destination.Timetable;

    [RelayCommand]
    private void NavigateToTodo() => ActiveDestination = Destination.Todo;

    [RelayCommand]
    private void NavigateToSubjects() => ActiveDestination = Destination.Subjects;

    [RelayCommand]
    private void NavigateToStats() => ActiveDestination = Destination.Stats;

    [RelayCommand]
    private void NavigateToReview() => ActiveDestination = Destination.Review;

    [RelayCommand]
    private void NavigateToShop() => ActiveDestination = Destination.Shop;

    [RelayCommand]
    private void NavigateToSettings() => ActiveDestination = Destination.Settings;

    /// <summary>Append a backlog item to today's task template as a new block
    /// and jump to the today page so the user sees it land.</summary>
    private void SendTodoToToday(TodoItem todo)
    {
        var block = $"// {todo.Title}";
        if (!string.IsNullOrWhiteSpace(todo.Course))
            block += $"\ncourse: {todo.Course}";

        var existing = (Today.Tasks.Source ?? string.Empty).TrimEnd();
        Today.Tasks.Source = string.IsNullOrEmpty(existing) ? block : $"{existing}\n\n{block}";

        ActiveDestination = Destination.Today;
    }

    /// <summary>
    /// Open a study link in the default browser — but only ever an absolute
    /// http/https URL. UseShellExecute will otherwise happily launch
    /// file://, UNC paths, custom protocols (ms-msdt:, vbscript:, …) or even
    /// a local executable; since links are persisted in state and may one day
    /// arrive via import/sync, anything non-web is refused outright.
    /// </summary>
    private static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // No browser, or the OS refused — nothing useful to do.
        }
    }

    partial void OnIsNavOpenChanged(bool value)
    {
        _state.IsNavOpen = value;
        Save();
    }

    partial void OnActiveDestinationChanged(Destination value)
    {
        _state.ActiveDestination = value;

        // Landing on a page re-reads anything other pages may have changed —
        // schedule edits show on today, timer sessions show on tickets.
        switch (value)
        {
            case Destination.Dashboard:
                Dashboard.Refresh();
                break;
            case Destination.Today:
                Today.RefreshScheduleInfo();
                break;
            case Destination.Todo:
                Todo.Refresh();
                break;
            case Destination.Timetable:
                Timetable.RefreshDeadlines();
                break;
            case Destination.Subjects:
                Subjects.Refresh();
                break;
            case Destination.Stats:
                Stats.Refresh();
                break;
            case Destination.Review:
                Review.Refresh();
                break;
        }

        Save();
    }

    private void OnDayWatcherTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        Today.Tick(now);
        CheckReminders();

        var today = DateOnly.FromDateTime(now);
        if (!ApplyDailyReset(today))
            return;

        Today.RefreshFromState();
        Save();
    }

    private bool ApplyDailyReset(DateOnly today)
    {
        var changed = false;

        if (_state.Today.Date != today)
        {
            // Bank the finished day before replacing it — the streak and the
            // future calendar need the history. Empty days aren't recorded.
            if (_state.Today.CompletedSessions > 0 || _state.Today.FocusedMinutes > 0)
            {
                _state.History.RemoveAll(d => d.Date == _state.Today.Date);
                _state.History.Add(_state.Today);

                // A year of history is plenty; keep the file small.
                if (_state.History.Count > 400)
                    _state.History.RemoveRange(0, _state.History.Count - 400);
            }

            _state.Today = new DailyStats { Date = today };
            changed = true;
        }

        if (_state.IntentionDate != today)
        {
            // Bank the finished day's intention + reflection into the journal
            // before clearing them, so the stats page keeps the look-back.
            if (!string.IsNullOrWhiteSpace(_state.DailyIntention) ||
                !string.IsNullOrWhiteSpace(_state.DailyReflection))
            {
                _state.Journal.RemoveAll(n => n.Date == _state.IntentionDate);
                _state.Journal.Add(new DayNote
                {
                    Date = _state.IntentionDate,
                    Intention = _state.DailyIntention,
                    IntentionKept = _state.IntentionKept,
                    Reflection = _state.DailyReflection
                });

                // A year of look-back is plenty; keep the file small.
                if (_state.Journal.Count > 400)
                    _state.Journal.RemoveRange(0, _state.Journal.Count - 400);
            }

            _state.DailyIntention = string.Empty;
            _state.IntentionKept = false;
            _state.DailyReflection = string.Empty;
            _state.IntentionDate = today;
            changed = true;
        }

        return changed;
    }

    /// <summary>Saved window placement, applied by the view at startup.
    /// Width 0 means nothing has been saved yet.</summary>
    public (double Width, double Height, int? X, int? Y) WindowPlacement =>
        (_state.WindowWidth, _state.WindowHeight, _state.WindowX, _state.WindowY);

    /// <summary>Called by the view when the window closes so the next launch
    /// opens where this one left off.</summary>
    public void SaveWindowPlacement(double width, double height, int x, int y)
    {
        _state.WindowWidth = width;
        _state.WindowHeight = height;
        _state.WindowX = x;
        _state.WindowY = y;
        Save();
    }

    // Saves are debounced: keystrokes in the intention and the .code editor
    // each call Save(), and writing the whole state on every one
    // is needless disk churn. Instead we coalesce — restart a short timer and
    // write once it goes quiet — and flush immediately on shutdown/close so
    // nothing is lost.
    private DispatcherTimer? _saveTimer;

    private void Save()
    {
        _saveTimer ??= CreateSaveTimer();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private DispatcherTimer CreateSaveTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _storage.Save(_state);
        };
        return timer;
    }

    /// <summary>Write any pending change right now — called on window close
    /// and app exit so a debounced edit never dies with the process.</summary>
    public void FlushSave()
    {
        if (_saveTimer?.IsEnabled == true)
            _saveTimer.Stop();
        _storage.Save(_state);
    }
}

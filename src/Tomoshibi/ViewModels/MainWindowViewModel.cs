using System;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExitZenCommand))]
    private bool _isZenMode;

    [ObservableProperty]
    private bool _isNavOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTodayActive))]
    [NotifyPropertyChangedFor(nameof(IsTimetableActive))]
    [NotifyPropertyChangedFor(nameof(IsTodoActive))]
    [NotifyPropertyChangedFor(nameof(ActiveContent))]
    private Destination _activeDestination;

    /// <summary>Window title: the app name when idle, the countdown while the
    /// timer runs so the dock/taskbar shows progress at a glance.</summary>
    [ObservableProperty]
    private string _windowTitle = "灯火 · tomoshibi";

    /// <summary>The "today" destination's content. Owns the pomodoro settings
    /// flyout since the settings only matter for the timer.</summary>
    public TodayViewModel Today { get; }

    /// <summary>The timetable destination's content.</summary>
    public TimetableViewModel Timetable { get; }

    /// <summary>The todo backlog destination's content.</summary>
    public TodoViewModel Todo { get; }

    /// <summary>The view model the main content area is currently bound to.
    /// Resolved through <see cref="ViewLocator"/> to the right view.</summary>
    public ViewModelBase ActiveContent => ActiveDestination switch
    {
        Destination.Timetable => Timetable,
        Destination.Todo => Todo,
        _ => Today
    };

    public bool IsTodayActive => ActiveDestination == Destination.Today;
    public bool IsTimetableActive => ActiveDestination == Destination.Timetable;
    public bool IsTodoActive => ActiveDestination == Destination.Todo;

    public MainWindowViewModel(IStorageService storage)
    {
        _storage = storage;
        _state = storage.Load();

        ApplyDailyReset(DateOnly.FromDateTime(DateTime.Now));

        _isNavOpen = _state.IsNavOpen;
        _activeDestination = _state.ActiveDestination;

        var settings = new SettingsViewModel(_state.Settings, OnSettingsChanged);
        Today = new TodayViewModel(_state, Save, () => IsZenMode = !IsZenMode, settings,
                                   new SoundService(), new NotificationService());
        Timetable = new TimetableViewModel(_state, Save);
        Todo = new TodoViewModel(_state, Save, SendTodoToToday);

        Today.Pomodoro.PropertyChanged += OnPomodoroPropertyChanged;

        // Catches the date rollover when the app is left running through midnight.
        _dayWatcher = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _dayWatcher.Tick += OnDayWatcherTick;
        _dayWatcher.Start();

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

    private void OnSettingsChanged()
    {
        Today.Pomodoro.ApplySettings();
        Save();
    }

    [RelayCommand]
    private void ToggleZen() => IsZenMode = !IsZenMode;

    [RelayCommand(CanExecute = nameof(CanExitZen))]
    private void ExitZen() => IsZenMode = false;

    private bool CanExitZen() => IsZenMode;

    [RelayCommand]
    private void ToggleNav() => IsNavOpen = !IsNavOpen;

    [RelayCommand]
    private void NavigateToToday() => ActiveDestination = Destination.Today;

    [RelayCommand]
    private void NavigateToTimetable() => ActiveDestination = Destination.Timetable;

    [RelayCommand]
    private void NavigateToTodo() => ActiveDestination = Destination.Todo;

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
            case Destination.Today:
                Today.RefreshScheduleInfo();
                break;
            case Destination.Todo:
                Todo.Refresh();
                break;
        }

        Save();
    }

    private void OnDayWatcherTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        Today.Tick(now);

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
            _state.DailyIntention = string.Empty;
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

    private void Save() => _storage.Save(_state);
}

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

    /// <summary>The view model the main content area is currently bound to.
    /// Resolved through <see cref="ViewLocator"/> to the right view.</summary>
    public ViewModelBase ActiveContent => ActiveDestination switch
    {
        Destination.Timetable => Timetable,
        _ => Today
    };

    public bool IsTodayActive => ActiveDestination == Destination.Today;
    public bool IsTimetableActive => ActiveDestination == Destination.Timetable;

    public MainWindowViewModel(IStorageService storage)
    {
        _storage = storage;
        _state = storage.Load();

        ApplyDailyReset(DateOnly.FromDateTime(DateTime.Now));

        _isNavOpen = _state.IsNavOpen;
        _activeDestination = _state.ActiveDestination;

        var settings = new SettingsViewModel(_state.Settings, OnSettingsChanged);
        Today = new TodayViewModel(_state, Save, () => IsZenMode = !IsZenMode, settings, new SoundService());
        Timetable = new TimetableViewModel(_state, Save);

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

    partial void OnIsNavOpenChanged(bool value)
    {
        _state.IsNavOpen = value;
        Save();
    }

    partial void OnActiveDestinationChanged(Destination value)
    {
        _state.ActiveDestination = value;
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

    private void Save() => _storage.Save(_state);
}

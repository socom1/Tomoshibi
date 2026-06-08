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
    private string _greeting;

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

    /// <summary>The "today" destination's content.</summary>
    public TodayViewModel Today { get; }

    /// <summary>The timetable destination's content.</summary>
    public TimetableViewModel Timetable { get; }

    /// <summary>Editable timer lengths, surfaced in the settings flyout.</summary>
    public SettingsViewModel Settings { get; }

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

        _greeting = GreetingFor(DateTime.Now);
        _isNavOpen = _state.IsNavOpen;
        _activeDestination = _state.ActiveDestination;

        Today = new TodayViewModel(_state, Save);
        Timetable = new TimetableViewModel(_state, Save);
        Settings = new SettingsViewModel(_state.Settings, OnSettingsChanged);

        // Catches the date rollover when the app is left running through midnight.
        _dayWatcher = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _dayWatcher.Tick += OnDayWatcherTick;
        _dayWatcher.Start();

        Save();
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
        Greeting = GreetingFor(now);

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

    private static string GreetingFor(DateTime now) => now.Hour switch
    {
        >= 5 and < 12 => "good morning",
        >= 12 and < 18 => "good afternoon",
        >= 18 and < 23 => "good evening",
        _ => "burning the midnight oil"
    };
}

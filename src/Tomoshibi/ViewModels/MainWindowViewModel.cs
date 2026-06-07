using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly AppState _state;
    private readonly DispatcherTimer _dayWatcher;

    [ObservableProperty]
    private string _dailyIntention;

    [ObservableProperty]
    private int _completedSessions;

    [ObservableProperty]
    private double _focusedHours;

    [ObservableProperty]
    private string _greeting;

    /// <summary>The Pomodoro timer shown in the focus card.</summary>
    public PomodoroViewModel Pomodoro { get; }

    /// <summary>The course-tagged task list.</summary>
    public TasksViewModel Tasks { get; }

    /// <summary>Editable timer lengths, surfaced in the settings flyout.</summary>
    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(IStorageService storage)
    {
        _storage = storage;
        _state = storage.Load();

        ApplyDailyReset(DateOnly.FromDateTime(DateTime.Now));

        _dailyIntention = _state.DailyIntention;
        _completedSessions = _state.Today.CompletedSessions;
        _focusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
        _greeting = GreetingFor(DateTime.Now);

        Pomodoro = new PomodoroViewModel(_state.Settings);
        Pomodoro.FocusSessionCompleted += OnFocusSessionCompleted;

        Tasks = new TasksViewModel(_state, Save);
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

    partial void OnDailyIntentionChanged(string value)
    {
        _state.DailyIntention = value;
        _state.IntentionDate = DateOnly.FromDateTime(DateTime.Now);
        Save();
    }

    private void OnFocusSessionCompleted(int focusMinutes)
    {
        _state.Today.CompletedSessions++;
        _state.Today.FocusedMinutes += focusMinutes;

        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);

        Save();
    }

    private void OnSettingsChanged()
    {
        Pomodoro.ApplySettings();
        Save();
    }

    private void OnDayWatcherTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        Greeting = GreetingFor(now);

        var today = DateOnly.FromDateTime(now);
        if (!ApplyDailyReset(today))
            return;

        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
        DailyIntention = _state.DailyIntention;
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

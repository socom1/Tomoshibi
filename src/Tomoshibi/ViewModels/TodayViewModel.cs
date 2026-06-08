using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The "today" destination — the daily intention, the Pomodoro timer,
/// today's focus stats and the course-tagged task list. Owns everything the
/// main window used to own directly, so the main view model only has to
/// route between destinations.
/// </summary>
public partial class TodayViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    [ObservableProperty]
    private string _dailyIntention;

    [ObservableProperty]
    private int _completedSessions;

    [ObservableProperty]
    private double _focusedHours;

    /// <summary>The Pomodoro timer.</summary>
    public PomodoroViewModel Pomodoro { get; }

    /// <summary>The course-tagged task list.</summary>
    public TasksViewModel Tasks { get; }

    public TodayViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;

        _dailyIntention = _state.DailyIntention;
        _completedSessions = _state.Today.CompletedSessions;
        _focusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);

        Pomodoro = new PomodoroViewModel(_state.Settings);
        Pomodoro.FocusSessionCompleted += OnFocusSessionCompleted;

        Tasks = new TasksViewModel(_state, _save);
    }

    /// <summary>Pull the latest stats and intention back out of state after
    /// the day-watcher's midnight reset.</summary>
    public void RefreshFromState()
    {
        DailyIntention = _state.DailyIntention;
        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
    }

    partial void OnDailyIntentionChanged(string value)
    {
        _state.DailyIntention = value;
        _state.IntentionDate = DateOnly.FromDateTime(DateTime.Now);
        _save();
    }

    private void OnFocusSessionCompleted(int focusMinutes)
    {
        _state.Today.CompletedSessions++;
        _state.Today.FocusedMinutes += focusMinutes;

        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);

        _save();
    }
}

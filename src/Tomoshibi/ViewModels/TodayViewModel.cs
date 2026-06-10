using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The "today" destination — the daily intention, the Pomodoro timer,
/// today's focus stats and the course-tagged task list, plus the page's
/// own time-of-day greeting and zen toggle. Owns everything the main
/// window used to own directly, so the main view model only has to
/// route between destinations.
/// </summary>
public partial class TodayViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;
    private readonly Action _toggleZen;

    [ObservableProperty]
    private string _dailyIntention;

    [ObservableProperty]
    private int _completedSessions;

    [ObservableProperty]
    private double _focusedHours;

    /// <summary>Time-of-day greeting shown at the top of the page.</summary>
    [ObservableProperty]
    private string _greeting;

    /// <summary>"now · maths" under the timer when a task is active, empty
    /// otherwise — confirms which task is driving the phase lengths.</summary>
    [ObservableProperty]
    private string _activeTaskLabel = string.Empty;

    [ObservableProperty]
    private bool _hasActiveTask;

    /// <summary>The Pomodoro timer.</summary>
    public PomodoroViewModel Pomodoro { get; }

    /// <summary>The code-edited task list for today.</summary>
    public TaskTemplateViewModel Tasks { get; }

    /// <summary>Editable timer lengths — surfaced from this page's header
    /// gear since the settings only ever affect the pomodoro.</summary>
    public SettingsViewModel Settings { get; }

    public TodayViewModel(AppState state, Action save, Action toggleZen,
                          SettingsViewModel settings, ISoundService? sound = null)
    {
        _state = state;
        _save = save;
        _toggleZen = toggleZen;
        Settings = settings;

        _dailyIntention = _state.DailyIntention;
        _completedSessions = _state.Today.CompletedSessions;
        _focusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
        _greeting = GreetingFor(DateTime.Now);

        Pomodoro = new PomodoroViewModel(ComposeEffectiveSettings, sound);
        Pomodoro.FocusSessionCompleted += OnFocusSessionCompleted;

        Tasks = new TaskTemplateViewModel(_state, _save, OnActiveTaskChanged);
    }

    /// <summary>The timer reads its phase lengths through this every time it
    /// switches phases — so the active task's <c>study</c>/<c>short</c>/
    /// <c>long</c> override the global settings without mutating them.</summary>
    private PomodoroSettings ComposeEffectiveSettings()
    {
        var t = Tasks?.ActiveTask;
        var s = _state.Settings;

        return new PomodoroSettings
        {
            FocusMinutes = t?.Study ?? s.FocusMinutes,
            ShortBreakMinutes = t?.Short ?? s.ShortBreakMinutes,
            LongBreakMinutes = t?.Long ?? s.LongBreakMinutes,
            RoundsBeforeLongBreak = s.RoundsBeforeLongBreak,
            AutoContinue = s.AutoContinue
        };
    }

    /// <summary>When the user picks a different task, the timer needs to
    /// reload the new effective settings — same path the settings flyout uses.</summary>
    private void OnActiveTaskChanged()
    {
        Pomodoro.ApplySettings();

        var active = Tasks?.ActiveTask;
        HasActiveTask = active is not null;
        ActiveTaskLabel = active is null ? string.Empty : $"now · {active.Title}";
    }

    /// <summary>Called by the shell's day-watcher every minute so the greeting
    /// keeps up with the time of day.</summary>
    public void Tick(DateTime now) => Greeting = GreetingFor(now);

    /// <summary>Pull the latest stats and intention back out of state after
    /// the day-watcher's midnight reset.</summary>
    public void RefreshFromState()
    {
        DailyIntention = _state.DailyIntention;
        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
    }

    /// <summary>Passes the click through to the shell so this view model
    /// doesn't have to know what zen mode actually is.</summary>
    [RelayCommand]
    private void ToggleZen() => _toggleZen();

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

    private static string GreetingFor(DateTime now) => now.Hour switch
    {
        >= 5 and < 12 => "good morning",
        >= 12 and < 18 => "good afternoon",
        >= 18 and < 23 => "good evening",
        _ => "burning the midnight oil"
    };
}

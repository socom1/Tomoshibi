using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Consecutive days (ending today or yesterday) with at least
    /// one completed session. Today not counting yet doesn't break it.</summary>
    [ObservableProperty]
    private int _streakDays;

    /// <summary>"next · algebra at 14:00" (or "now · algebra until 15:00")
    /// from today's class slots. Empty when nothing's left today.</summary>
    [ObservableProperty]
    private string _nextClassLabel = string.Empty;

    [ObservableProperty]
    private bool _hasNextClass;

    /// <summary>The soonest deadlines within a week, e.g.
    /// "essay due tomorrow · quiz due fri". Empty when nothing's close.</summary>
    [ObservableProperty]
    private string _deadlinesLabel = string.Empty;

    [ObservableProperty]
    private bool _hasUpcomingDeadlines;

    /// <summary>The Pomodoro timer.</summary>
    public PomodoroViewModel Pomodoro { get; }

    /// <summary>The code-edited task list for today.</summary>
    public TaskTemplateViewModel Tasks { get; }

    /// <summary>Editable timer lengths — surfaced from this page's header
    /// gear since the settings only ever affect the pomodoro.</summary>
    public SettingsViewModel Settings { get; }

    private readonly WalletViewModel _wallet;

    public TodayViewModel(AppState state, Action save, Action toggleZen,
                          SettingsViewModel settings, WalletViewModel wallet,
                          ISoundService? sound = null, INotificationService? notify = null)
    {
        _state = state;
        _save = save;
        _toggleZen = toggleZen;
        Settings = settings;
        _wallet = wallet;

        _dailyIntention = _state.DailyIntention;
        _completedSessions = _state.Today.CompletedSessions;
        _focusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
        _greeting = GreetingFor(DateTime.Now);

        Pomodoro = new PomodoroViewModel(ComposeEffectiveSettings, sound, notify);
        Pomodoro.FocusSessionCompleted += OnFocusSessionCompleted;

        Tasks = new TaskTemplateViewModel(_state, _save, OnActiveTaskChanged);

        RecomputeStreak();
        RefreshScheduleInfo();
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
            AutoContinue = s.AutoContinue,
            ChimeEnabled = s.ChimeEnabled,
            NotificationsEnabled = s.NotificationsEnabled
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
    /// and the schedule line keep up with the time of day.</summary>
    public void Tick(DateTime now)
    {
        Greeting = GreetingFor(now);
        RefreshScheduleInfo();
    }

    /// <summary>Rebuild the "up next" class line and the upcoming-deadlines
    /// line from the timetable. Called on launch, every minute, and when the
    /// user navigates back from editing the timetable.</summary>
    public void RefreshScheduleInfo()
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);
        var weekday = (WeekDay)(((int)now.DayOfWeek + 6) % 7);

        // A class running right now beats one coming later.
        var current = _state.ClassSlots
            .Where(s => s.Day == weekday && s.Start <= time && time < s.End)
            .OrderBy(s => s.End)
            .FirstOrDefault();

        if (current is not null)
        {
            NextClassLabel = $"now · {current.Title} until {current.End:HH\\:mm}";
        }
        else
        {
            var next = _state.ClassSlots
                .Where(s => s.Day == weekday && s.Start > time)
                .OrderBy(s => s.Start)
                .FirstOrDefault();

            NextClassLabel = next is null
                ? string.Empty
                : $"next · {next.Title} at {next.Start:HH\\:mm}";
        }
        HasNextClass = NextClassLabel.Length > 0;

        // Due things come from two places: open tickets with due dates, and
        // dated, still-ungraded assessments (exams, essay hand-ins).
        var dueTickets = _state.Todos
            .Where(t => t.Status != TodoStatus.Done && t.Due is { } due &&
                        due >= today && due <= today.AddDays(7))
            .Select(t => (Date: t.Due!.Value, Label: t.Title));

        var dueExams = _state.Subjects
            .SelectMany(s => s.Assessments
                .Where(a => a.Grade is null && a.Date is { } d &&
                            d >= today && d <= today.AddDays(7))
                .Select(a => (Date: a.Date!.Value,
                              Label: $"{a.Title} ({s.Code ?? s.Name})")));

        var soon = dueTickets.Concat(dueExams)
            .OrderBy(x => x.Date)
            .Take(2)
            .Select(x => $"{x.Label} due {DueWord(x.Date, today)}")
            .ToList();

        DeadlinesLabel = string.Join(" · ", soon);
        HasUpcomingDeadlines = soon.Count > 0;
    }

    private static string DueWord(DateOnly due, DateOnly today)
    {
        if (due == today) return "today";
        if (due == today.AddDays(1)) return "tomorrow";
        if (due <= today.AddDays(6)) return $"{due:ddd}".ToLowerInvariant();
        return $"{due:MMM d}".ToLowerInvariant();
    }

    /// <summary>Pull the latest stats and intention back out of state after
    /// the day-watcher's midnight reset.</summary>
    public void RefreshFromState()
    {
        DailyIntention = _state.DailyIntention;
        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
        RecomputeStreak();
    }

    /// <summary>Rebuild the streak count and the last-14-days dots from
    /// history + today. A day counts when it had at least one session.</summary>
    private void RecomputeStreak()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var focusedDays = new HashSet<DateOnly>(
            _state.History.Where(d => d.CompletedSessions > 0).Select(d => d.Date));
        if (_state.Today.CompletedSessions > 0)
            focusedDays.Add(_state.Today.Date);

        // Streak counts back from today; an inactive today doesn't break it
        // (the day isn't over), but an inactive yesterday does.
        var cursor = focusedDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (focusedDays.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        StreakDays = streak;
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

        // If the active task came from (or shares a title with) a backlog
        // ticket, credit the session against its estimate; and if it carries
        // a course, log the minutes against that course for the day.
        if (Tasks.ActiveTask is { } active)
        {
            var ticket = _state.Todos.FirstOrDefault(t =>
                t.Status != TodoStatus.Done &&
                string.Equals(t.Title, active.Title, StringComparison.OrdinalIgnoreCase));
            if (ticket is not null)
                ticket.SessionsSpent++;

            if (!string.IsNullOrWhiteSpace(active.Course))
            {
                var key = active.Course.Trim();
                _state.Today.FocusByCourse[key] =
                    _state.Today.FocusByCourse.GetValueOrDefault(key) + focusMinutes;
            }
        }

        // Earn embers for the focus put in — one per minute.
        _wallet.Add(focusMinutes);

        CompletedSessions = _state.Today.CompletedSessions;
        FocusedHours = Math.Round(_state.Today.FocusedMinutes / 60.0, 1);
        RecomputeStreak();

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

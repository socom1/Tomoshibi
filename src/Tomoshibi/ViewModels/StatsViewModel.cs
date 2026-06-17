using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The 記録 · stats destination: a month calendar tinted by how many focus
/// sessions landed on each day (the banked <see cref="AppState.History"/>
/// plus today), and the all-time numbers — current streak, best streak,
/// total hours, active days.
/// </summary>
public partial class StatsViewModel : ViewModelBase
{
    private readonly AppState _state;

    /// <summary>First day of the month the calendar is showing.</summary>
    private DateOnly _month;

    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();

    /// <summary>Focus time by course over the last 7 days, busiest first.</summary>
    public ObservableCollection<CourseFocusViewModel> FocusByCourse { get; } = new();

    [ObservableProperty] private bool _hasFocusByCourse;

    /// <summary>Intention + reflection look-back, today first then newest.</summary>
    public ObservableCollection<JournalEntryViewModel> Journal { get; } = new();

    [ObservableProperty] private bool _hasJournal;

    [ObservableProperty] private string _monthLabel = string.Empty;

    /// <summary>Caption under the calendar — active days + hours this month.</summary>
    [ObservableProperty] private string _monthSummary = string.Empty;

    /// <summary>14-day focus trend drawn from block glyphs, one column per day.</summary>
    [ObservableProperty] private string _sparkline = string.Empty;

    /// <summary>Right-hand caption for the sparkline, e.g. "14d · 12.5h".</summary>
    [ObservableProperty] private string _sparklineSummary = string.Empty;

    [ObservableProperty] private int _currentStreak;
    [ObservableProperty] private int _bestStreak;
    [ObservableProperty] private double _totalHours;
    [ObservableProperty] private int _activeDays;
    [ObservableProperty] private int _totalSessions;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand))]
    private bool _canGoForward;

    public StatsViewModel(AppState state)
    {
        _state = state;
        var today = DateOnly.FromDateTime(DateTime.Now);
        _month = new DateOnly(today.Year, today.Month, 1);
        Refresh();
    }

    /// <summary>Re-read history and rebuild everything — called on
    /// construction and whenever the user navigates here.</summary>
    public void Refresh()
    {
        var byDate = DaysWithActivity();

        BuildSummary(byDate);
        BuildSparkline(byDate);
        BuildMonth(byDate);
        BuildFocusByCourse();
        BuildJournal();
    }

    /// <summary>Raised when the palette asks to surface a specific journal
    /// row, so the view can scroll it into sight.</summary>
    public event Action<JournalEntryViewModel>? JournalRevealRequested;

    /// <summary>Surface the look-back entry for <paramref name="date"/>: clear
    /// any prior highlight, flag the matching row, and ask the view to scroll
    /// it into view. The page is rebuilt on navigation, so the row exists by
    /// the time this runs. No-op if that day isn't in the look-back window.</summary>
    public void RevealJournal(DateOnly date)
    {
        JournalEntryViewModel? target = null;
        foreach (var entry in Journal)
        {
            entry.Highlighted = entry.Date == date;
            if (entry.Highlighted)
                target = entry;
        }

        if (target is not null)
            JournalRevealRequested?.Invoke(target);
    }

    private static readonly char[] SparkLevels = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

    /// <summary>A 14-day focus sparkline: one block glyph per day, its height
    /// scaled against the busiest day in the window; '·' marks an idle day.</summary>
    private void BuildSparkline(Dictionary<DateOnly, (int Sessions, double Minutes)> byDate)
    {
        const int span = 14;
        var today = DateOnly.FromDateTime(DateTime.Now);

        var minutes = new double[span];
        for (var i = 0; i < span; i++)
        {
            var date = today.AddDays(-(span - 1 - i));
            byDate.TryGetValue(date, out var activity);
            minutes[i] = activity.Minutes;
        }

        var max = minutes.Max();
        var columns = new char[span];
        for (var i = 0; i < span; i++)
        {
            if (minutes[i] <= 0 || max <= 0)
            {
                columns[i] = '·';
                continue;
            }
            var level = (int)Math.Ceiling(minutes[i] / max * SparkLevels.Length) - 1;
            columns[i] = SparkLevels[Math.Clamp(level, 0, SparkLevels.Length - 1)];
        }

        Sparkline = new string(columns);
        SparklineSummary = $"{span}d · {Math.Round(minutes.Sum() / 60.0, 1)}h";
    }

    /// <summary>The intention/reflection look-back: today's live note first
    /// (when it has anything), then the banked journal, newest first.</summary>
    private void BuildJournal()
    {
        Journal.Clear();
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (!string.IsNullOrWhiteSpace(_state.DailyIntention) ||
            !string.IsNullOrWhiteSpace(_state.DailyReflection))
        {
            Journal.Add(new JournalEntryViewModel(DailyNoteSnapshot(today, _state), isToday: true));
        }

        foreach (var note in _state.Journal
                     .Where(n => n.Date != today)
                     .OrderByDescending(n => n.Date)
                     .Take(30))
        {
            Journal.Add(new JournalEntryViewModel(note, isToday: false));
        }

        HasJournal = Journal.Count > 0;
    }

    /// <summary>Today's not-yet-banked intention + reflection, shaped as a
    /// <see cref="DayNote"/> so the journal row treats it like any other.</summary>
    private static DayNote DailyNoteSnapshot(DateOnly today, AppState state) => new()
    {
        Date = today,
        Intention = state.DailyIntention,
        IntentionKept = state.IntentionKept,
        Reflection = state.DailyReflection
    };

    private void BuildFocusByCourse()
    {
        FocusByCourse.Clear();

        var minutes = FocusLog.MinutesByCourse(_state, 7)
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        const int barWidth = 12;
        var max = minutes.Count > 0 ? minutes[0].Value : 0;
        foreach (var (course, mins) in minutes)
        {
            var fraction = max > 0 ? mins / max : 0;
            var filled = Math.Clamp((int)Math.Round(fraction * barWidth), 0, barWidth);
            FocusByCourse.Add(new CourseFocusViewModel
            {
                Course = course,
                HoursLabel = FocusLog.HoursLabel(mins),
                Fraction = fraction,
                Bar = new string('█', filled) + new string('░', barWidth - filled)
            });
        }

        HasFocusByCourse = FocusByCourse.Count > 0;
    }

    [RelayCommand]
    private void PrevMonth()
    {
        _month = _month.AddMonths(-1);
        BuildMonth(DaysWithActivity());
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void NextMonth()
    {
        _month = _month.AddMonths(1);
        BuildMonth(DaysWithActivity());
    }

    /// <summary>date → (sessions, minutes), history plus today.</summary>
    private Dictionary<DateOnly, (int Sessions, double Minutes)> DaysWithActivity()
    {
        var byDate = _state.History
            .Where(d => d.CompletedSessions > 0)
            .ToDictionary(d => d.Date, d => (d.CompletedSessions, d.FocusedMinutes));

        if (_state.Today.CompletedSessions > 0)
            byDate[_state.Today.Date] = (_state.Today.CompletedSessions, _state.Today.FocusedMinutes);

        return byDate;
    }

    private void BuildSummary(Dictionary<DateOnly, (int Sessions, double Minutes)> byDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        TotalSessions = byDate.Values.Sum(v => v.Sessions);
        TotalHours = Math.Round(byDate.Values.Sum(v => v.Minutes) / 60.0, 1);
        ActiveDays = byDate.Count;

        // Current streak: back from today (an idle today doesn't break it).
        var cursor = byDate.ContainsKey(today) ? today : today.AddDays(-1);
        var current = 0;
        while (byDate.ContainsKey(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }
        CurrentStreak = current;

        // Best streak: longest consecutive run anywhere in the history.
        var best = 0;
        var run = 0;
        DateOnly? prev = null;
        foreach (var date in byDate.Keys.OrderBy(d => d))
        {
            run = prev is { } p && date == p.AddDays(1) ? run + 1 : 1;
            best = Math.Max(best, run);
            prev = date;
        }
        BestStreak = best;
    }

    private void BuildMonth(Dictionary<DateOnly, (int Sessions, double Minutes)> byDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        MonthLabel = $"{_month:MMMM yyyy}".ToLowerInvariant();
        CanGoForward = _month < new DateOnly(today.Year, today.Month, 1);

        var inMonth = byDate
            .Where(kv => kv.Key.Year == _month.Year && kv.Key.Month == _month.Month)
            .ToList();
        MonthSummary = inMonth.Count == 0
            ? "no focus logged this month"
            : $"{inMonth.Count} active days · {Math.Round(inMonth.Sum(kv => kv.Value.Minutes) / 60.0, 1)}h this month";

        Days.Clear();

        // Pad so day 1 lands on its weekday column (Monday-first).
        var lead = ((int)_month.DayOfWeek + 6) % 7;
        for (var i = 0; i < lead; i++)
            Days.Add(new CalendarDayViewModel { IsPlaceholder = true });

        var daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(_month.Year, _month.Month, day);
            byDate.TryGetValue(date, out var activity);

            var hours = Math.Round(activity.Minutes / 60.0, 1);
            Days.Add(new CalendarDayViewModel
            {
                Label = day.ToString(),
                IsToday = date == today,
                IsFuture = date > today,
                IsTier1 = activity.Sessions == 1,
                IsTier2 = activity.Sessions == 2,
                IsTier3 = activity.Sessions >= 3,
                Tooltip = activity.Sessions > 0
                    ? $"{date:MMM d} · {activity.Sessions} sessions · {hours}h".ToLowerInvariant()
                    : $"{date:MMM d} · no focus".ToLowerInvariant()
            });
        }
    }
}

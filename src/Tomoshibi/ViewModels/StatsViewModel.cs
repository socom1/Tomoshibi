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

    [ObservableProperty] private string _monthLabel = string.Empty;

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
        BuildMonth(byDate);
        BuildFocusByCourse();
    }

    private void BuildFocusByCourse()
    {
        FocusByCourse.Clear();

        var minutes = FocusLog.MinutesByCourse(_state, 7)
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        var max = minutes.Count > 0 ? minutes[0].Value : 0;
        foreach (var (course, mins) in minutes)
        {
            FocusByCourse.Add(new CourseFocusViewModel
            {
                Course = course,
                HoursLabel = FocusLog.HoursLabel(mins),
                Fraction = max > 0 ? mins / max : 0
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

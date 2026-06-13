using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Aggregates the per-course focus minutes that <see cref="DailyStats"/>
/// records, over a window of days (today + the banked history). One place
/// so the stats page, the dashboard and the subject detail all read the
/// same numbers.
/// </summary>
public static class FocusLog
{
    /// <summary>course code → minutes focused, over the last <paramref name="days"/>
    /// days (inclusive of today). days &lt;= 0 means all-time.</summary>
    public static Dictionary<string, double> MinutesByCourse(AppState state, int days)
    {
        var cutoff = days > 0
            ? DateOnly.FromDateTime(DateTime.Now).AddDays(-(days - 1))
            : DateOnly.MinValue;

        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        void fold(DailyStats day)
        {
            if (day.Date < cutoff)
                return;
            foreach (var (course, minutes) in day.FocusByCourse)
                totals[course] = totals.GetValueOrDefault(course) + minutes;
        }

        foreach (var day in state.History)
            fold(day);
        fold(state.Today);

        return totals;
    }

    /// <summary>Minutes focused against one course over the window.</summary>
    public static double MinutesForCourse(AppState state, string? course, int days)
    {
        if (string.IsNullOrWhiteSpace(course))
            return 0;
        return MinutesByCourse(state, days).GetValueOrDefault(course.Trim());
    }

    public static string HoursLabel(double minutes) =>
        minutes <= 0 ? "0h" : $"{minutes / 60.0:0.#}h";
}

using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Writes the weekly retrospective — a few plain sentences over the last
/// seven days' focus, courses, flashcard reviews and journal, weighed
/// against the seven days before. Pure state-in/lines-out so the wording
/// rules stay testable; a week with nothing in it comes back as no lines
/// and the card stays hidden.
/// </summary>
public static class WeeklyRetrospective
{
    public static List<string> Lines(AppState state, DateOnly today)
    {
        var lines = new List<string>();

        var start = today.AddDays(-6);
        var week = DaysIn(state, start, today);
        var prior = DaysIn(state, today.AddDays(-13), start.AddDays(-1));

        var minutes = week.Sum(d => d.FocusedMinutes);
        var sessions = week.Sum(d => d.CompletedSessions);
        var activeDays = week.Count(d => d.CompletedSessions > 0 || d.FocusedMinutes > 0);
        var reviewed = week.Sum(d => d.ReviewedCards);
        var (set, kept) = Intentions(state, start, today);

        // ---- focus, with the momentum against the week before ----
        if (minutes > 0)
        {
            var line = $"{FocusLog.HoursLabel(minutes)} of focus across " +
                       $"{Plural(sessions, "session")} on {Plural(activeDays, "day")}";

            var priorMinutes = prior.Sum(d => d.FocusedMinutes);
            if (priorMinutes > 0)
            {
                var delta = Math.Round((minutes - priorMinutes) / 60.0, 1);
                line += delta switch
                {
                    > 0 => $" — up {delta:0.#}h on the week before",
                    < 0 => $" — down {-delta:0.#}h on the week before",
                    _ => " — level with the week before"
                };
            }
            lines.Add(line);
        }
        else if (reviewed > 0 || set > 0)
        {
            lines.Add("no focus logged this week");
        }

        // ---- where it went ----
        var byCourse = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var day in week)
            foreach (var (course, mins) in day.FocusByCourse)
                byCourse[course] = byCourse.GetValueOrDefault(course) + mins;

        var top = byCourse
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => $"{kv.Key} ({FocusLog.HoursLabel(kv.Value)})")
            .ToList();

        if (top.Count > 0)
        {
            lines.Add(top.Count switch
            {
                1 => $"most of it went to {top[0]}",
                2 => $"most of it went to {top[0]}, then {top[1]}",
                _ => $"most of it went to {top[0]}, then {top[1]} and {top[2]}"
            });
        }

        // ---- the standout day (redundant when only one day happened) ----
        if (activeDays >= 2)
        {
            var busiest = week.OrderByDescending(d => d.FocusedMinutes).First();
            lines.Add($"busiest day — {busiest.Date.DayOfWeek.ToString().ToLowerInvariant()}, " +
                      FocusLog.HoursLabel(busiest.FocusedMinutes));
        }

        // ---- recall ----
        if (reviewed > 0)
            lines.Add($"{Plural(reviewed, "flashcard")} cleared in review");

        // ---- the journal's word on it ----
        if (set > 0)
            lines.Add($"{Plural(set, "intention")} set · {kept} kept");

        return lines;
    }

    /// <summary>The banked days inside the window, plus the live today if it
    /// falls there and hasn't banked yet.</summary>
    private static List<DailyStats> DaysIn(AppState state, DateOnly from, DateOnly to)
    {
        var days = state.History.Where(d => d.Date >= from && d.Date <= to).ToList();

        if (state.Today.Date >= from && state.Today.Date <= to &&
            days.All(d => d.Date != state.Today.Date))
            days.Add(state.Today);

        return days;
    }

    /// <summary>Intentions set / kept over the window — the banked journal
    /// plus today's live one, which the rollover hasn't banked yet.</summary>
    private static (int Set, int Kept) Intentions(AppState state, DateOnly from, DateOnly to)
    {
        var noted = state.Journal
            .Where(n => n.Date >= from && n.Date <= to &&
                        !string.IsNullOrWhiteSpace(n.Intention))
            .ToList();

        var set = noted.Count;
        var kept = noted.Count(n => n.IntentionKept);

        if (state.IntentionDate >= from && state.IntentionDate <= to &&
            !string.IsNullOrWhiteSpace(state.DailyIntention) &&
            state.Journal.All(n => n.Date != state.IntentionDate))
        {
            set++;
            if (state.IntentionKept)
                kept++;
        }

        return (set, kept);
    }

    private static string Plural(int n, string word) =>
        n == 1 ? $"1 {word}" : $"{n} {word}s";
}

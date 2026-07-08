using System;
using System.Collections.Generic;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>A day's review activity: how many reviews and how many passed.</summary>
public readonly record struct DayReviewStat(int Count, int Passed)
{
    public double PassRate => Count > 0 ? (double)Passed / Count : 0;
}

/// <summary>A pass-rate over some slice of reviews.</summary>
public readonly record struct RetentionStat(int Reviews, int Passed)
{
    public double Rate => Reviews > 0 ? (double)Passed / Reviews : 0;
    public string Percent => Reviews > 0 ? $"{Rate * 100:0.#}%" : "—";
}

/// <summary>True retention split into young (short interval) and mature cards.</summary>
public sealed record RetentionResult(RetentionStat Overall, RetentionStat Young, RetentionStat Mature);

/// <summary>
/// Pure aggregation of the review log for the stats page: per-day counts for
/// the heatmap and "true retention" (pass rate on cards that were already in
/// the review stage — learning/new steps don't count). A pass is any grade
/// above Again; young/mature split at a 21-day interval, matching Anki.
/// </summary>
public static class ReviewStatsBuilder
{
    private const int MatureIntervalDays = 21;

    public static Dictionary<DateOnly, DayReviewStat> ByDay(IEnumerable<ReviewLogEntry> log)
    {
        var map = new Dictionary<DateOnly, DayReviewStat>();
        foreach (var e in log)
        {
            var day = DateOnly.FromDateTime(e.Timestamp);
            var pass = e.Grade >= 2 ? 1 : 0;
            map[day] = map.TryGetValue(day, out var cur)
                ? new DayReviewStat(cur.Count + 1, cur.Passed + pass)
                : new DayReviewStat(1, pass);
        }
        return map;
    }

    /// <summary>Reviews logged on a given day (all states).</summary>
    public static int CountOn(IEnumerable<ReviewLogEntry> log, DateOnly day)
    {
        var n = 0;
        foreach (var e in log)
            if (DateOnly.FromDateTime(e.Timestamp) == day)
                n++;
        return n;
    }

    /// <summary>True retention over the trailing <paramref name="windowDays"/>
    /// (null = all time). Only review-stage answers count.</summary>
    public static RetentionResult Retention(IEnumerable<ReviewLogEntry> log, DateTime now, int? windowDays)
    {
        DateTime? cutoff = windowDays is int d ? now.AddDays(-d) : null;
        int oR = 0, oP = 0, yR = 0, yP = 0, mR = 0, mP = 0;

        foreach (var e in log)
        {
            if (e.StateBefore != CardState.Review) continue;
            if (cutoff is { } c && e.Timestamp < c) continue;

            var pass = e.Grade >= 2;
            oR++; if (pass) oP++;
            if (e.IntervalBefore < MatureIntervalDays) { yR++; if (pass) yP++; }
            else { mR++; if (pass) mP++; }
        }

        return new RetentionResult(new RetentionStat(oR, oP),
                                   new RetentionStat(yR, yP), new RetentionStat(mR, mP));
    }
}

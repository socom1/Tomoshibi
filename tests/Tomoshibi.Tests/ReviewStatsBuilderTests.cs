using System;
using System.Collections.Generic;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Stats read straight off the review log: day buckets for the heatmap
/// and true retention (review-stage pass rate, young/mature split).</summary>
public class ReviewStatsBuilderTests
{
    private static readonly DateTime Now = new(2026, 7, 6, 12, 0, 0);

    private static ReviewLogEntry Entry(DateTime ts, int grade, CardState before, double interval = 30) => new()
    {
        Timestamp = ts,
        CardId = Guid.NewGuid(),
        DeckId = Guid.NewGuid(),
        Grade = grade,
        StateBefore = before,
        IntervalBefore = interval
    };

    [Fact]
    public void By_day_buckets_counts_and_passes()
    {
        var log = new[]
        {
            Entry(Now, 3, CardState.Review),
            Entry(Now.AddHours(1), 1, CardState.Review),   // fail
            Entry(Now.AddDays(-1), 4, CardState.Review)
        };

        var byDay = ReviewStatsBuilder.ByDay(log);

        var today = byDay[new DateOnly(2026, 7, 6)];
        Assert.Equal(2, today.Count);
        Assert.Equal(1, today.Passed);
        Assert.Equal(1, byDay[new DateOnly(2026, 7, 5)].Count);
    }

    [Fact]
    public void Retention_counts_only_review_stage_answers()
    {
        var log = new[]
        {
            Entry(Now, 3, CardState.Review),   // counts, pass
            Entry(Now, 1, CardState.Review),   // counts, fail
            Entry(Now, 1, CardState.New),      // ignored (learning-stage)
            Entry(Now, 3, CardState.Learning)  // ignored
        };

        var r = ReviewStatsBuilder.Retention(log, Now, null);

        Assert.Equal(2, r.Overall.Reviews);
        Assert.Equal(1, r.Overall.Passed);
        Assert.Equal(0.5, r.Overall.Rate, 6);
    }

    [Fact]
    public void Young_and_mature_split_at_21_days()
    {
        var log = new[]
        {
            Entry(Now, 3, CardState.Review, interval: 5),    // young pass
            Entry(Now, 1, CardState.Review, interval: 10),   // young fail
            Entry(Now, 3, CardState.Review, interval: 40),   // mature pass
            Entry(Now, 3, CardState.Review, interval: 21)    // mature pass (boundary)
        };

        var r = ReviewStatsBuilder.Retention(log, Now, null);

        Assert.Equal(2, r.Young.Reviews);
        Assert.Equal(1, r.Young.Passed);
        Assert.Equal(2, r.Mature.Reviews);
        Assert.Equal(2, r.Mature.Passed);
    }

    [Fact]
    public void Window_excludes_older_entries()
    {
        var log = new[]
        {
            Entry(Now, 3, CardState.Review),
            Entry(Now.AddDays(-40), 3, CardState.Review) // outside a 30-day window
        };

        var month = ReviewStatsBuilder.Retention(log, Now, 30);
        var all = ReviewStatsBuilder.Retention(log, Now, null);

        Assert.Equal(1, month.Overall.Reviews);
        Assert.Equal(2, all.Overall.Reviews);
    }

    [Fact]
    public void Empty_stats_read_as_blank_not_crash()
    {
        var r = ReviewStatsBuilder.Retention(Array.Empty<ReviewLogEntry>(), Now, null);
        Assert.Equal(0, r.Overall.Reviews);
        Assert.Equal("—", r.Overall.Percent);
        Assert.Empty(ReviewStatsBuilder.ByDay(Array.Empty<ReviewLogEntry>()));
    }
}

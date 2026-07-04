using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Frecency's promises: what you just ran outranks what you ran a
/// while ago, frequency helps but fades on a one-week half-life, and the
/// usage table never grows past its cap.</summary>
public class PaletteFrecencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_run_is_counted_and_scores_above_never_used()
    {
        var usage = new Dictionary<string, PaletteUse>();
        PaletteFrecency.Record(usage, "page:stats", Now);

        Assert.True(PaletteFrecency.Score(usage, "page:stats", Now) > 0);
        Assert.Equal(0, PaletteFrecency.Score(usage, "page:review", Now));
    }

    [Fact]
    public void Repeat_runs_stack()
    {
        var usage = new Dictionary<string, PaletteUse>();
        PaletteFrecency.Record(usage, "action:zen mode", Now);
        PaletteFrecency.Record(usage, "action:zen mode", Now);
        PaletteFrecency.Record(usage, "page:stats", Now);

        Assert.True(PaletteFrecency.Score(usage, "action:zen mode", Now) >
                    PaletteFrecency.Score(usage, "page:stats", Now));
    }

    [Fact]
    public void One_fresh_run_beats_a_pile_from_three_weeks_ago()
    {
        var usage = new Dictionary<string, PaletteUse>
        {
            ["page:stats"] = new() { Count = 2, LastUsed = Now.AddDays(-21) },
            ["page:review"] = new() { Count = 1, LastUsed = Now }
        };

        Assert.True(PaletteFrecency.Score(usage, "page:review", Now) >
                    PaletteFrecency.Score(usage, "page:stats", Now));
    }

    [Fact]
    public void A_week_halves_the_warmth()
    {
        var usage = new Dictionary<string, PaletteUse>
        {
            ["a"] = new() { Count = 2, LastUsed = Now.AddDays(-7) },
            ["b"] = new() { Count = 1, LastUsed = Now }
        };

        Assert.Equal(
            PaletteFrecency.Score(usage, "a", Now),
            PaletteFrecency.Score(usage, "b", Now),
            precision: 10);
    }

    [Fact]
    public void The_table_stays_capped_and_drops_the_coldest()
    {
        var usage = new Dictionary<string, PaletteUse>();

        // 50 old, cold entries…
        for (var i = 0; i < 50; i++)
            usage[$"old:{i}"] = new PaletteUse { Count = 1, LastUsed = Now.AddDays(-60) };

        // …then one warm run pushes past the cap.
        PaletteFrecency.Record(usage, "page:today", Now);

        Assert.Equal(50, usage.Count);
        Assert.True(usage.ContainsKey("page:today")); // the newcomer survives
    }
}

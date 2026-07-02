using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The retrospective is auto-written prose, so the wording rules —
/// what earns a line, how the momentum reads, which day wins — are the
/// contract. A week with nothing in it must say nothing at all.</summary>
public class WeeklyRetrospectiveTests
{
    // A Wednesday, so the window (Thu 25 Jun – Wed 1 Jul) crosses a month edge.
    private static readonly DateOnly Today = new(2026, 7, 1);

    private static AppState StateOn(DateOnly date) => new()
    {
        Today = new DailyStats { Date = date },
        IntentionDate = date
    };

    private static DailyStats Day(int daysAgo, int sessions, double minutes) => new()
    {
        Date = Today.AddDays(-daysAgo),
        CompletedSessions = sessions,
        FocusedMinutes = minutes
    };

    [Fact]
    public void An_empty_week_says_nothing()
    {
        Assert.Empty(WeeklyRetrospective.Lines(StateOn(Today), Today));
    }

    [Fact]
    public void Focus_totals_cover_history_and_the_live_today()
    {
        var state = StateOn(Today);
        state.History.Add(Day(2, 3, 75));
        state.Today.CompletedSessions = 1;
        state.Today.FocusedMinutes = 25;

        var lines = WeeklyRetrospective.Lines(state, Today);

        Assert.Equal("1.7h of focus across 4 sessions on 2 days", lines[0]);
    }

    [Fact]
    public void Momentum_reads_up_down_or_level_against_the_prior_week()
    {
        var state = StateOn(Today);
        state.History.Add(Day(1, 2, 120));
        state.History.Add(Day(9, 2, 60)); // prior window

        var up = WeeklyRetrospective.Lines(state, Today)[0];
        Assert.EndsWith("— up 1h on the week before", up);

        state.History[1].FocusedMinutes = 180;
        var down = WeeklyRetrospective.Lines(state, Today)[0];
        Assert.EndsWith("— down 1h on the week before", down);

        state.History[1].FocusedMinutes = 120;
        var level = WeeklyRetrospective.Lines(state, Today)[0];
        Assert.EndsWith("— level with the week before", level);
    }

    [Fact]
    public void Days_outside_the_window_are_ignored()
    {
        var state = StateOn(Today);
        state.History.Add(Day(7, 4, 200)); // one day too old for the window

        Assert.Empty(WeeklyRetrospective.Lines(state, Today));
    }

    [Fact]
    public void Courses_rank_by_minutes_and_cap_at_three()
    {
        var state = StateOn(Today);
        var day = Day(1, 4, 240);
        day.FocusByCourse["math101"] = 120;
        day.FocusByCourse["cs210"] = 60;
        day.FocusByCourse["phys120"] = 40;
        day.FocusByCourse["eng201"] = 20;
        state.History.Add(day);

        var lines = WeeklyRetrospective.Lines(state, Today);

        Assert.Contains("most of it went to math101 (2h), then cs210 (1h) and phys120 (0.7h)",
                        lines);
        Assert.DoesNotContain(lines, l => l.Contains("eng201"));
    }

    [Fact]
    public void The_busiest_day_needs_at_least_two_active_days()
    {
        var state = StateOn(Today);
        state.History.Add(Day(1, 2, 50));

        Assert.DoesNotContain(WeeklyRetrospective.Lines(state, Today),
                              l => l.StartsWith("busiest day"));

        state.History.Add(Day(2, 3, 90)); // 2026-06-29, a Monday
        Assert.Contains("busiest day — monday, 1.5h",
                        WeeklyRetrospective.Lines(state, Today));
    }

    [Fact]
    public void Cleared_flashcards_earn_a_line()
    {
        var state = StateOn(Today);
        state.History.Add(Day(1, 1, 25));
        state.History[0].ReviewedCards = 12;
        state.Today.ReviewedCards = 3;

        Assert.Contains("15 flashcards cleared in review",
                        WeeklyRetrospective.Lines(state, Today));
    }

    [Fact]
    public void A_review_only_week_still_gets_a_write_up()
    {
        var state = StateOn(Today);
        state.Today.ReviewedCards = 8;

        var lines = WeeklyRetrospective.Lines(state, Today);

        Assert.Equal("no focus logged this week", lines[0]);
        Assert.Contains("8 flashcards cleared in review", lines);
    }

    [Fact]
    public void Intentions_count_the_banked_journal_plus_the_live_today()
    {
        var state = StateOn(Today);
        state.History.Add(Day(1, 1, 25));
        state.Journal.Add(new DayNote
        {
            Date = Today.AddDays(-1),
            Intention = "finish the lab",
            IntentionKept = true
        });
        state.Journal.Add(new DayNote
        {
            Date = Today.AddDays(-3),
            Intention = "start revising",
            IntentionKept = false
        });
        state.DailyIntention = "read chapter 4";
        state.IntentionKept = true;

        Assert.Contains("3 intentions set · 2 kept",
                        WeeklyRetrospective.Lines(state, Today));
    }

    [Fact]
    public void Singulars_read_naturally()
    {
        var state = StateOn(Today);
        state.Today.CompletedSessions = 1;
        state.Today.FocusedMinutes = 25;

        var lines = WeeklyRetrospective.Lines(state, Today);

        Assert.Equal("0.4h of focus across 1 session on 1 day", lines[0]);
    }
}

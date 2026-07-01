using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The midnight rollover banks the finished day into history and the
/// journal — the streak, the calendar and the look-back all depend on these
/// rules, and a mistake here corrupts them silently. So: every branch.</summary>
public class DailyResetTests
{
    private static readonly DateOnly Yesterday = new(2026, 6, 30);
    private static readonly DateOnly Today = new(2026, 7, 1);

    private static AppState StateOn(DateOnly date) => new()
    {
        Today = new DailyStats { Date = date },
        IntentionDate = date
    };

    [Fact]
    public void Same_day_changes_nothing()
    {
        var state = StateOn(Today);
        state.Today.CompletedSessions = 3;
        state.DailyIntention = "keep going";

        Assert.False(DailyReset.Apply(state, Today));
        Assert.Equal(3, state.Today.CompletedSessions);
        Assert.Equal("keep going", state.DailyIntention);
        Assert.Empty(state.History);
        Assert.Empty(state.Journal);
    }

    [Fact]
    public void Rollover_banks_stats_and_starts_a_fresh_day()
    {
        var state = StateOn(Yesterday);
        state.Today.CompletedSessions = 4;
        state.Today.FocusedMinutes = 100;

        Assert.True(DailyReset.Apply(state, Today));

        var banked = Assert.Single(state.History);
        Assert.Equal(Yesterday, banked.Date);
        Assert.Equal(4, banked.CompletedSessions);

        Assert.Equal(Today, state.Today.Date);
        Assert.Equal(0, state.Today.CompletedSessions);
        Assert.Equal(0, state.Today.FocusedMinutes);
    }

    [Fact]
    public void Empty_days_are_not_banked()
    {
        var state = StateOn(Yesterday);

        Assert.True(DailyReset.Apply(state, Today));
        Assert.Empty(state.History);
        Assert.Equal(Today, state.Today.Date);
    }

    [Fact]
    public void Banking_replaces_an_existing_entry_for_the_same_date()
    {
        var state = StateOn(Yesterday);
        state.Today.FocusedMinutes = 50;
        state.History.Add(new DailyStats { Date = Yesterday, FocusedMinutes = 10 });

        DailyReset.Apply(state, Today);

        var banked = Assert.Single(state.History, d => d.Date == Yesterday);
        Assert.Equal(50, banked.FocusedMinutes);
    }

    [Fact]
    public void History_is_capped_at_400_days_dropping_the_oldest()
    {
        var state = StateOn(Yesterday);
        state.Today.FocusedMinutes = 25;
        for (var i = 0; i < 400; i++)
            state.History.Add(new DailyStats
            {
                Date = Yesterday.AddDays(-400 + i),
                FocusedMinutes = 25
            });

        DailyReset.Apply(state, Today);

        Assert.Equal(400, state.History.Count);
        Assert.Equal(Yesterday, state.History[^1].Date);
        Assert.DoesNotContain(state.History, d => d.Date == Yesterday.AddDays(-400));
    }

    [Fact]
    public void Rollover_banks_intention_and_reflection_into_the_journal()
    {
        var state = StateOn(Yesterday);
        state.DailyIntention = "finish the lab report";
        state.IntentionKept = true;
        state.DailyReflection = "got there in the end";

        Assert.True(DailyReset.Apply(state, Today));

        var note = Assert.Single(state.Journal);
        Assert.Equal(Yesterday, note.Date);
        Assert.Equal("finish the lab report", note.Intention);
        Assert.True(note.IntentionKept);
        Assert.Equal("got there in the end", note.Reflection);

        Assert.Equal(string.Empty, state.DailyIntention);
        Assert.False(state.IntentionKept);
        Assert.Equal(string.Empty, state.DailyReflection);
        Assert.Equal(Today, state.IntentionDate);
    }

    [Fact]
    public void A_day_with_nothing_written_leaves_no_journal_entry()
    {
        var state = StateOn(Yesterday);
        state.DailyIntention = "   ";

        DailyReset.Apply(state, Today);

        Assert.Empty(state.Journal);
        Assert.Equal(Today, state.IntentionDate);
    }

    [Fact]
    public void A_reflection_alone_is_still_banked()
    {
        var state = StateOn(Yesterday);
        state.DailyReflection = "quiet day, mostly reading";

        DailyReset.Apply(state, Today);

        var note = Assert.Single(state.Journal);
        Assert.Equal(string.Empty, note.Intention);
        Assert.Equal("quiet day, mostly reading", note.Reflection);
    }

    [Fact]
    public void Journal_is_capped_at_400_notes_dropping_the_oldest()
    {
        var state = StateOn(Yesterday);
        state.DailyIntention = "one more";
        for (var i = 0; i < 400; i++)
            state.Journal.Add(new DayNote
            {
                Date = Yesterday.AddDays(-400 + i),
                Intention = "old"
            });

        DailyReset.Apply(state, Today);

        Assert.Equal(400, state.Journal.Count);
        Assert.Equal(Yesterday, state.Journal[^1].Date);
    }

    [Fact]
    public void Skipping_several_days_still_banks_the_last_recorded_day_once()
    {
        // App closed for a week: the stored day rolls straight to today.
        var state = StateOn(Yesterday.AddDays(-6));
        state.Today.CompletedSessions = 2;
        state.DailyIntention = "start the essay";

        Assert.True(DailyReset.Apply(state, Today));

        Assert.Equal(Yesterday.AddDays(-6), Assert.Single(state.History).Date);
        Assert.Equal(Yesterday.AddDays(-6), Assert.Single(state.Journal).Date);
        Assert.Equal(Today, state.Today.Date);
        Assert.Equal(Today, state.IntentionDate);
    }
}

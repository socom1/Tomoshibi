using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The learning-step state machine wrapped around FSRS: new cards walk
/// the steps, lapses relearn, leeches get suspended, fuzz is deterministic, and
/// the queue honours daily caps and holds back suspended/buried cards.</summary>
public class SchedulerTests
{
    private static readonly DateTime Now = new(2026, 7, 6, 12, 0, 0);

    private static (Deck deck, Note note, Card card) NewCard()
    {
        var card = new Card { State = CardState.New, Due = Now };
        var note = new Note { Cards = { card } };
        var deck = new Deck { Notes = { note } };
        return (deck, note, card);
    }

    private static (Deck deck, Note note, Card card) ReviewCard(double stability = 20, int lapses = 0)
    {
        var card = new Card
        {
            State = CardState.Review,
            Stability = stability,
            Difficulty = 5,
            Lapses = lapses,
            Reps = 5,
            LastReviewed = Now.AddDays(-stability),
            Due = Now
        };
        var note = new Note { Cards = { card } };
        var deck = new Deck { Notes = { note } };
        return (deck, note, card);
    }

    [Fact]
    public void New_card_good_advances_to_the_second_learning_step()
    {
        var (deck, note, card) = NewCard();

        Scheduler.Apply(deck, note, card, ReviewGrade.Good, Now, new Random(1));

        Assert.Equal(CardState.Learning, card.State);
        Assert.Equal(1, card.StepIndex);
        Assert.Equal(Now.AddMinutes(10), card.Due);
    }

    [Fact]
    public void New_card_again_stays_on_the_first_step()
    {
        var (deck, note, card) = NewCard();

        Scheduler.Apply(deck, note, card, ReviewGrade.Again, Now, new Random(1));

        Assert.Equal(CardState.Learning, card.State);
        Assert.Equal(0, card.StepIndex);
        Assert.Equal(Now.AddMinutes(1), card.Due);
    }

    [Fact]
    public void New_card_easy_graduates_straight_to_review()
    {
        var (deck, note, card) = NewCard();

        Scheduler.Apply(deck, note, card, ReviewGrade.Easy, Now, new Random(1));

        Assert.Equal(CardState.Review, card.State);
        // Seeded from InitialStability(Easy) ≈ 15.7 days out.
        Assert.True((card.Due - Now).TotalDays >= 10);
    }

    [Fact]
    public void Good_on_the_last_learning_step_graduates()
    {
        var (deck, note, card) = NewCard();
        // Walk to the last step first.
        Scheduler.Apply(deck, note, card, ReviewGrade.Good, Now, new Random(1)); // step 1 (10m)

        Scheduler.Apply(deck, note, card, ReviewGrade.Good, Now.AddMinutes(10), new Random(1));

        Assert.Equal(CardState.Review, card.State);
        Assert.True(DateOnly.FromDateTime(card.Due) > DateOnly.FromDateTime(Now));
    }

    [Fact]
    public void A_review_lapse_relearns_and_counts_the_lapse()
    {
        var (deck, note, card) = ReviewCard(stability: 30);

        Scheduler.Apply(deck, note, card, ReviewGrade.Again, Now, new Random(1));

        Assert.Equal(CardState.Relearning, card.State);
        Assert.Equal(1, card.Lapses);
        Assert.Equal(Now.AddMinutes(10), card.Due); // relearning step
    }

    [Fact]
    public void Repeated_success_pushes_the_interval_further_out_each_time()
    {
        var (deck, note, card) = ReviewCard(stability: 5);
        var first = card.Due;

        Scheduler.Apply(deck, note, card, ReviewGrade.Good, Now, new Random(7));
        var afterOne = (card.Due - Now).TotalDays;

        var later = card.Due;
        Scheduler.Apply(deck, note, card, ReviewGrade.Good, later, new Random(7));
        var afterTwo = (card.Due - later).TotalDays;

        Assert.True(afterOne >= 1);
        Assert.True(afterTwo > afterOne);
    }

    [Fact]
    public void A_card_becomes_a_leech_and_is_suspended_at_the_threshold()
    {
        // Threshold 8; this Again is the 8th lapse.
        var (deck, note, card) = ReviewCard(stability: 10, lapses: 7);

        Scheduler.Apply(deck, note, card, ReviewGrade.Again, Now, new Random(1));

        Assert.Equal(8, card.Lapses);
        Assert.Contains("leech", note.Tags);
        Assert.True(card.Suspended);
    }

    [Fact]
    public void Between_leech_thresholds_the_card_is_not_reflagged()
    {
        // Threshold 8, re-flag every 4 lapses → 8, 12, 16. Lapse 9 must not flag.
        var (deck, note, card) = ReviewCard(stability: 10, lapses: 8);

        Scheduler.Apply(deck, note, card, ReviewGrade.Again, Now, new Random(1));

        Assert.Equal(9, card.Lapses);
        Assert.False(card.Suspended);
        Assert.DoesNotContain("leech", note.Tags);
    }

    [Fact]
    public void Grading_is_deterministic_for_a_given_seed()
    {
        var (deckA, noteA, cardA) = ReviewCard(stability: 40);
        var (deckB, noteB, cardB) = ReviewCard(stability: 40);

        Scheduler.Apply(deckA, noteA, cardA, ReviewGrade.Good, Now, new Random(42));
        Scheduler.Apply(deckB, noteB, cardB, ReviewGrade.Good, Now, new Random(42));

        Assert.Equal(cardA.Due, cardB.Due);
        Assert.Equal(cardA.Stability, cardB.Stability, 9);
    }

    [Fact]
    public void Scheduled_intervals_never_fall_below_one_day()
    {
        var (deck, note, card) = ReviewCard(stability: 0.2);

        Scheduler.Apply(deck, note, card, ReviewGrade.Good, Now, new Random(3));

        Assert.True((card.Due.Date - Now.Date).TotalDays >= 1);
    }

    [Fact]
    public void Build_queue_caps_new_cards_per_day()
    {
        var deck = new Deck { Options = { NewPerDay = 2 } };
        for (var i = 0; i < 5; i++)
            deck.Notes.Add(new Note { Cards = { new Card { State = CardState.New, Due = Now } } });

        var queue = Scheduler.BuildQueue(new[] { deck }, Now, _ => 0, _ => 0, new Random(1));

        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Build_queue_subtracts_new_cards_already_done_today()
    {
        var deck = new Deck { Options = { NewPerDay = 5 } };
        for (var i = 0; i < 5; i++)
            deck.Notes.Add(new Note { Cards = { new Card { State = CardState.New, Due = Now } } });

        // Three already introduced today → only two slots left.
        var queue = Scheduler.BuildQueue(new[] { deck }, Now, _ => 0, _ => 3, new Random(1));

        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Build_queue_excludes_suspended_and_buried_cards()
    {
        var deck = new Deck();
        deck.Notes.Add(new Note { Cards = { new Card { State = CardState.New, Due = Now, Suspended = true } } });
        deck.Notes.Add(new Note
        {
            Cards = { new Card { State = CardState.Review, Due = Now, BuriedUntil = DateOnly.FromDateTime(Now).AddDays(1) } }
        });
        deck.Notes.Add(new Note { Cards = { new Card { State = CardState.New, Due = Now } } });

        var queue = Scheduler.BuildQueue(new[] { deck }, Now, _ => 0, _ => 0, new Random(1));

        Assert.Single(queue);
    }

    [Fact]
    public void Preview_returns_four_non_decreasing_intervals()
    {
        var (deck, _, card) = NewCard();

        var preview = Scheduler.Preview(deck.Options, card, Now);

        Assert.Equal(4, preview.Count);
        for (var i = 1; i < preview.Count; i++)
            Assert.True(preview[i].Interval >= preview[i - 1].Interval);
    }

    [Fact]
    public void Preview_does_not_mutate_the_card()
    {
        var (deck, _, card) = ReviewCard(stability: 12);
        var due = card.Due;
        var reps = card.Reps;

        Scheduler.Preview(deck.Options, card, Now);

        Assert.Equal(due, card.Due);
        Assert.Equal(reps, card.Reps);
    }
}

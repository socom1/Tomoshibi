using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Tomoshibi.ViewModels;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The review page. The FSRS scheduler underneath it has its own
/// tests as pure logic; this is the wiring around it — that starting a session
/// picks up the due cards, that a card doesn't show its answer until asked,
/// that grading moves on, and that the session ends rather than looping.
///
/// <para>It went uncovered because it holds a <c>DispatcherTimer</c> for the
/// auto-reveal countdown, so constructing one needs a dispatcher. The headless
/// session has one.</para></summary>
[Collection(HeadlessCollection.Name)]
public class ReviewViewModelTests : IDisposable
{
    /// <summary>Records rather than writes — the real one flushes to disk on
    /// every single grade, which these tests have no use for.</summary>
    private sealed class NullLog : IReviewLogService
    {
        public List<ReviewLogEntry> Written { get; } = new();

        public void Append(ReviewLogEntry entry) => Written.Add(entry);
        public IReadOnlyList<ReviewLogEntry> All() => Written;

        public int CountToday(Guid deckId, CardState stateBefore, DateOnly today) =>
            Written.Count(e => e.DeckId == deckId
                            && e.StateBefore == stateBefore
                            && DateOnly.FromDateTime(e.Timestamp) == today);
    }

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "tomoshibi-tests", Guid.NewGuid().ToString("N"));

    public ReviewViewModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    /// <summary>A deck of new cards, due now.</summary>
    private static Deck Deck(string name, params string[] fronts)
    {
        var deck = new Deck { Name = name };
        foreach (var front in fronts)
        {
            deck.Notes.Add(new Note
            {
                Type = NoteType.Basic,
                Fields = { front, $"answer to {front}" },
                Cards = { new Card { Ord = 0, State = CardState.New, Due = DateTime.Now.AddMinutes(-1) } }
            });
        }
        return deck;
    }

    private (ReviewViewModel Vm, AppState State, NullLog Log) Vm(params Deck[] decks)
    {
        var state = new AppState();
        state.Decks.AddRange(decks);
        var log = new NullLog();
        var wallet = new WalletViewModel(state, () => { });
        var vm = new ReviewViewModel(state, () => { }, wallet, log, new MediaStore(_dir));
        return (vm, state, log);
    }

    // ---- starting a session ----

    [Fact]
    public void Reviewing_everything_picks_up_the_due_cards() => Headless.Run(() =>
    {
        var (vm, _, _) = Vm(Deck("kanji", "火", "水"));

        vm.ReviewAllCommand.Execute(null);

        Assert.False(vm.IsSessionDone);
        Assert.Contains("all decks", vm.ReviewScopeLabel);
    });

    [Fact]
    public void A_deck_with_nothing_due_never_enters_a_session() => Headless.Run(() =>
    {
        // An empty queue backs out before anything is set up, rather than
        // starting a session with no card in it.
        var (vm, _, _) = Vm(new Deck { Name = "empty" });

        vm.ReviewAllCommand.Execute(null);

        Assert.False(vm.IsReviewing);
        Assert.Empty(vm.ReviewScopeLabel);
    });

    // ---- the card itself ----

    [Fact]
    public void A_card_keeps_its_answer_back_until_asked() => Headless.Run(() =>
    {
        var (vm, _, _) = Vm(Deck("kanji", "火"));
        vm.ReviewAllCommand.Execute(null);

        Assert.False(vm.IsFlipped);
        Assert.Equal(CardSide.Front, vm.CurrentSide);

        vm.FlipCommand.Execute(null);

        Assert.True(vm.IsFlipped);
        Assert.Equal(CardSide.Back, vm.CurrentSide);
    });

    [Fact]
    public void The_prompt_stays_up_beside_the_answer_by_default() => Headless.Run(() =>
    {
        var (vm, _, _) = Vm(Deck("kanji", "火"));
        vm.ReviewAllCommand.Execute(null);
        vm.FlipCommand.Execute(null);

        Assert.True(vm.ShowFront);
        Assert.True(vm.ShowDivider);   // both on screen, so the rule earns its place
    });

    [Fact]
    public void The_anki_style_flip_replaces_the_prompt_instead() => Headless.Run(() =>
    {
        var (vm, state, _) = Vm(Deck("kanji", "火"));
        state.ReviewHideFrontOnReveal = true;
        vm.ReviewAllCommand.Execute(null);

        vm.FlipCommand.Execute(null);

        Assert.False(vm.ShowFront);
        Assert.False(vm.ShowDivider);  // nothing to divide
    });

    // ---- grading ----

    [Fact]
    public void Grading_a_card_moves_on_to_the_next_one() => Headless.Run(() =>
    {
        var (vm, _, _) = Vm(Deck("kanji", "火", "水"));
        vm.ReviewAllCommand.Execute(null);
        vm.FlipCommand.Execute(null);
        var first = vm.CardFrontSource;

        vm.GradeGoodCommand.Execute(null);

        Assert.NotEqual(first, vm.CardFrontSource);
        Assert.False(vm.IsFlipped);   // the next card starts face down
        Assert.False(vm.IsSessionDone);
    });

    [Fact]
    public void A_grade_without_a_reveal_is_ignored() => Headless.Run(() =>
    {
        // You have to have seen the answer before you can say you knew it —
        // otherwise a stray keypress grades a card nobody read.
        var (vm, _, log) = Vm(Deck("kanji", "火"));
        vm.ReviewAllCommand.Execute(null);

        vm.GradeGoodCommand.Execute(null);

        Assert.Empty(log.Written);
        Assert.False(vm.IsFlipped);
    });

    [Fact]
    public void A_session_of_new_cards_finishes_rather_than_looping() => Headless.Run(() =>
    {
        // A new card graded Good goes to a learning step and comes back in the
        // same session, so "one card, one grade" doesn't end it. What matters
        // is that repeating does — a queue that never drains is a page you
        // can't leave.
        var (vm, _, _) = Vm(Deck("kanji", "火"));
        vm.ReviewAllCommand.Execute(null);

        for (var i = 0; i < 20 && !vm.IsSessionDone; i++)
        {
            vm.FlipCommand.Execute(null);
            vm.GradeGoodCommand.Execute(null);
        }

        Assert.True(vm.IsSessionDone);
    });

    [Fact]
    public void Every_answer_is_written_to_the_log() => Headless.Run(() =>
    {
        // The log is what the stats page reads; a review that isn't recorded
        // is a review that didn't happen as far as the streak is concerned.
        var (vm, _, log) = Vm(Deck("kanji", "火", "水"));
        vm.ReviewAllCommand.Execute(null);

        vm.FlipCommand.Execute(null);
        vm.GradeGoodCommand.Execute(null);
        vm.FlipCommand.Execute(null);
        vm.GradeAgainCommand.Execute(null);

        Assert.Equal(2, log.Written.Count);
    });

    [Fact]
    public void Again_puts_the_card_back_rather_than_burying_it() => Headless.Run(() =>
    {
        // "again" means they didn't know it. It has to come round again in the
        // same session, or the button quietly means "skip".
        var (vm, state, _) = Vm(Deck("kanji", "火"));
        vm.ReviewAllCommand.Execute(null);
        vm.FlipCommand.Execute(null);

        vm.GradeAgainCommand.Execute(null);

        var card = state.Decks[0].Notes[0].Cards[0];
        Assert.Equal(CardState.Learning, card.State);
        Assert.False(vm.IsSessionDone);   // it comes back before the session ends
    });

    // ---- decks ----

    [Fact]
    public void Opening_a_deck_scopes_the_session_to_it() => Headless.Run(() =>
    {
        var (vm, _, _) = Vm(Deck("kanji", "火"), Deck("vocab", "犬"));

        var kanji = Assert.Single(vm.Decks, d => d.Model.Name == "kanji");
        vm.ReviewDeckCommand.Execute(kanji);

        Assert.Contains("kanji", vm.ReviewScopeLabel);
        Assert.False(vm.IsSessionDone);
    });

    [Fact]
    public void Deleting_a_deck_takes_it_immediately_and_asks_nothing() => Headless.Run(() =>
    {
        // Pinning what it does, not what it ought to. One click on the trash
        // icon takes the deck, its notes, its cards and their scheduling
        // history, with no confirmation and no undo — while deleting a subject,
        // which loses less, does ask. If that gap gets closed this test is
        // where it will fail, which is the point of writing it down.
        var (vm, state, _) = Vm(Deck("kanji", "火", "水"));

        vm.DeleteDeckCommand.Execute(vm.Decks[0]);

        Assert.Empty(state.Decks);
        Assert.Empty(vm.Decks);
    });
}

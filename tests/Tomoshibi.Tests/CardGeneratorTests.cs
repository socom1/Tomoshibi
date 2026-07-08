using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Card generation is where a note edit turns into cards. The key
/// promise is that surviving cards keep their scheduling when a note changes —
/// editing the front of a card must never reset its FSRS state.</summary>
public class CardGeneratorTests
{
    [Fact]
    public void Basic_note_generates_one_card()
    {
        var note = new Note { Type = NoteType.Basic, Fields = { "q", "a" } };
        CardGenerator.Sync(note);
        Assert.Equal(new[] { 0 }, note.Cards.Select(c => c.Ord));
    }

    [Fact]
    public void Reversed_note_generates_two_cards()
    {
        var note = new Note { Type = NoteType.BasicReversed, Fields = { "q", "a" } };
        CardGenerator.Sync(note);
        Assert.Equal(new[] { 0, 1 }, note.Cards.Select(c => c.Ord).OrderBy(o => o));
    }

    [Fact]
    public void Cloze_note_generates_one_card_per_distinct_number()
    {
        var note = new Note { Type = NoteType.Cloze, Fields = { "{{c1::a}} and {{c2::b}} and {{c1::c}}", "" } };
        CardGenerator.Sync(note);
        Assert.Equal(new[] { 1, 2 }, note.Cards.Select(c => c.Ord).OrderBy(o => o));
    }

    [Fact]
    public void Occlusion_note_generates_one_card_per_mask()
    {
        var note = new Note { Type = NoteType.ImageOcclusion };
        note.Occlusions.Add(new OcclusionRect());
        note.Occlusions.Add(new OcclusionRect());
        note.Occlusions.Add(new OcclusionRect());
        CardGenerator.Sync(note);
        Assert.Equal(3, note.Cards.Count);
    }

    [Fact]
    public void Editing_a_field_preserves_surviving_cards_scheduling()
    {
        var note = new Note { Type = NoteType.Cloze, Fields = { "{{c1::a}} {{c2::b}}", "" } };
        CardGenerator.Sync(note);

        // Give c1's card some history.
        var c1 = note.Cards.Single(c => c.Ord == 1);
        c1.State = CardState.Review;
        c1.Stability = 42;
        c1.Reps = 9;

        // Remove the c2 deletion and re-sync.
        note.Fields[0] = "{{c1::a}} b";
        CardGenerator.Sync(note);

        var survivor = Assert.Single(note.Cards);
        Assert.Equal(1, survivor.Ord);
        Assert.Equal(42, survivor.Stability); // untouched
        Assert.Equal(9, survivor.Reps);
    }

    [Fact]
    public void Adding_a_cloze_number_adds_a_fresh_new_card()
    {
        var note = new Note { Type = NoteType.Cloze, Fields = { "{{c1::a}}", "" } };
        CardGenerator.Sync(note);

        note.Fields[0] = "{{c1::a}} {{c2::b}}";
        var changed = CardGenerator.Sync(note);

        Assert.True(changed);
        var added = note.Cards.Single(c => c.Ord == 2);
        Assert.Equal(CardState.New, added.State);
    }

    [Fact]
    public void Sync_is_idempotent_when_nothing_changed()
    {
        var note = new Note { Type = NoteType.Basic, Fields = { "q", "a" } };
        CardGenerator.Sync(note);
        Assert.False(CardGenerator.Sync(note));
    }
}

using System;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The browser is only as good as its filter; each operator, negation
/// and the AND-of-terms behaviour needs to hold.</summary>
public class SearchQueryParserTests
{
    private static readonly DateTime Now = new(2026, 7, 6, 12, 0, 0);

    private static CardMatch Match(
        NoteType type = NoteType.Basic,
        string front = "front", string back = "back",
        string deckName = "deck",
        CardState state = CardState.Review,
        bool suspended = false,
        DateOnly? buriedUntil = null,
        params string[] tags)
    {
        var card = new Card { State = state, Suspended = suspended, BuriedUntil = buriedUntil, Due = Now };
        var note = new Note { Type = type, Fields = { front, back } };
        note.Tags.AddRange(tags);
        note.Cards.Add(card);
        var deck = new Deck { Name = deckName, Notes = { note } };
        return new CardMatch(deck, note, card);
    }

    private static bool Matches(string query, CardMatch m) => SearchQueryParser.Parse(query, Now)(m);

    [Fact]
    public void Empty_query_matches_everything()
    {
        Assert.True(Matches("", Match()));
        Assert.True(Matches("   ", Match()));
    }

    [Fact]
    public void Bare_word_matches_field_text_case_insensitively()
    {
        var m = Match(front: "The Mitochondria", back: "powerhouse");
        Assert.True(Matches("mitochondria", m));
        Assert.True(Matches("POWERHOUSE", m));
        Assert.False(Matches("nucleus", m));
    }

    [Fact]
    public void Tag_and_deck_filters()
    {
        var m = Match(deckName: "Biology 101", tags: new[] { "midterm" });
        Assert.True(Matches("tag:midterm", m));
        Assert.True(Matches("deck:biology", m));
        Assert.False(Matches("tag:final", m));
    }

    [Fact]
    public void Quoted_deck_name_with_spaces()
    {
        var m = Match(deckName: "comp sci");
        Assert.True(Matches("deck:\"comp sci\"", m));
    }

    [Fact]
    public void Is_filters_read_card_state()
    {
        Assert.True(Matches("is:new", Match(state: CardState.New)));
        Assert.True(Matches("is:suspended", Match(suspended: true)));
        Assert.True(Matches("is:leech", Match(tags: new[] { "leech" })));
        Assert.True(Matches("is:buried", Match(buriedUntil: DateOnly.FromDateTime(Now).AddDays(1))));
        Assert.False(Matches("is:new", Match(state: CardState.Review)));
    }

    [Fact]
    public void Note_type_filter()
    {
        Assert.True(Matches("note:cloze", Match(type: NoteType.Cloze)));
        Assert.False(Matches("note:cloze", Match(type: NoteType.Basic)));
    }

    [Fact]
    public void Negation_inverts_a_term()
    {
        var m = Match(tags: new[] { "leech" });
        Assert.False(Matches("-is:leech", m));
        Assert.True(Matches("-is:new", Match(state: CardState.Review)));
    }

    [Fact]
    public void Terms_are_anded_together()
    {
        var m = Match(front: "cell", deckName: "biology", state: CardState.New, tags: new[] { "midterm" });
        Assert.True(Matches("cell tag:midterm is:new", m));
        Assert.False(Matches("cell tag:midterm is:review", m));
    }
}

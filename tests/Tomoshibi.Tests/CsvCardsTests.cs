using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Import/export must round-trip, cope with commas/quotes/newlines
/// inside fields, detect TSV, and infer cloze notes.</summary>
public class CsvCardsTests
{
    [Fact]
    public void Export_then_import_round_trips_notes()
    {
        var notes = new[]
        {
            MakeNote(NoteType.Basic, "capital of France?", "Paris", "geo europe"),
            MakeNote(NoteType.Cloze, "{{c1::Ohm}}'s law", "", "physics")
        };

        var csv = CsvCards.Export(notes);
        var back = CsvCards.Import(csv);

        Assert.Equal(2, back.Count);
        Assert.Equal(NoteType.Basic, back[0].Type);
        Assert.Equal("Paris", back[0].Fields[1]);
        Assert.Equal(new[] { "geo", "europe" }, back[0].Tags);
        Assert.Equal(NoteType.Cloze, back[1].Type);
    }

    [Fact]
    public void Fields_with_commas_quotes_and_newlines_survive()
    {
        var note = MakeNote(NoteType.Basic, "a \"quoted\", comma", "line one\nline two", "");
        var csv = CsvCards.Export(new[] { note });

        var back = CsvCards.Import(csv).Single();

        Assert.Equal("a \"quoted\", comma", back.Fields[0]);
        Assert.Equal("line one\nline two", back.Fields[1]);
    }

    [Fact]
    public void Plain_front_back_without_a_type_column_imports_as_basic()
    {
        var back = CsvCards.Import("front,back,tags\nDog,犬,animals\n");

        var note = Assert.Single(back);
        Assert.Equal(NoteType.Basic, note.Type);
        Assert.Equal("Dog", note.Fields[0]);
        Assert.Equal("犬", note.Fields[1]);
        Assert.Equal(new[] { "animals" }, note.Tags);
    }

    [Fact]
    public void A_first_field_with_cloze_markup_is_inferred_as_cloze()
    {
        var back = CsvCards.Import("{{c1::mitochondria}} is the powerhouse,,\n");
        Assert.Equal(NoteType.Cloze, back.Single().Type);
    }

    [Fact]
    public void Tab_separated_input_is_detected()
    {
        var back = CsvCards.Import("front\tback\nDog\tCanine\n");
        var note = Assert.Single(back);
        Assert.Equal("Dog", note.Fields[0]);
        Assert.Equal("Canine", note.Fields[1]);
    }

    [Fact]
    public void Imported_notes_have_their_cards_generated()
    {
        var note = CsvCards.Import("reversed,hola,hello,\n").Single();
        Assert.Equal(2, note.Cards.Count); // reversed → two cards
    }

    private static Note MakeNote(NoteType type, string f0, string f1, string tags)
    {
        var note = new Note
        {
            Type = type,
            Fields = { f0, f1 },
            Tags = tags.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).ToList()
        };
        CardGenerator.Sync(note);
        return note;
    }
}

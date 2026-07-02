using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The deck reader faces whatever Anki (or a spreadsheet) exported,
/// so the format rules — header lines, metadata columns, CSV-style quoting,
/// HTML flattening — need to hold on real files, and a Tomoshibi export must
/// come back in unchanged.</summary>
public class DeckTsvTests
{
    [Fact]
    public void Empty_input_yields_nothing()
    {
        var result = DeckTsv.Parse("");
        Assert.Empty(result.Cards);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Plain_tab_separated_lines_become_cards()
    {
        var result = DeckTsv.Parse("犬\tdog\n猫\tcat\n");

        Assert.Equal(2, result.Cards.Count);
        Assert.Equal("犬", result.Cards[0].Front);
        Assert.Equal("dog", result.Cards[0].Back);
        Assert.Equal("cat", result.Cards[1].Back);
    }

    [Fact]
    public void Anki_header_lines_are_not_cards_and_extra_columns_are_ignored()
    {
        var result = DeckTsv.Parse(
            "#separator:tab\n" +
            "#html:true\n" +
            "#tags column:3\n" +
            "front side\tback side\tvocab lesson1\n");

        var card = Assert.Single(result.Cards);
        Assert.Equal("front side", card.Front);
        Assert.Equal("back side", card.Back);
    }

    [Fact]
    public void The_separator_header_switches_the_delimiter()
    {
        var result = DeckTsv.Parse(
            "#separator:semicolon\n" +
            "une question;une réponse\n");

        var card = Assert.Single(result.Cards);
        Assert.Equal("une question", card.Front);
        Assert.Equal("une réponse", card.Back);
    }

    [Fact]
    public void Metadata_column_headers_shift_front_and_back()
    {
        // Anki's "include guid / notetype / deck" export prepends columns and
        // declares them in headers — the card text is whatever remains.
        var result = DeckTsv.Parse(
            "#separator:tab\n" +
            "#guid column:1\n" +
            "#notetype column:2\n" +
            "#deck column:3\n" +
            "#tags column:6\n" +
            "a1b2c3\tBasic\tJapanese::N5\t飲む\tto drink\tverb\n");

        var card = Assert.Single(result.Cards);
        Assert.Equal("飲む", card.Front);
        Assert.Equal("to drink", card.Back);
    }

    [Fact]
    public void Quoted_fields_keep_separators_newlines_and_doubled_quotes()
    {
        var result = DeckTsv.Parse(
            "#html:false\n" +
            "\"line one\nline two\ttabbed\"\t\"she said \"\"hi\"\"\"\n");

        var card = Assert.Single(result.Cards);
        Assert.Equal("line one\nline two\ttabbed", card.Front);
        Assert.Equal("she said \"hi\"", card.Back);
    }

    [Fact]
    public void Html_is_flattened_to_plain_text()
    {
        var result = DeckTsv.Parse(
            "#html:true\n" +
            "<b>bold</b> &amp; more<br>next line\tplain &nbsp;back\n");

        var card = Assert.Single(result.Cards);
        Assert.Equal("bold & more\nnext line", card.Front);
        Assert.Equal("plain  back", card.Back);
    }

    [Fact]
    public void A_plain_less_than_sign_is_not_a_tag()
    {
        var result = DeckTsv.Parse("when is x < y?\twhen y is bigger\n");

        Assert.Equal("when is x < y?", Assert.Single(result.Cards).Front);
    }

    [Fact]
    public void The_html_false_header_leaves_text_untouched()
    {
        var result = DeckTsv.Parse(
            "#html:false\n" +
            "what does <br> mean in html?\ta line break\n");

        Assert.Equal("what does <br> mean in html?", Assert.Single(result.Cards).Front);
    }

    [Fact]
    public void Rows_without_both_sides_are_counted_and_skipped()
    {
        var result = DeckTsv.Parse("only a front\n\nfront\tback\n");

        Assert.Single(result.Cards);
        Assert.Equal(1, result.Skipped); // the blank line doesn't count
    }

    [Fact]
    public void Windows_line_endings_are_handled()
    {
        var result = DeckTsv.Parse("a\t1\r\nb\t2\r\n");

        Assert.Equal(2, result.Cards.Count);
        Assert.Equal("1", result.Cards[0].Back);
    }

    [Fact]
    public void Imported_cards_arrive_due()
    {
        var result = DeckTsv.Parse("front\tback\n");
        var today = DateOnly.FromDateTime(DateTime.Now);

        Assert.True(ReviewScheduler.IsDue(Assert.Single(result.Cards), today));
    }

    [Fact]
    public void Write_starts_with_the_anki_headers()
    {
        var text = DeckTsv.Write(new Deck { Name = "empty" });

        Assert.StartsWith("#separator:tab\n#html:false\n", text);
    }

    [Fact]
    public void An_export_round_trips_through_parse_unchanged()
    {
        var deck = new Deck();
        deck.Cards.Add(new Flashcard { Front = "犬", Back = "dog" });
        deck.Cards.Add(new Flashcard
        {
            Front = "multi\nline\twith\ttabs",
            Back = "quotes \"inside\" too"
        });
        deck.Cards.Add(new Flashcard
        {
            Front = "what does <br> mean?", // must survive the html handling
            Back = "a line break"
        });

        var result = DeckTsv.Parse(DeckTsv.Write(deck));

        Assert.Equal(0, result.Skipped);
        Assert.Equal(deck.Cards.Select(c => (c.Front, c.Back)),
                     result.Cards.Select(c => (c.Front, c.Back)));
    }
}

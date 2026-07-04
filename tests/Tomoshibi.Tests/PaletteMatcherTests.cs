using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The palette's ranking rules are the contract: prefix over
/// word-start over substring over subsequence over typo — and short queries
/// must not get typo-matched into noise.</summary>
public class PaletteMatcherTests
{
    [Fact]
    public void An_empty_query_matches_everything_equally()
    {
        Assert.Equal(0, PaletteMatcher.Score("", "dashboard"));
        Assert.Equal(0, PaletteMatcher.Score(null, "stats"));
        Assert.Equal(0, PaletteMatcher.Score("   ", "review"));
    }

    [Fact]
    public void The_ranking_ladder_holds()
    {
        var prefix = PaletteMatcher.Score("sta", "stats");
        var wordStart = PaletteMatcher.Score("sta", "focus stats");
        var substring = PaletteMatcher.Score("tat", "stats");
        var subsequence = PaletteMatcher.Score("stts", "stats");

        Assert.True(prefix > wordStart);
        Assert.True(wordStart > substring);
        Assert.True(substring > subsequence);
    }

    [Theory]
    [InlineData("reveiw", "review")]        // transposition
    [InlineData("algoritms", "algorithms")] // missing letter, long word
    [InlineData("timetabel", "timetable")]  // swapped tail
    public void Typos_still_find_the_row(string typo, string title)
    {
        Assert.NotNull(PaletteMatcher.Score(typo, title));
    }

    [Fact]
    public void Typo_tolerance_ranks_below_a_real_match()
    {
        var real = PaletteMatcher.Score("review", "review");
        var typo = PaletteMatcher.Score("reveiw", "review");

        Assert.True(real > typo);
    }

    [Fact]
    public void Short_queries_do_not_get_typo_matched()
    {
        // "st" is one edit from "at" — but two-letter queries matching half
        // the list would make the palette feel broken, not forgiving.
        Assert.Null(PaletteMatcher.Score("st", "at a glance"));
    }

    [Fact]
    public void Unrelated_text_does_not_match()
    {
        Assert.Null(PaletteMatcher.Score("zzzz", "dashboard"));
        Assert.Null(PaletteMatcher.Score("shop", "timetable"));
    }

    [Fact]
    public void Words_split_on_the_palettes_own_punctuation()
    {
        // "#12 · essay draft" — the ticket number and separators shouldn't
        // hide the word starts.
        Assert.NotNull(PaletteMatcher.Score("essay", "#12 · essay draft"));
        Assert.Equal(80, PaletteMatcher.Score("essay", "#12 · essay draft"));
    }

    [Fact]
    public void Case_never_matters()
    {
        Assert.Equal(
            PaletteMatcher.Score("MATH", "math201 problem set"),
            PaletteMatcher.Score("math", "MATH201 PROBLEM SET"));
    }
}

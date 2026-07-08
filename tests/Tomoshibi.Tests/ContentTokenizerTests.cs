using System.Linq;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The tokenizer feeds every card render, so its grammar — emphasis,
/// media, cloze — must parse cleanly and, crucially, degrade malformed markup
/// to plain text instead of throwing.</summary>
public class ContentTokenizerTests
{
    private static ContentSegment[] Segments(string text)
        => ContentTokenizer.Parse(text).Single().Segments.ToArray();

    [Fact]
    public void Plain_text_is_one_text_segment()
    {
        var seg = Assert.IsType<TextSegment>(Assert.Single(Segments("just words")));
        Assert.Equal("just words", seg.Text);
        Assert.False(seg.Bold);
        Assert.False(seg.Italic);
    }

    [Fact]
    public void Bold_and_italic_are_recognised()
    {
        var segs = Segments("a **b** c *d*");
        Assert.Collection(segs,
            s => Assert.Equal("a ", ((TextSegment)s).Text),
            s => { var t = Assert.IsType<TextSegment>(s); Assert.Equal("b", t.Text); Assert.True(t.Bold); },
            s => Assert.Equal(" c ", ((TextSegment)s).Text),
            s => { var t = Assert.IsType<TextSegment>(s); Assert.Equal("d", t.Text); Assert.True(t.Italic); });
    }

    [Fact]
    public void An_unclosed_bold_marker_stays_literal()
    {
        var seg = Assert.IsType<TextSegment>(Assert.Single(Segments("**oops")));
        Assert.Equal("**oops", seg.Text);
        Assert.False(seg.Bold);
    }

    [Fact]
    public void Media_tokens_parse_by_kind()
    {
        var segs = Segments("see [img:a.png] then [sound:b.mp3] and [video:c.mp4]");
        var media = segs.OfType<MediaSegment>().ToArray();
        Assert.Equal(MediaKind.Image, media[0].Kind);
        Assert.Equal("a.png", media[0].Name);
        Assert.Equal(MediaKind.Audio, media[1].Kind);
        Assert.Equal(MediaKind.Video, media[2].Kind);
    }

    [Fact]
    public void A_media_token_with_no_closing_bracket_is_literal()
    {
        var seg = Assert.IsType<TextSegment>(Assert.Single(Segments("[img:broken")));
        Assert.Equal("[img:broken", seg.Text);
    }

    [Fact]
    public void Cloze_with_and_without_hint()
    {
        var withHint = Segments("{{c1::answer::hint}}").OfType<ClozeSegment>().Single();
        Assert.Equal(1, withHint.Ord);
        Assert.Equal("answer", withHint.Answer);
        Assert.Equal("hint", withHint.Hint);

        var noHint = Segments("{{c2::plain}}").OfType<ClozeSegment>().Single();
        Assert.Equal(2, noHint.Ord);
        Assert.Equal("plain", noHint.Answer);
        Assert.Null(noHint.Hint);
    }

    [Fact]
    public void Malformed_cloze_is_literal_text()
    {
        var seg = Assert.IsType<TextSegment>(Assert.Single(Segments("{{c1 no colons}}")));
        Assert.Equal("{{c1 no colons}}", seg.Text);
    }

    [Fact]
    public void Each_line_is_its_own_block()
    {
        var blocks = ContentTokenizer.Parse("line one\nline two");
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void Plain_text_flattening_shows_answers_and_media_glyphs()
    {
        var flat = ContentTokenizer.ToPlainText("recall {{c1::this}} with [img:x.png]");
        Assert.Equal("recall this with 🖼", flat);
    }
}

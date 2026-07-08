using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Anki fields are HTML; the importer must turn them into Tomoshibi
/// markup faithfully and never choke on messy input.</summary>
public class AnkiHtmlConverterTests
{
    [Fact]
    public void Img_tags_become_image_tokens()
    {
        Assert.Equal("[img:cat.jpg]", AnkiHtmlConverter.Convert("<img src=\"cat.jpg\">"));
        Assert.Equal("[img:dog.png]", AnkiHtmlConverter.Convert("<img src='dog.png' />"));
    }

    [Fact]
    public void Sound_tokens_pass_through()
    {
        Assert.Equal("[sound:a.mp3]", AnkiHtmlConverter.Convert("[sound:a.mp3]"));
    }

    [Fact]
    public void Bold_and_italic_convert()
    {
        Assert.Equal("**hi**", AnkiHtmlConverter.Convert("<b>hi</b>"));
        Assert.Equal("**hi**", AnkiHtmlConverter.Convert("<strong>hi</strong>"));
        Assert.Equal("*hi*", AnkiHtmlConverter.Convert("<i>hi</i>"));
        Assert.Equal("*hi*", AnkiHtmlConverter.Convert("<em>hi</em>"));
    }

    [Fact]
    public void Breaks_and_blocks_become_newlines()
    {
        Assert.Equal("a\nb", AnkiHtmlConverter.Convert("a<br>b"));
        Assert.Equal("a\nb", AnkiHtmlConverter.Convert("<div>a</div><div>b</div>"));
    }

    [Fact]
    public void Entities_are_decoded_and_stray_tags_stripped()
    {
        Assert.Equal("a & b", AnkiHtmlConverter.Convert("a &amp; b"));
        Assert.Equal("plain", AnkiHtmlConverter.Convert("<span class=\"x\">plain</span>"));
    }

    [Fact]
    public void Cloze_markup_is_preserved()
    {
        Assert.Equal("{{c1::answer}}", AnkiHtmlConverter.Convert("{{c1::answer}}"));
    }
}

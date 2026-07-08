using System.Net;
using System.Text.RegularExpressions;

namespace Tomoshibi.Services;

/// <summary>
/// Converts an Anki field (HTML) into Tomoshibi's plain-text markup: images and
/// sounds become <c>[img:…]</c>/<c>[sound:…]</c> tokens, bold/italic become
/// <c>**</c>/<c>*</c>, line breaks become newlines, everything else is stripped
/// and HTML entities are decoded. Regex-based and defensive — never throws.
/// Cloze syntax (<c>{{c1::…}}</c>) is identical in both apps, so it passes
/// through untouched.
/// </summary>
public static partial class AnkiHtmlConverter
{
    public static string Convert(string? html)
    {
        var s = html ?? string.Empty;

        // <img src="x"> / <img src='x'> / <img src=x> → [img:x]
        s = ImgRx().Replace(s, m => $"[img:{m.Groups["src"].Value.Trim()}]");

        // Emphasis.
        s = BoldRx().Replace(s, "**${inner}**");
        s = ItalicRx().Replace(s, "*${inner}*");

        // Line breaks: <br>, block-level closers.
        s = BreakRx().Replace(s, "\n");

        // Strip any remaining tags.
        s = TagRx().Replace(s, string.Empty);

        // Decode entities (&amp; &lt; &nbsp; &#39; …).
        s = WebUtility.HtmlDecode(s);

        // Tidy whitespace the markup left behind: strip trailing spaces on lines
        // and collapse the double breaks that block tags (</div><div>) leave.
        s = TrailingSpacesRx().Replace(s, "\n");
        s = MultiNewlineRx().Replace(s, "\n");
        return s.Trim();
    }

    [GeneratedRegex(@"<img[^>]*?\ssrc\s*=\s*(?:""(?<src>[^""]*)""|'(?<src>[^']*)'|(?<src>[^\s>]+))[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImgRx();

    [GeneratedRegex(@"<(?:b|strong)\b[^>]*>(?<inner>.*?)</(?:b|strong)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BoldRx();

    [GeneratedRegex(@"<(?:i|em)\b[^>]*>(?<inner>.*?)</(?:i|em)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ItalicRx();

    [GeneratedRegex(@"<br\s*/?>|</div>|</p>|<div[^>]*>|<p[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRx();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRx();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpacesRx();

    [GeneratedRegex(@"\n{2,}")]
    private static partial Regex MultiNewlineRx();
}

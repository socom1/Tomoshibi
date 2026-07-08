using System;
using System.Collections.Generic;
using System.Text;

namespace Tomoshibi.Services;

/// <summary>
/// Parses a field string into renderable blocks. The grammar is tiny and
/// line-oriented — each source line becomes one <see cref="ContentBlock"/> —
/// with inline tokens for emphasis, media and cloze deletions:
/// <code>
///   **bold**   *italic*
///   [img:name] [sound:name] [video:name]
///   {{cN::answer}}  {{cN::answer::hint}}
/// </code>
/// Anything malformed (an unclosed <c>**</c>, a media token with no closing
/// bracket) degrades to literal text — the tokenizer never throws.
/// </summary>
public static class ContentTokenizer
{
    public static IReadOnlyList<ContentBlock> Parse(string? text)
    {
        var blocks = new List<ContentBlock>();
        foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            blocks.Add(new ContentBlock { Segments = ParseLine(line) });
        return blocks;
    }

    /// <summary>Flatten a field to plain text for compact previews: text kept,
    /// cloze shown as its answer, media shown as a small glyph.</summary>
    public static string ToPlainText(string? text)
    {
        var sb = new StringBuilder();
        foreach (var block in Parse(text))
        {
            foreach (var seg in block.Segments)
            {
                switch (seg)
                {
                    case TextSegment t: sb.Append(t.Text); break;
                    case ClozeSegment c: sb.Append(c.Answer); break;
                    case MediaSegment { Kind: MediaKind.Image }: sb.Append("🖼"); break;
                    case MediaSegment { Kind: MediaKind.Audio }: sb.Append("🔊"); break;
                    case MediaSegment { Kind: MediaKind.Video }: sb.Append("🎬"); break;
                }
            }
            sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static List<ContentSegment> ParseLine(string line)
    {
        var segments = new List<ContentSegment>();
        var textStart = 0;
        var i = 0;

        void FlushText(int end)
        {
            if (end > textStart)
                segments.Add(new TextSegment { Text = line.Substring(textStart, end - textStart) });
        }

        while (i < line.Length)
        {
            if (TryMedia(line, i, out var media, out var mediaLen))
            {
                FlushText(i);
                segments.Add(media!);
                i += mediaLen;
                textStart = i;
            }
            else if (TryCloze(line, i, out var cloze, out var clozeLen))
            {
                FlushText(i);
                segments.Add(cloze!);
                i += clozeLen;
                textStart = i;
            }
            else if (TryEmphasis(line, i, "**", bold: true, out var bold, out var boldLen))
            {
                FlushText(i);
                segments.Add(bold!);
                i += boldLen;
                textStart = i;
            }
            else if (TryEmphasis(line, i, "*", bold: false, out var italic, out var italicLen))
            {
                FlushText(i);
                segments.Add(italic!);
                i += italicLen;
                textStart = i;
            }
            else
            {
                i++;
            }
        }

        FlushText(line.Length);
        return segments;
    }

    private static bool TryMedia(string s, int i, out MediaSegment? seg, out int length)
    {
        seg = null;
        length = 0;
        (string prefix, MediaKind kind)[] kinds =
        {
            ("[img:", MediaKind.Image),
            ("[sound:", MediaKind.Audio),
            ("[video:", MediaKind.Video)
        };

        foreach (var (prefix, kind) in kinds)
        {
            if (!Matches(s, i, prefix)) continue;
            var close = s.IndexOf(']', i + prefix.Length);
            if (close < 0) return false; // no closer → literal text
            var name = s.Substring(i + prefix.Length, close - (i + prefix.Length)).Trim();
            if (name.Length == 0) return false;
            seg = new MediaSegment { Kind = kind, Name = name };
            length = close - i + 1;
            return true;
        }
        return false;
    }

    private static bool TryCloze(string s, int i, out ClozeSegment? seg, out int length)
    {
        seg = null;
        length = 0;
        if (!Matches(s, i, "{{c")) return false;

        var j = i + 3;
        var digitsStart = j;
        while (j < s.Length && char.IsDigit(s[j])) j++;
        if (j == digitsStart) return false;                 // {{c with no number
        if (!Matches(s, j, "::")) return false;             // must have ::

        var close = s.IndexOf("}}", j, StringComparison.Ordinal);
        if (close < 0) return false;

        var ord = int.Parse(s.Substring(digitsStart, j - digitsStart));
        var body = s.Substring(j + 2, close - (j + 2));
        string answer;
        string? hint = null;
        var sep = body.IndexOf("::", StringComparison.Ordinal);
        if (sep >= 0)
        {
            answer = body[..sep];
            hint = body[(sep + 2)..];
        }
        else
        {
            answer = body;
        }

        seg = new ClozeSegment { Ord = ord, Answer = answer, Hint = hint };
        length = close + 2 - i;
        return true;
    }

    private static bool TryEmphasis(string s, int i, string marker, bool bold,
                                    out TextSegment? seg, out int length)
    {
        seg = null;
        length = 0;
        if (!Matches(s, i, marker)) return false;

        // For a single-'*' italic, don't mistake the '**' bold opener.
        if (!bold && i + 1 < s.Length && s[i + 1] == '*') return false;

        var contentStart = i + marker.Length;
        var close = s.IndexOf(marker, contentStart, StringComparison.Ordinal);
        if (close < 0) return false;                        // unclosed → literal text
        if (close == contentStart) return false;            // empty (** ** / * *)

        seg = new TextSegment
        {
            Text = s.Substring(contentStart, close - contentStart),
            Bold = bold,
            Italic = !bold
        };
        length = close + marker.Length - i;
        return true;
    }

    private static bool Matches(string s, int i, string token)
        => i + token.Length <= s.Length && string.CompareOrdinal(s, i, token, 0, token.Length) == 0;
}

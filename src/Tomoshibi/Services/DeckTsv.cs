using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>What a deck-file import produced — ready-to-add cards plus a
/// count of rows that couldn't be read as a card.</summary>
public class DeckTsvResult
{
    public List<Flashcard> Cards { get; } = new();
    public int Skipped { get; set; }
}

/// <summary>
/// A deliberately small reader/writer for flashcard decks as tab-separated
/// text, compatible with Anki's "Notes in Plain Text" format: optional
/// #key:value header lines at the top, CSV-style quoting for fields that hold
/// tabs or newlines, and the first two columns as front/back. Anki's metadata
/// columns (declared by "#guid column:N"-style headers) are dropped, and HTML
/// in fields is flattened to plain text unless the file says "#html:false" —
/// Tomoshibi cards are plain text. Rows that don't yield both a front and a
/// back are counted and skipped rather than guessed at.
/// </summary>
public static class DeckTsv
{
    public static DeckTsvResult Parse(string text)
    {
        var result = new DeckTsvResult();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var body = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var separator = '\t';
        var html = true; // headerless exports are usually old Anki, HTML included
        var metaColumns = new HashSet<int>();

        // Header lines sit at the top of the file, one "#key:value" per line.
        while (body.StartsWith('#'))
        {
            var lineEnd = body.IndexOf('\n');
            var header = (lineEnd < 0 ? body : body[..lineEnd]).Trim();
            body = lineEnd < 0 ? string.Empty : body[(lineEnd + 1)..];

            var colon = header.IndexOf(':');
            if (colon < 0)
                continue;

            var key = header[1..colon].Trim().ToLowerInvariant();
            var value = header[(colon + 1)..].Trim();

            if (key == "separator")
                separator = SeparatorFor(value) ?? separator;
            else if (key == "html")
                html = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
            else if (key.EndsWith(" column", StringComparison.Ordinal) &&
                     int.TryParse(value, out var col) && col >= 1)
                metaColumns.Add(col); // e.g. "#tags column:3" — not card text
        }

        foreach (var record in Records(body, separator))
        {
            var fields = record
                .Where((_, i) => !metaColumns.Contains(i + 1))
                .Select(f => Flatten(f, html))
                .ToList();

            var front = fields.ElementAtOrDefault(0);
            var back = fields.ElementAtOrDefault(1);

            if (string.IsNullOrEmpty(front) || string.IsNullOrEmpty(back))
            {
                // A blank line isn't a failed card; only count real content.
                if (record.Any(f => !string.IsNullOrWhiteSpace(f)))
                    result.Skipped++;
                continue;
            }

            // Default Due (0001-01-01) reads as brand-new — due straight away.
            result.Cards.Add(new Flashcard { Front = front, Back = back });
        }

        return result;
    }

    /// <summary>A deck as Anki-importable text: two header lines, then one
    /// front-TAB-back line per card, quoting any field that holds a tab,
    /// quote or newline. Parses back losslessly (#html:false skips the
    /// flattening), so export → import round-trips.</summary>
    public static string Write(Deck deck)
    {
        var sb = new StringBuilder();
        sb.Append("#separator:tab\n");
        sb.Append("#html:false\n");

        foreach (var card in deck.Cards)
            sb.Append(Escape(card.Front)).Append('\t')
              .Append(Escape(card.Back)).Append('\n');

        return sb.ToString();
    }

    private static string Escape(string field) =>
        field.IndexOfAny(new[] { '\t', '"', '\n', '\r' }) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    /// <summary>Split into records of fields, honouring CSV-style quoting: a
    /// field that opens with a quote runs — separators, newlines and all —
    /// until the closing quote; doubled quotes inside are literal.</summary>
    private static IEnumerable<List<string>> Records(string body, char sep)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var quoted = false; // the current field opened with a quote

        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < body.Length && body[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
                continue;
            }

            if (c == '"' && field.Length == 0 && !quoted)
            {
                inQuotes = true;
                quoted = true;
            }
            else if (c == sep)
            {
                fields.Add(field.ToString());
                field.Clear();
                quoted = false;
            }
            else if (c == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                quoted = false;
                yield return fields;
                fields = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || quoted || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return fields;
        }
    }

    /// <summary>The separator names Anki writes; a single literal character
    /// is accepted too. Unknown names keep the current separator.</summary>
    private static char? SeparatorFor(string value) => value.ToLowerInvariant() switch
    {
        "tab" => '\t',
        "comma" => ',',
        "semicolon" => ';',
        "space" => ' ',
        "pipe" => '|',
        "colon" => ':',
        _ => value.Length == 1 ? value[0] : null
    };

    private static readonly Regex LineBreakTags = new(
        @"<\s*(?:br\s*/?|/p|/div)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OtherTags = new(
        @"</?[a-zA-Z][^>\n]*>", RegexOptions.Compiled);

    /// <summary>Anki fields usually carry HTML. Turn breaks into newlines,
    /// drop anything else tag-shaped and decode the common entities — a stray
    /// "x &lt; y" in plain text doesn't look like a tag and passes through.</summary>
    internal static string Flatten(string field, bool html)
    {
        if (!html)
            return field.Trim();

        var s = LineBreakTags.Replace(field, "\n");
        s = OtherTags.Replace(s, string.Empty);
        s = s.Replace("&nbsp;", " ")
             .Replace("&lt;", "<")
             .Replace("&gt;", ">")
             .Replace("&quot;", "\"")
             .Replace("&#39;", "'")
             .Replace("&amp;", "&"); // last, so "&amp;lt;" ends as "&lt;"
        return s.Trim();
    }
}

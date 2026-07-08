using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>One card in its context, for the browser's search predicate.</summary>
public sealed record CardMatch(Deck Deck, Note Note, Card Card);

/// <summary>
/// Parses the card-browser search box into a predicate over cards. Space-
/// separated terms are ANDed; a term is one of:
/// <code>
///   tag:x            note has a tag containing x
///   deck:name        deck name contains name  (quote for spaces: deck:"comp sci")
///   is:due|new|learning|review|suspended|leech|buried
///   note:basic|reversed|cloze|occlusion
///   -term            negate any of the above (or a bare word)
///   word             a note field contains word
/// </code>
/// An empty query matches everything. Unknown <c>key:value</c> forms fall back
/// to a literal text match so nothing is silently dropped.
/// </summary>
public static class SearchQueryParser
{
    public static Func<CardMatch, bool> Parse(string? query, DateTime now)
    {
        var terms = Tokenize(query ?? string.Empty);
        if (terms.Count == 0)
            return _ => true;

        var predicates = terms.Select(t => BuildTerm(t, now)).ToList();
        return m => predicates.All(p => p(m));
    }

    private static Func<CardMatch, bool> BuildTerm(string token, DateTime now)
    {
        var negate = token.StartsWith('-') && token.Length > 1;
        if (negate) token = token[1..];

        var predicate = BuildAtom(token, now);
        return negate ? m => !predicate(m) : predicate;
    }

    private static Func<CardMatch, bool> BuildAtom(string token, DateTime now)
    {
        var colon = token.IndexOf(':');
        if (colon > 0)
        {
            var key = token[..colon].ToLowerInvariant();
            var value = token[(colon + 1)..];
            switch (key)
            {
                case "tag":
                    return m => m.Note.Tags.Any(t => Contains(t, value));
                case "deck":
                    return m => Contains(m.Deck.Name, value);
                case "note":
                    return m => NoteTypeMatches(m.Note.Type, value);
                case "is":
                    return BuildIs(value.ToLowerInvariant(), now);
            }
        }

        // Bare word (or unknown key:value) → match against the note's fields.
        return m => m.Note.Fields.Any(f => Contains(ContentTokenizer.ToPlainText(f), token));
    }

    private static Func<CardMatch, bool> BuildIs(string value, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        return value switch
        {
            "due" => m => Scheduler.IsDue(m.Card, now),
            "new" => m => m.Card.State == CardState.New,
            "learning" => m => m.Card.State is CardState.Learning or CardState.Relearning,
            "review" => m => m.Card.State == CardState.Review,
            "suspended" => m => m.Card.Suspended,
            "leech" => m => m.Note.Tags.Any(t => string.Equals(t, "leech", StringComparison.OrdinalIgnoreCase)),
            "buried" => m => m.Card.BuriedUntil is { } b && b > today,
            _ => _ => false
        };
    }

    private static bool NoteTypeMatches(NoteType type, string value) => value.ToLowerInvariant() switch
    {
        "basic" => type == NoteType.Basic,
        "reversed" => type == NoteType.BasicReversed,
        "cloze" => type == NoteType.Cloze,
        "occlusion" => type == NoteType.ImageOcclusion,
        _ => false
    };

    private static bool Contains(string haystack, string needle)
        => needle.Length == 0 || haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    /// <summary>Split on whitespace, but keep quoted spans together so
    /// <c>deck:"comp sci"</c> is one term.</summary>
    private static List<string> Tokenize(string query)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        void Flush()
        {
            if (current.Length > 0) tokens.Add(current.ToString());
            current.Clear();
        }

        foreach (var ch in query)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (char.IsWhiteSpace(ch) && !inQuotes) Flush();
            else current.Append(ch);
        }
        Flush();
        return tokens;
    }
}

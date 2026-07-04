using System;
using System.Linq;

namespace Tomoshibi.Services;

/// <summary>
/// Scoring for the command palette — more forgiving than "contains". A
/// prefix beats a word-start beats a substring beats an in-order
/// subsequence, and as a last resort a word within an edit or two of the
/// query still matches, so "reveiw" finds review and "algoritms" finds
/// algorithms. Higher scores sort first; null means no match. Pure and
/// culture-invariant so the ranking rules stay testable.
/// </summary>
public static class PaletteMatcher
{
    /// <summary>Score <paramref name="candidate"/> against the query. An
    /// empty query matches everything equally (score 0).</summary>
    public static int? Score(string? query, string candidate)
    {
        var q = query?.Trim().ToLowerInvariant() ?? string.Empty;
        if (q.Length == 0)
            return 0;

        var c = candidate.ToLowerInvariant();

        if (c.StartsWith(q, StringComparison.Ordinal))
            return 100;

        var words = c.Split(
            new[] { ' ', '·', '#', '/', '—', '-', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries);

        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal)))
            return 80;

        if (c.Contains(q, StringComparison.Ordinal))
            return 60;

        if (IsSubsequence(q, c))
            return 40;

        // Typo tolerance: a word within one edit of the query (two for longer
        // queries). Too-short queries skip this — "st" shouldn't match "at".
        var maxEdits = q.Length >= 6 ? 2 : q.Length >= 4 ? 1 : 0;
        if (maxEdits > 0 &&
            words.Any(w => Math.Abs(w.Length - q.Length) <= maxEdits &&
                           EditDistance(q, w, maxEdits) <= maxEdits))
            return 20;

        return null;
    }

    /// <summary>Do the query's characters appear in order inside the
    /// candidate? ("stts" → s̲t̲a t̲s̲ in "stats")</summary>
    private static bool IsSubsequence(string q, string c)
    {
        var qi = 0;
        foreach (var ch in c)
        {
            if (qi < q.Length && ch == q[qi])
                qi++;
        }
        return qi == q.Length;
    }

    /// <summary>Damerau-Levenshtein (with adjacent transpositions, so a
    /// swapped pair costs one), capped — anything past
    /// <paramref name="max"/> just reports max+1.</summary>
    private static int EditDistance(string a, string b, int max)
    {
        var d = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(
                    d[i - 1, j] + 1,          // deletion
                    d[i, j - 1] + 1),         // insertion
                    d[i - 1, j - 1] + cost);  // substitution

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1); // transposition
            }
        }

        return Math.Min(d[a.Length, b.Length], max + 1);
    }
}

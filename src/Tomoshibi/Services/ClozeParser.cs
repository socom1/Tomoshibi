using System.Collections.Generic;
using System.Linq;

namespace Tomoshibi.Services;

/// <summary>
/// Reads the cloze numbers out of a field so the card generator knows how many
/// cards a cloze note produces (one per distinct number). Shares the tokenizer
/// so the notion of "what is a cloze" stays in one place.
/// </summary>
public static class ClozeParser
{
    /// <summary>The distinct cloze numbers in a field, ascending. A card's
    /// <see cref="Models.Card.Ord"/> is its cloze number.</summary>
    public static IReadOnlyList<int> Ords(string? field)
    {
        var set = new SortedSet<int>();
        foreach (var block in ContentTokenizer.Parse(field))
            foreach (var seg in block.Segments)
                if (seg is ClozeSegment c)
                    set.Add(c.Ord);
        return set.ToList();
    }

    /// <summary>Does the field contain any cloze deletion?</summary>
    public static bool HasCloze(string? field) => Ords(field).Count > 0;
}

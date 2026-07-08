using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Derives the set of cards a note should have from its type and content, and
/// syncs the note's card list to match — adding cards for new ords, dropping
/// cards whose ord no longer exists (e.g. a deleted cloze), and leaving every
/// surviving card's scheduling state untouched. This is how one note edit keeps
/// all its cards in step, Anki-style.
/// </summary>
public static class CardGenerator
{
    /// <summary>The ords a note should generate cards for.</summary>
    public static IReadOnlyList<int> ExpectedOrds(Note note)
    {
        return note.Type switch
        {
            NoteType.Basic => new[] { 0 },
            NoteType.BasicReversed => new[] { 0, 1 },
            NoteType.Cloze => ClozeParser.Ords(Field(note, 0)),
            NoteType.ImageOcclusion => Enumerable.Range(0, note.Occlusions.Count).ToArray(),
            _ => new[] { 0 }
        };
    }

    /// <summary>Bring <paramref name="note"/>'s cards in line with
    /// <see cref="ExpectedOrds"/>. Returns true if anything changed.</summary>
    public static bool Sync(Note note)
    {
        var expected = ExpectedOrds(note).ToHashSet();
        var changed = false;

        // Drop cards whose ord no longer exists.
        var removed = note.Cards.RemoveAll(c => !expected.Contains(c.Ord));
        if (removed > 0) changed = true;

        // Add a fresh card for each newly expected ord.
        var have = note.Cards.Select(c => c.Ord).ToHashSet();
        foreach (var ord in expected.OrderBy(o => o))
        {
            if (have.Contains(ord)) continue;
            note.Cards.Add(new Card { Ord = ord, State = CardState.New, Due = DateTime.Now });
            changed = true;
        }

        return changed;
    }

    private static string Field(Note note, int i)
        => i < note.Fields.Count ? note.Fields[i] : string.Empty;
}

using System;
using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// A named set of notes, optionally tagged to a course code so the review
/// due-count can link back to a subject. Notes own their cards; the deck also
/// carries its own scheduling options.
/// </summary>
public class Deck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Course { get; set; }

    /// <summary>A small emoji/glyph shown beside the deck. Null = default book.</summary>
    public string? Icon { get; set; }

    /// <summary>Accent colour for the deck (hex, e.g. "#9ECE6A"). Null = matcha.</summary>
    public string? Color { get; set; }

    /// <summary>The notes in this deck; each generates one or more cards.</summary>
    public List<Note> Notes { get; set; } = new();

    /// <summary>Per-deck scheduling options.</summary>
    public DeckOptions Options { get; set; } = new();

    /// <summary>Legacy flat card list from the pre-note schema. Kept so old
    /// state files still deserialise; on first load
    /// <see cref="Services.StateMigrations"/> folds each card into a Basic note
    /// and clears this list.</summary>
    public List<Flashcard> Cards { get; set; } = new();
}

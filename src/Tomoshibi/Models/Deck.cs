using System;
using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// A named set of flashcards, optionally tagged to a course code so the
/// review due-count can link back to a subject.
/// </summary>
public class Deck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Course { get; set; }
    public List<Flashcard> Cards { get; set; } = new();
}

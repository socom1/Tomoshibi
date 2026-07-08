using System;

namespace Tomoshibi.Models;

/// <summary>
/// One reviewable card generated from a <see cref="Note"/>. The card carries
/// all the FSRS scheduling state; the note carries the content. Several cards
/// can share a note (reversed pairs, cloze numbers, occlusion masks), told
/// apart by <see cref="Ord"/>.
/// </summary>
public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Which template / cloze number / occlusion mask this card is,
    /// within its note. Basic = 0, reversed back-card = 1, cloze cN = N,
    /// occlusion mask i = i.</summary>
    public int Ord { get; set; }

    public CardState State { get; set; } = CardState.New;

    /// <summary>Index into the deck's learning / relearning step list while the
    /// card is in a stepping state; ignored otherwise.</summary>
    public int StepIndex { get; set; }

    /// <summary>FSRS memory stability — roughly the number of days until recall
    /// probability falls to the desired retention. 0 until first reviewed.</summary>
    public double Stability { get; set; }

    /// <summary>FSRS difficulty on a 1–10 scale. 0 until first reviewed.</summary>
    public double Difficulty { get; set; }

    /// <summary>When the card next comes up. A <see cref="DateTime"/> (not a
    /// date) so intraday learning steps — "again in 1 minute" — work.</summary>
    public DateTime Due { get; set; }

    public DateTime? LastReviewed { get; set; }

    /// <summary>Total times graded.</summary>
    public int Reps { get; set; }

    /// <summary>Times the card lapsed (was rated Again while in Review). Drives
    /// leech detection.</summary>
    public int Lapses { get; set; }

    /// <summary>Held out of reviews indefinitely until the user unsuspends it
    /// (manually, or automatically on becoming a leech).</summary>
    public bool Suspended { get; set; }

    /// <summary>Held out of reviews until this date (exclusive) — used to bury
    /// a card, or its siblings, until tomorrow.</summary>
    public DateOnly? BuriedUntil { get; set; }
}

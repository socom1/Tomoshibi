using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// Per-deck scheduling knobs, mirroring Anki's deck options. Defaults match
/// Anki's out-of-the-box preset so a fresh deck behaves sensibly.
/// </summary>
public class DeckOptions
{
    /// <summary>Cap on brand-new cards introduced per day.</summary>
    public int NewPerDay { get; set; } = 20;

    /// <summary>Cap on review (non-new, non-learning) cards per day.</summary>
    public int ReviewsPerDay { get; set; } = 200;

    /// <summary>FSRS target recall probability at review time (0–1). Higher
    /// means shorter intervals and more reviews.</summary>
    public double DesiredRetention { get; set; } = 0.9;

    /// <summary>Intraday steps (minutes) a new card walks through before it
    /// graduates to the long schedule.</summary>
    public List<int> LearningStepsMinutes { get; set; } = new() { 1, 10 };

    /// <summary>Steps (minutes) a lapsed card walks through before rejoining
    /// the review schedule.</summary>
    public List<int> RelearningStepsMinutes { get; set; } = new() { 10 };

    /// <summary>Lapse count at which a card is flagged a leech.</summary>
    public int LeechThreshold { get; set; } = 8;

    /// <summary>Suspend a card automatically when it becomes a leech.</summary>
    public bool SuspendLeeches { get; set; } = true;

    /// <summary>Seconds before the answer auto-reveals during review; 0 = off
    /// (the speed-focus timer).</summary>
    public int AutoRevealSeconds { get; set; }

    /// <summary>Hard ceiling on any scheduled interval, in days.</summary>
    public int MaxIntervalDays { get; set; } = 36500;

    /// <summary>Bury the other cards of a note until tomorrow once one of its
    /// cards is seen, so siblings don't stack up in one session.</summary>
    public bool BurySiblings { get; set; } = true;
}

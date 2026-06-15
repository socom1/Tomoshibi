using System;

namespace Tomoshibi.Models;

/// <summary>
/// One flashcard with SM-2-lite spaced-repetition scheduling. The card is
/// "due" once <see cref="Due"/> is today or earlier; reviewing it pushes the
/// date forward by a growing interval.
/// </summary>
public class Flashcard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Front { get; set; } = string.Empty;
    public string Back { get; set; } = string.Empty;

    /// <summary>Days between the last review and the next.</summary>
    public int Interval { get; set; }

    /// <summary>SM-2 ease factor — grows on easy answers, shrinks on lapses.</summary>
    public double Ease { get; set; } = 2.5;

    /// <summary>Consecutive successful reviews; resets to 0 on a lapse.</summary>
    public int Reps { get; set; }

    /// <summary>Next review date. The default (0001-01-01) means a brand-new
    /// card, which reads as due straight away.</summary>
    public DateOnly Due { get; set; }

    public DateOnly? LastReviewed { get; set; }
}

using System;
using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// Focus stats for a single day. Reset when the calendar date rolls over.
/// </summary>
public class DailyStats
{
    public DateOnly Date { get; set; }
    public int CompletedSessions { get; set; }
    public double FocusedMinutes { get; set; }

    /// <summary>Focused minutes broken out by course code (lower-cased) for
    /// the day — populated when the active task carries a course. Lets the
    /// app answer "how long have I studied MATH101?".</summary>
    public Dictionary<string, double> FocusByCourse { get; set; } = new();

    /// <summary>Flashcards cleared in review sessions this day — "again"
    /// repeats don't count twice. Feeds the weekly retrospective.</summary>
    public int ReviewedCards { get; set; }
}

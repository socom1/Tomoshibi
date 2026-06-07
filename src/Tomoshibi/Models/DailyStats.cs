using System;

namespace Tomoshibi.Models;

/// <summary>
/// Focus stats for a single day. Reset when the calendar date rolls over.
/// </summary>
public class DailyStats
{
    public DateOnly Date { get; set; }
    public int CompletedSessions { get; set; }
    public double FocusedMinutes { get; set; }
}

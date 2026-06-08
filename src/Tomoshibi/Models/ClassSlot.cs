using System;

namespace Tomoshibi.Models;

/// <summary>
/// A recurring weekly class slot on the timetable, e.g. "Mon 09:00–11:00 Algebra".
/// Time-of-day only — the model is intentionally calendar-agnostic.
/// </summary>
public class ClassSlot
{
    public WeekDay Day { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Course { get; set; }
}

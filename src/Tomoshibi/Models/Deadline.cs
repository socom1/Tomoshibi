using System;

namespace Tomoshibi.Models;

/// <summary>
/// A one-off dated deadline on the timetable — an essay due, an exam date.
/// Date only; the calendar grid only cares about which day it falls on.
/// </summary>
public class Deadline
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Course { get; set; }
}

using System.Collections.Generic;
using System;

namespace Tomoshibi.Models;

/// <summary>
/// Root object persisted to disk as JSON. One file holds everything the app
/// needs to restore between launches.
/// </summary>
public class AppState
{
    public string DailyIntention { get; set; } = string.Empty;
    public DateOnly IntentionDate { get; set; }

    public DailyStats Today { get; set; } = new();
    public PomodoroSettings Settings { get; set; } = new();

    /// <summary>The user's "task code" — what they've typed in the today
    /// editor. Parsed at the view-model layer; persisted as-is.</summary>
    public string TaskTemplate { get; set; } = string.Empty;

    /// <summary>Legacy task list. Kept around so existing JSON files still
    /// deserialise; on first load the contents are migrated into
    /// <see cref="TaskTemplate"/> and this list is cleared.</summary>
    public List<TaskItem> Tasks { get; set; } = new();

    // Navigation
    public bool IsNavOpen { get; set; } = true;
    public Destination ActiveDestination { get; set; } = Destination.Today;

    // Window placement (0 width = never saved, use the XAML defaults)
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }

    // Timetable
    public List<ClassSlot> ClassSlots { get; set; } = new();
    public List<Deadline> Deadlines { get; set; } = new();
    public ClassesView ClassesView { get; set; } = ClassesView.Grid;
}

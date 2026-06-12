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

    /// <summary>Past days' stats, appended at the midnight rollover. The
    /// streak display reads this; the future streak calendar will too.</summary>
    public List<DailyStats> History { get; set; } = new();

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
    public ClassesView ClassesView { get; set; } = ClassesView.Grid;

    /// <summary>Legacy: standalone deadlines were folded into the todo
    /// backlog as tickets with due dates. Kept so old state files still
    /// deserialise; migrated and emptied on load.</summary>
    public List<Deadline> Deadlines { get; set; } = new();

    // Todo backlog
    public List<TodoItem> Todos { get; set; } = new();
    public int NextTodoNumber { get; set; } = 1;

    // Subjects + grades
    public List<Subject> Subjects { get; set; } = new();
}

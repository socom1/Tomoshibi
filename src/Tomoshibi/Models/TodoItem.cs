using System;
using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// One backlog entry on the todo page — a small ticket: numbered, statused,
/// prioritised, with an optional description, due date, effort estimate and
/// a subtask checklist. Plain serialisable model.
/// </summary>
public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Sequential ticket number (#12), assigned at creation from
    /// <see cref="AppState.NextTodoNumber"/>. 0 = predates numbering and
    /// gets one assigned on load.</summary>
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Course { get; set; }
    public DateOnly? Due { get; set; }

    public TodoStatus Status { get; set; } = TodoStatus.Backlog;
    public TodoPriority Priority { get; set; } = TodoPriority.Normal;

    /// <summary>Estimated focus sessions to finish this. 0 = no estimate.</summary>
    public int EstimatePomos { get; set; }

    /// <summary>Focus sessions actually completed against this ticket —
    /// counted when a session finishes while a today-task with the same
    /// title is active. Closes the loop on the estimate.</summary>
    public int SessionsSpent { get; set; }

    public List<Subtask> Subtasks { get; set; } = new();

    /// <summary>Kept in sync with Status for backwards compatibility with
    /// state files written before the status lifecycle existed.</summary>
    public bool IsDone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? CompletedAt { get; set; }
}

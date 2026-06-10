using System;

namespace Tomoshibi.Models;

/// <summary>
/// One backlog entry on the todo page — longer-horizon than today's task
/// template, lighter than a timetable deadline. Plain serialisable model.
/// </summary>
public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Course { get; set; }
    public DateOnly? Due { get; set; }
    public bool IsDone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

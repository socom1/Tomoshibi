using System;

namespace Tomoshibi.Models;

/// <summary>
/// A single piece of coursework. Plain serialisable model; the UI layer
/// wraps these in a view model when it needs change notification.
/// </summary>
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional course tag, e.g. "CS101".</summary>
    public string? Course { get; set; }

    public bool IsDone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

using System;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// A row in the timetable's deadlines card. Since deadlines were folded into
/// the todo backlog, this wraps an open <see cref="TodoItem"/> that has a due
/// date — the card is just a date-ordered window onto the same tickets.
/// </summary>
public class DeadlineItemViewModel : ViewModelBase
{
    public TodoItem Model { get; }

    public string NumberLabel => $"#{Model.Number}";
    public string DateLabel => Model.Due is { } d ? $"{d:ddd MMM d}".ToLowerInvariant() : string.Empty;
    public string Title => Model.Title;
    public string? Course => Model.Course;
    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);

    /// <summary>Due date already behind us — the date goes sakura.</summary>
    public bool IsOverdue =>
        Model.Due is { } due && due < DateOnly.FromDateTime(DateTime.Now);

    public DeadlineItemViewModel(TodoItem model)
    {
        Model = model;
    }
}

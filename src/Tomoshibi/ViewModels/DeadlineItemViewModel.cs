using System;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Read-only row representation of a <see cref="Deadline"/>. Past deadlines
/// are flagged so the row can dim them.
/// </summary>
public class DeadlineItemViewModel : ViewModelBase
{
    public Deadline Model { get; }

    public string DateLabel => $"{Model.Date:ddd MMM d}".ToLowerInvariant();
    public string Title => Model.Title;
    public string? Course => Model.Course;
    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);
    public bool IsPast => Model.Date < DateOnly.FromDateTime(DateTime.Now);

    public DeadlineItemViewModel(Deadline model)
    {
        Model = model;
    }
}

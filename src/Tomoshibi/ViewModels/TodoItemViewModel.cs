using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Row wrapper around a <see cref="TodoItem"/>: exposes an observable IsDone
/// (ticking writes through to the model and raises <see cref="Changed"/> so
/// the list can save and re-sort) plus the display strings the row binds to.
/// </summary>
public partial class TodoItemViewModel : ViewModelBase
{
    public TodoItem Model { get; }

    /// <summary>Raised after IsDone writes through, so the parent saves.</summary>
    public event Action? Changed;

    [ObservableProperty]
    private bool _isDone;

    public string Title => Model.Title;
    public string? Course => Model.Course;
    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);

    public bool HasDue => Model.Due is not null;
    public string DueLabel => Model.Due is { } d ? $"{d:MMM d}".ToLowerInvariant() : string.Empty;

    /// <summary>Due in the past and still open — the row shows it in sakura.</summary>
    public bool IsOverdue =>
        !IsDone && Model.Due is { } due && due < DateOnly.FromDateTime(DateTime.Now);

    public TodoItemViewModel(TodoItem model)
    {
        Model = model;
        _isDone = model.IsDone;
    }

    partial void OnIsDoneChanged(bool value)
    {
        Model.IsDone = value;
        OnPropertyChanged(nameof(IsOverdue));
        Changed?.Invoke();
    }
}

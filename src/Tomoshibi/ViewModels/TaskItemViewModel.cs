using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Wraps a <see cref="TaskItem"/> so the UI gets change notification on the
/// fields it toggles. The underlying model is the same instance stored in
/// AppState, so flipping IsDone here is what gets persisted.
/// </summary>
public partial class TaskItemViewModel : ViewModelBase
{
    public TaskItem Model { get; }

    /// <summary>Raised when something on the task changes and should be saved.</summary>
    public event Action? Changed;

    [ObservableProperty]
    private bool _isDone;

    public TaskItemViewModel(TaskItem model)
    {
        Model = model;
        _isDone = model.IsDone;
    }

    public string Title => Model.Title;
    public string? Course => Model.Course;
    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);

    partial void OnIsDoneChanged(bool value)
    {
        Model.IsDone = value;
        Changed?.Invoke();
    }
}

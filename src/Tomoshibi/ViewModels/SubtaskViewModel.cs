using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>Checklist row inside an expanded todo. Ticking writes through to
/// the model and raises <see cref="Changed"/> so the parent saves.</summary>
public partial class SubtaskViewModel : ViewModelBase
{
    public Subtask Model { get; }

    public event Action? Changed;

    [ObservableProperty]
    private bool _isDone;

    public string Title => Model.Title;

    public SubtaskViewModel(Subtask model)
    {
        Model = model;
        _isDone = model.IsDone;
    }

    partial void OnIsDoneChanged(bool value)
    {
        Model.IsDone = value;
        Changed?.Invoke();
    }
}

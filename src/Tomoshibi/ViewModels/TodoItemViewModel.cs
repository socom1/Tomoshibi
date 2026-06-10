using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Row wrapper around a <see cref="TodoItem"/> ticket. Carries the row's
/// expansion state, the status-cycling command, and the subtask checklist.
/// Two callbacks distinguish "just persist" (subtask edits — keeps the row
/// alive so typing and expansion survive) from "persist and re-sort"
/// (status changes — the list order depends on status).
/// </summary>
public partial class TodoItemViewModel : ViewModelBase
{
    private readonly Action _save;
    private readonly Action _saveAndResort;

    public TodoItem Model { get; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _newSubtaskTitle = string.Empty;

    public ObservableCollection<SubtaskViewModel> Subtasks { get; } = new();

    // ---- Identity ----
    public string NumberLabel => $"#{Model.Number}";
    public string Title => Model.Title;

    public string Description => Model.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Model.Description);

    public string? Course => Model.Course;
    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);

    // ---- Status ----
    public bool IsDone => Model.Status == TodoStatus.Done;
    public bool IsDoing => Model.Status == TodoStatus.Doing;

    public string StatusGlyph => Model.Status switch
    {
        TodoStatus.Doing => "◐",
        TodoStatus.Done => "●",
        _ => "○"
    };

    public string StatusTip => Model.Status switch
    {
        TodoStatus.Doing => "doing — click for done",
        TodoStatus.Done => "done — click for backlog",
        _ => "backlog — click for doing"
    };

    // ---- Priority ----
    public bool HasPriorityChip => Model.Priority != TodoPriority.Normal;
    public bool IsHighPriority => Model.Priority == TodoPriority.High;
    public string PriorityLabel => Model.Priority switch
    {
        TodoPriority.High => "high",
        TodoPriority.Low => "low",
        _ => string.Empty
    };

    // ---- Due ----
    public bool HasDue => Model.Due is not null;
    public string DueLabel => Model.Due is { } d ? $"{d:MMM d}".ToLowerInvariant() : string.Empty;
    public bool IsOverdue =>
        !IsDone && Model.Due is { } due && due < DateOnly.FromDateTime(DateTime.Now);

    // ---- Effort / progress ----
    public bool HasEstimate => Model.EstimatePomos > 0;
    public string EstimateLabel => $"×{Model.EstimatePomos}";

    public bool HasSubtasks => Subtasks.Count > 0;
    public string SubtaskProgress =>
        $"{Subtasks.Count(s => s.IsDone)}/{Subtasks.Count}";

    public string MetaLabel
    {
        get
        {
            var meta = $"{NumberLabel} · created {Model.CreatedAt:MMM d}".ToLowerInvariant();
            if (Model.CompletedAt is { } done)
                meta += $" · done {done:MMM d}".ToLowerInvariant();
            if (HasEstimate)
                meta += $" · est {Model.EstimatePomos} sessions";
            return meta;
        }
    }

    public TodoItemViewModel(TodoItem model, Action save, Action saveAndResort)
    {
        Model = model;
        _save = save;
        _saveAndResort = saveAndResort;

        foreach (var sub in model.Subtasks)
            Wrap(sub);
    }

    /// <summary>○ → ◐ → ● → ○. Done stamps CompletedAt; leaving done clears it.</summary>
    [RelayCommand]
    private void CycleStatus()
    {
        Model.Status = Model.Status switch
        {
            TodoStatus.Backlog => TodoStatus.Doing,
            TodoStatus.Doing => TodoStatus.Done,
            _ => TodoStatus.Backlog
        };

        Model.IsDone = Model.Status == TodoStatus.Done;
        Model.CompletedAt = Model.Status == TodoStatus.Done ? DateTimeOffset.Now : null;

        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsDoing));
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(StatusTip));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(MetaLabel));

        _saveAndResort();
    }

    [RelayCommand]
    private void AddSubtask()
    {
        var title = NewSubtaskTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        var sub = new Subtask { Title = title };
        Model.Subtasks.Add(sub);
        Wrap(sub);

        NewSubtaskTitle = string.Empty;
        NotifySubtasksChanged();
        _save();
    }

    [RelayCommand]
    private void RemoveSubtask(SubtaskViewModel? sub)
    {
        if (sub is null)
            return;

        Model.Subtasks.Remove(sub.Model);
        sub.Changed -= OnSubtaskChanged;
        Subtasks.Remove(sub);

        NotifySubtasksChanged();
        _save();
    }

    private void Wrap(Subtask sub)
    {
        var vm = new SubtaskViewModel(sub);
        vm.Changed += OnSubtaskChanged;
        Subtasks.Add(vm);
    }

    private void OnSubtaskChanged()
    {
        OnPropertyChanged(nameof(SubtaskProgress));
        _save();
    }

    private void NotifySubtasksChanged()
    {
        OnPropertyChanged(nameof(HasSubtasks));
        OnPropertyChanged(nameof(SubtaskProgress));
    }
}

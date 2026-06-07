using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The course-tagged task list. Holds an observable view of AppState.Tasks and
/// keeps the model list and disk in sync on every change.
/// </summary>
public partial class TasksViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    public ObservableCollection<TaskItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string _newTaskCourse = string.Empty;

    [ObservableProperty]
    private bool _hasTasks;

    public TasksViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;

        foreach (var model in _state.Tasks)
            Items.Add(Wrap(model));

        UpdateHasTasks();
    }

    [RelayCommand]
    private void AddTask()
    {
        var title = NewTaskTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        var course = string.IsNullOrWhiteSpace(NewTaskCourse) ? null : NewTaskCourse.Trim();
        var model = new TaskItem { Title = title, Course = course };

        _state.Tasks.Add(model);
        Items.Add(Wrap(model));

        NewTaskTitle = string.Empty;
        NewTaskCourse = string.Empty;

        UpdateHasTasks();
        _save();
    }

    [RelayCommand]
    private void RemoveTask(TaskItemViewModel? item)
    {
        if (item is null)
            return;

        _state.Tasks.Remove(item.Model);
        Items.Remove(item);

        UpdateHasTasks();
        _save();
    }

    private TaskItemViewModel Wrap(TaskItem model)
    {
        var vm = new TaskItemViewModel(model);
        vm.Changed += _save;
        return vm;
    }

    private void UpdateHasTasks() => HasTasks = Items.Count > 0;
}

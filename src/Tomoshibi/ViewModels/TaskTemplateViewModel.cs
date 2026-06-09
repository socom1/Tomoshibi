using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The "today" task list, edited as code: the user types a small template
/// in <see cref="Source"/>, and the parser produces <see cref="Tasks"/> on
/// every edit. Migrates the legacy <see cref="AppState.Tasks"/> list into
/// the template on first construction.
/// </summary>
public partial class TaskTemplateViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;
    private readonly Action _onActiveTaskChanged;

    /// <summary>The raw template text the user edits.</summary>
    [ObservableProperty]
    private string _source;

    /// <summary>Parsed view of the template. Rebuilt every time
    /// <see cref="Source"/> changes.</summary>
    public ObservableCollection<TaskBlock> Tasks { get; } = new();

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _doneCount;
    [ObservableProperty] private bool _hasTasks;

    /// <summary>The task whose study/short/long values the timer is currently
    /// reading from. Null means use the global settings.</summary>
    [ObservableProperty]
    private TaskBlock? _activeTask;

    public TaskTemplateViewModel(AppState state, Action save, Action onActiveTaskChanged)
    {
        _state = state;
        _save = save;
        _onActiveTaskChanged = onActiveTaskChanged;

        // Migrate any legacy TaskItems into the code template on first load.
        if (string.IsNullOrEmpty(_state.TaskTemplate) && _state.Tasks.Count > 0)
        {
            _state.TaskTemplate = TaskTemplateParser.FromTaskItems(_state.Tasks);
            _state.Tasks.Clear();
            _save();
        }

        _source = _state.TaskTemplate;
        Reparse();
    }

    partial void OnActiveTaskChanged(TaskBlock? value) => _onActiveTaskChanged?.Invoke();

    /// <summary>The course strings used in the current template, for the
    /// timetable's autocomplete to draw on.</summary>
    public IEnumerable<string> CoursesInUse
    {
        get
        {
            foreach (var t in Tasks)
                if (t.HasCourse)
                    yield return t.Course!;
        }
    }

    partial void OnSourceChanged(string value)
    {
        _state.TaskTemplate = value;
        _save();
        Reparse();
    }

    private void Reparse()
    {
        // Preserve the active selection across re-parses by title.
        var activeTitle = ActiveTask?.Title;

        var parsed = TaskTemplateParser.Parse(Source);

        Tasks.Clear();
        foreach (var t in parsed)
            Tasks.Add(t);

        TotalCount = Tasks.Count;
        DoneCount = 0;
        foreach (var t in Tasks)
            if (t.IsDone) DoneCount++;

        HasTasks = TotalCount > 0;

        // Re-attach the active selection to the freshly parsed instance, or
        // drop it if the user removed that task.
        ActiveTask = string.IsNullOrEmpty(activeTitle)
            ? null
            : Tasks.FirstOrDefault(t => t.Title == activeTitle);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The "today" task list, edited as code: the user types a small template
/// in <see cref="Source"/>, and the parser produces <see cref="Tasks"/> on
/// every edit.
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

    /// <summary>Power-user toggle — show the raw template source for hand-editing.
    /// Off by default; the list view is the everyday surface.</summary>
    [ObservableProperty] private bool _isEditorVisible;

    // ---- New-task modal state ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelAddTaskCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmAddTaskCommand))]
    private bool _isAddTaskModalOpen;

    /// <summary>True = simplified form, false = .code area.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCodeMode))]
    private bool _isSimpleMode = true;

    public bool IsCodeMode => !IsSimpleMode;

    // Simple-form fields
    [ObservableProperty] private string _newTaskTitle = string.Empty;
    [ObservableProperty] private decimal? _newTaskStudy;
    [ObservableProperty] private decimal? _newTaskShort;
    [ObservableProperty] private decimal? _newTaskLong;
    [ObservableProperty] private string _newTaskCourse = string.Empty;
    [ObservableProperty] private bool _newTaskDone;

    // Code-mode field
    [ObservableProperty] private string _newTaskCode = string.Empty;

    public TaskTemplateViewModel(AppState state, Action save, Action onActiveTaskChanged)
    {
        _state = state;
        _save = save;
        _onActiveTaskChanged = onActiveTaskChanged;

        // Legacy TaskItems are migrated into the template by StateMigrations
        // before any view model is built, so the source is ready to read.
        _source = _state.TaskTemplate;
        Reparse();
    }

    partial void OnActiveTaskChanged(TaskBlock? value) => _onActiveTaskChanged?.Invoke();

    [RelayCommand]
    private void ToggleEditor() => IsEditorVisible = !IsEditorVisible;

    /// <summary>Tick a task done (or back to open) straight from the list —
    /// edits the underlying source so the editor view stays in sync.</summary>
    [RelayCommand]
    private void ToggleDone(TaskBlock? task)
    {
        if (task is null)
            return;

        Source = TaskTemplateParser.ToggleDone(Source ?? string.Empty, task.Title);
    }

    [RelayCommand]
    private void OpenAddTask()
    {
        ResetForm();
        IsSimpleMode = true;
        IsAddTaskModalOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanCloseAddTask))]
    private void CancelAddTask()
    {
        IsAddTaskModalOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanCloseAddTask))]
    private void ConfirmAddTask()
    {
        var block = IsSimpleMode ? BuildBlockFromForm() : NewTaskCode?.Trim();
        if (string.IsNullOrWhiteSpace(block))
            return;

        var existing = (Source ?? string.Empty).TrimEnd();
        Source = string.IsNullOrEmpty(existing) ? block : $"{existing}\n\n{block}";

        ResetForm();
        IsAddTaskModalOpen = false;
    }

    [RelayCommand]
    private void ShowSimpleMode() => IsSimpleMode = true;

    [RelayCommand]
    private void ShowCodeMode() => IsSimpleMode = false;

    private bool CanCloseAddTask() => IsAddTaskModalOpen;

    /// <summary>Render the simple-form fields into a task block in the
    /// template grammar. Skips any field the user left blank.</summary>
    private string? BuildBlockFromForm()
    {
        var title = NewTaskTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var sb = new StringBuilder();
        sb.AppendLine($"// {title}");
        if (NewTaskStudy is { } s) sb.AppendLine($"study: {(int)s}");
        if (NewTaskShort is { } sh) sb.AppendLine($"short: {(int)sh}");
        if (NewTaskLong is { } l) sb.AppendLine($"long: {(int)l}");
        if (!string.IsNullOrWhiteSpace(NewTaskCourse)) sb.AppendLine($"course: {NewTaskCourse.Trim()}");
        if (NewTaskDone) sb.AppendLine("done");

        return sb.ToString().TrimEnd();
    }

    private void ResetForm()
    {
        NewTaskTitle = string.Empty;
        NewTaskStudy = null;
        NewTaskShort = null;
        NewTaskLong = null;
        NewTaskCourse = string.Empty;
        NewTaskDone = false;
        NewTaskCode = string.Empty;
    }

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

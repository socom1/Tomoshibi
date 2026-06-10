using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>Which slice of the backlog the list shows.</summary>
public enum TodoFilter
{
    All,
    Open,
    Done
}

/// <summary>
/// The todo backlog destination — a small ticket tracker for coursework:
/// numbered items with status (backlog/doing/done), priority, descriptions,
/// due dates, effort estimates and subtask checklists, searchable and
/// filterable. Items can be sent to the today task template with one click.
/// </summary>
public partial class TodoViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;
    private readonly Action<TodoItem> _sendToToday;

    private TodoItem? _editing;

    public ObservableCollection<TodoItemViewModel> Items { get; } = new();

    /// <summary>Courses seen across the app, for the form's autocomplete.</summary>
    public ObservableCollection<string> KnownCourses { get; } = new();

    /// <summary>Priority options for the form's picker.</summary>
    public IReadOnlyList<TodoPriority> Priorities { get; } = Enum.GetValues<TodoPriority>();

    [ObservableProperty] private bool _hasVisibleItems;
    [ObservableProperty] private int _openCount;
    [ObservableProperty] private int _doingCount;
    [ObservableProperty] private int _doneCount;

    // ---- Filtering ----
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllFilter))]
    [NotifyPropertyChangedFor(nameof(IsOpenFilter))]
    [NotifyPropertyChangedFor(nameof(IsDoneFilter))]
    private TodoFilter _filter = TodoFilter.Open;

    public bool IsAllFilter => Filter == TodoFilter.All;
    public bool IsOpenFilter => Filter == TodoFilter.Open;
    public bool IsDoneFilter => Filter == TodoFilter.Done;

    // ---- Add/edit modal ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelModalCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmModalCommand))]
    private bool _isModalOpen;

    [ObservableProperty] private string _modalTitle = "新しいやること · new todo";
    [ObservableProperty] private string _modalAction = "add";

    [ObservableProperty] private string _formTitle = string.Empty;
    [ObservableProperty] private string _formDescription = string.Empty;
    [ObservableProperty] private string _formCourse = string.Empty;
    [ObservableProperty] private DateTime? _formDue;
    [ObservableProperty] private TodoPriority _formPriority = TodoPriority.Normal;
    [ObservableProperty] private decimal? _formEstimate;

    public TodoViewModel(AppState state, Action save, Action<TodoItem> sendToToday)
    {
        _state = state;
        _save = save;
        _sendToToday = sendToToday;

        MigrateLegacyItems();
        Rebuild();
        RebuildKnownCourses();
    }

    /// <summary>State files from before the ticket upgrade: map the old
    /// IsDone flag onto Status and hand out numbers in creation order.</summary>
    private void MigrateLegacyItems()
    {
        var dirty = false;

        foreach (var todo in _state.Todos.OrderBy(t => t.CreatedAt))
        {
            if (todo.IsDone && todo.Status == TodoStatus.Backlog)
            {
                todo.Status = TodoStatus.Done;
                dirty = true;
            }

            if (todo.Number == 0)
            {
                todo.Number = _state.NextTodoNumber++;
                dirty = true;
            }
        }

        if (dirty)
            _save();
    }

    [RelayCommand]
    private void OpenAdd()
    {
        _editing = null;
        FormTitle = string.Empty;
        FormDescription = string.Empty;
        FormCourse = string.Empty;
        FormDue = null;
        FormPriority = TodoPriority.Normal;
        FormEstimate = null;

        ModalTitle = "新しいやること · new todo";
        ModalAction = "add";
        IsModalOpen = true;
    }

    public void BeginEdit(TodoItemViewModel row)
    {
        _editing = row.Model;
        FormTitle = row.Model.Title;
        FormDescription = row.Model.Description;
        FormCourse = row.Model.Course ?? string.Empty;
        FormDue = row.Model.Due is { } d ? d.ToDateTime(TimeOnly.MinValue) : null;
        FormPriority = row.Model.Priority;
        FormEstimate = row.Model.EstimatePomos > 0 ? row.Model.EstimatePomos : null;

        ModalTitle = $"編集 · edit {row.NumberLabel}";
        ModalAction = "save";
        IsModalOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanUseModal))]
    private void CancelModal() => IsModalOpen = false;

    [RelayCommand(CanExecute = nameof(CanUseModal))]
    private void ConfirmModal()
    {
        var title = FormTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        var target = _editing ?? new TodoItem { Number = _state.NextTodoNumber++ };

        target.Title = title;
        target.Description = FormDescription?.Trim() ?? string.Empty;
        target.Course = string.IsNullOrWhiteSpace(FormCourse) ? null : FormCourse.Trim();
        target.Due = FormDue is { } d ? DateOnly.FromDateTime(d) : null;
        target.Priority = FormPriority;
        target.EstimatePomos = FormEstimate is { } e ? (int)e : 0;

        if (_editing is null)
            _state.Todos.Add(target);

        _editing = null;
        IsModalOpen = false;

        Rebuild();
        RebuildKnownCourses();
        _save();
    }

    private bool CanUseModal() => IsModalOpen;

    [RelayCommand]
    private void Remove(TodoItemViewModel? item)
    {
        if (item is null)
            return;

        if (item.Model == _editing)
        {
            _editing = null;
            IsModalOpen = false;
        }

        _state.Todos.Remove(item.Model);
        Rebuild();
        RebuildKnownCourses();
        _save();
    }

    /// <summary>Copy the todo into today's task template. The backlog entry
    /// stays put — planning a thing isn't the same as finishing it.</summary>
    [RelayCommand]
    private void SendToToday(TodoItemViewModel? item)
    {
        if (item is null)
            return;

        _sendToToday(item.Model);
    }

    [RelayCommand] private void ShowAll() => Filter = TodoFilter.All;
    [RelayCommand] private void ShowOpen() => Filter = TodoFilter.Open;
    [RelayCommand] private void ShowDone() => Filter = TodoFilter.Done;

    partial void OnFilterChanged(TodoFilter value) => Rebuild();
    partial void OnSearchTextChanged(string value) => Rebuild();

    /// <summary>Re-filter, re-sort and re-wrap. Doing first, then backlog,
    /// then done; high priority first within a status, then due date (none
    /// last), then ticket number. Expansion survives rebuilds by id.</summary>
    private void Rebuild()
    {
        var expanded = Items.Where(r => r.IsExpanded).Select(r => r.Model.Id).ToHashSet();
        Items.Clear();

        var query = _state.Todos.AsEnumerable();

        query = Filter switch
        {
            TodoFilter.Open => query.Where(t => t.Status != TodoStatus.Done),
            TodoFilter.Done => query.Where(t => t.Status == TodoStatus.Done),
            _ => query
        };

        var search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t =>
                t.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (t.Course?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var sorted = query
            .OrderBy(t => t.Status switch
            {
                TodoStatus.Doing => 0,
                TodoStatus.Backlog => 1,
                _ => 2
            })
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.Due ?? DateOnly.MaxValue)
            .ThenBy(t => t.Number);

        foreach (var model in sorted)
        {
            var row = new TodoItemViewModel(model, _save, OnRowNeedsResort)
            {
                IsExpanded = expanded.Contains(model.Id)
            };
            Items.Add(row);
        }

        HasVisibleItems = Items.Count > 0;
        OpenCount = _state.Todos.Count(t => t.Status == TodoStatus.Backlog);
        DoingCount = _state.Todos.Count(t => t.Status == TodoStatus.Doing);
        DoneCount = _state.Todos.Count(t => t.Status == TodoStatus.Done);
    }

    private void OnRowNeedsResort()
    {
        _save();
        Rebuild();
    }

    private void RebuildKnownCourses()
    {
        var courses = _state.Todos.Select(t => t.Course)
            .Concat(_state.ClassSlots.Select(s => s.Course))
            .Concat(_state.Deadlines.Select(d => d.Course))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        KnownCourses.Clear();
        foreach (var c in courses)
            KnownCourses.Add(c);
    }
}

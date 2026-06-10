using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The todo backlog destination — longer-horizon coursework that hasn't
/// earned a spot in today's plan yet. Items can be sent to the today task
/// template with one click, which is the whole point of keeping the backlog
/// inside the app instead of a notes file.
/// </summary>
public partial class TodoViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;
    private readonly Action<TodoItem> _sendToToday;

    public ObservableCollection<TodoItemViewModel> Items { get; } = new();

    /// <summary>Courses seen across the app, for the form's autocomplete.</summary>
    public ObservableCollection<string> KnownCourses { get; } = new();

    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private int _openCount;

    // ---- New todo form ----
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newCourse = string.Empty;
    [ObservableProperty] private DateTime? _newDue;

    public TodoViewModel(AppState state, Action save, Action<TodoItem> sendToToday)
    {
        _state = state;
        _save = save;
        _sendToToday = sendToToday;

        Rebuild();
        RebuildKnownCourses();
    }

    [RelayCommand]
    private void Add()
    {
        var title = NewTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        _state.Todos.Add(new TodoItem
        {
            Title = title,
            Course = string.IsNullOrWhiteSpace(NewCourse) ? null : NewCourse.Trim(),
            Due = NewDue is { } d ? DateOnly.FromDateTime(d) : null
        });

        NewTitle = string.Empty;
        NewCourse = string.Empty;
        NewDue = null;

        Rebuild();
        RebuildKnownCourses();
        _save();
    }

    [RelayCommand]
    private void Remove(TodoItemViewModel? item)
    {
        if (item is null)
            return;

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

    /// <summary>Resort and rewrap: open before done, then due date (none
    /// last), then oldest first. Lists are tiny — rebuilding wholesale is
    /// simpler than incremental bookkeeping.</summary>
    private void Rebuild()
    {
        foreach (var row in Items)
            row.Changed -= OnRowChanged;
        Items.Clear();

        var sorted = _state.Todos
            .OrderBy(t => t.IsDone)
            .ThenBy(t => t.Due ?? DateOnly.MaxValue)
            .ThenBy(t => t.CreatedAt);

        foreach (var model in sorted)
        {
            var row = new TodoItemViewModel(model);
            row.Changed += OnRowChanged;
            Items.Add(row);
        }

        HasItems = Items.Count > 0;
        OpenCount = _state.Todos.Count(t => !t.IsDone);
    }

    private void OnRowChanged()
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

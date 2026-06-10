using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The timetable side panel: a recurring weekly class schedule and a list of
/// upcoming deadlines, both editable by hand. Wraps the same model lists that
/// live on <see cref="AppState"/> so saves go through one path.
/// </summary>
public partial class TimetableViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    public ObservableCollection<ClassSlotItemViewModel> Slots { get; } = new();
    public ObservableCollection<DeadlineItemViewModel> Deadlines { get; } = new();

    /// <summary>Course strings already seen across tasks, slots and deadlines —
    /// the source the entry forms autocomplete against.</summary>
    public ObservableCollection<string> KnownCourses { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClassesGridShown))]
    [NotifyPropertyChangedFor(nameof(IsClassesListShown))]
    private bool _hasSlots;

    [ObservableProperty] private bool _hasDeadlines;

    /// <summary>One-line result of the last .ics import, shown in the header
    /// area ("imported 5 classes · 2 deadlines · 1 skipped").</summary>
    [ObservableProperty] private string _importSummary = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClassesGridView))]
    [NotifyPropertyChangedFor(nameof(IsClassesListView))]
    [NotifyPropertyChangedFor(nameof(IsClassesGridShown))]
    [NotifyPropertyChangedFor(nameof(IsClassesListShown))]
    private ClassesView _classesView;

    /// <summary>Toggle-button highlight state — true when that view is selected.</summary>
    public bool IsClassesGridView => ClassesView == ClassesView.Grid;
    public bool IsClassesListView => ClassesView == ClassesView.List;

    /// <summary>View-content visibility — true only when the view is selected
    /// AND there's something to render.</summary>
    public bool IsClassesGridShown => HasSlots && IsClassesGridView;
    public bool IsClassesListShown => HasSlots && IsClassesListView;

    // ---- New deadline form (doubles as the edit form) ----
    [ObservableProperty] private DateTime? _newDeadlineDate = DateTime.Now;
    [ObservableProperty] private string _newDeadlineTitle = string.Empty;
    [ObservableProperty] private string _newDeadlineCourse = string.Empty;
    [ObservableProperty] private string _deadlineFormLabel = "add";
    private Deadline? _editingDeadline;

    // ---- New class slot form (doubles as the edit form) ----
    [ObservableProperty] private WeekDay _newSlotDay = WeekDay.Mon;
    [ObservableProperty] private TimeSpan _newSlotStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan _newSlotEnd = new(10, 0, 0);
    [ObservableProperty] private string _newSlotTitle = string.Empty;
    [ObservableProperty] private string _newSlotCourse = string.Empty;
    [ObservableProperty] private string _slotFormLabel = "add";
    private ClassSlot? _editingSlot;

    /// <summary>The seven weekdays as picker options for the new-slot form.</summary>
    public IReadOnlyList<WeekDay> WeekDays { get; } = Enum.GetValues<WeekDay>();

    public TimetableViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;
        _classesView = _state.ClassesView;

        foreach (var slot in _state.ClassSlots.OrderBy(s => s.Day).ThenBy(s => s.Start))
            Slots.Add(new ClassSlotItemViewModel(slot));

        foreach (var d in _state.Deadlines.OrderBy(d => d.Date))
            Deadlines.Add(new DeadlineItemViewModel(d));

        RebuildKnownCourses();
        UpdateFlags();
    }

    [RelayCommand]
    private void ShowGridClasses() => ClassesView = ClassesView.Grid;

    [RelayCommand]
    private void ShowListClasses() => ClassesView = ClassesView.List;

    partial void OnClassesViewChanged(ClassesView value)
    {
        _state.ClassesView = value;
        _save();
    }

    [RelayCommand]
    private void AddDeadline()
    {
        var title = NewDeadlineTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || NewDeadlineDate is null)
            return;

        var course = string.IsNullOrWhiteSpace(NewDeadlineCourse) ? null : NewDeadlineCourse.Trim();

        if (_editingDeadline is { } editing)
        {
            // Saving an in-place edit: mutate the model, rewrap its row so the
            // computed labels refresh, and re-sort.
            editing.Date = DateOnly.FromDateTime(NewDeadlineDate.Value);
            editing.Title = title;
            editing.Course = course;

            var row = Deadlines.FirstOrDefault(r => r.Model == editing);
            if (row is not null)
                Deadlines.Remove(row);
            InsertSorted(Deadlines, new DeadlineItemViewModel(editing), (a, b) => a.Model.Date.CompareTo(b.Model.Date));
        }
        else
        {
            var model = new Deadline
            {
                Date = DateOnly.FromDateTime(NewDeadlineDate.Value),
                Title = title,
                Course = course
            };

            _state.Deadlines.Add(model);
            InsertSorted(Deadlines, new DeadlineItemViewModel(model), (a, b) => a.Model.Date.CompareTo(b.Model.Date));
        }

        CancelEdits();
        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    [RelayCommand]
    private void RemoveDeadline(DeadlineItemViewModel? item)
    {
        if (item is null)
            return;

        if (item.Model == _editingDeadline)
            CancelEdits();

        _state.Deadlines.Remove(item.Model);
        Deadlines.Remove(item);

        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    [RelayCommand]
    private void AddSlot()
    {
        var title = NewSlotTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        if (NewSlotEnd <= NewSlotStart)
            return;

        var course = string.IsNullOrWhiteSpace(NewSlotCourse) ? null : NewSlotCourse.Trim();

        if (_editingSlot is { } editing)
        {
            editing.Day = NewSlotDay;
            editing.Start = TimeOnly.FromTimeSpan(NewSlotStart);
            editing.End = TimeOnly.FromTimeSpan(NewSlotEnd);
            editing.Title = title;
            editing.Course = course;

            var row = Slots.FirstOrDefault(r => r.Model == editing);
            if (row is not null)
                Slots.Remove(row);
            InsertSorted(Slots, new ClassSlotItemViewModel(editing), CompareSlots);
        }
        else
        {
            var model = new ClassSlot
            {
                Day = NewSlotDay,
                Start = TimeOnly.FromTimeSpan(NewSlotStart),
                End = TimeOnly.FromTimeSpan(NewSlotEnd),
                Title = title,
                Course = course
            };

            _state.ClassSlots.Add(model);
            InsertSorted(Slots, new ClassSlotItemViewModel(model), CompareSlots);
        }

        CancelEdits();
        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    private static int CompareSlots(ClassSlotItemViewModel a, ClassSlotItemViewModel b) =>
        a.Model.Day != b.Model.Day
            ? a.Model.Day.CompareTo(b.Model.Day)
            : a.Model.Start.CompareTo(b.Model.Start);

    /// <summary>Load a slot into the entry form for in-place editing; the
    /// form's add button becomes "save" until it lands or is cancelled.</summary>
    public void BeginEditSlot(ClassSlotItemViewModel item)
    {
        _editingSlot = item.Model;
        NewSlotDay = item.Model.Day;
        NewSlotStart = item.Model.Start.ToTimeSpan();
        NewSlotEnd = item.Model.End.ToTimeSpan();
        NewSlotTitle = item.Model.Title;
        NewSlotCourse = item.Model.Course ?? string.Empty;
        SlotFormLabel = "save";
    }

    public void BeginEditDeadline(DeadlineItemViewModel item)
    {
        _editingDeadline = item.Model;
        NewDeadlineDate = item.Model.Date.ToDateTime(TimeOnly.MinValue);
        NewDeadlineTitle = item.Model.Title;
        NewDeadlineCourse = item.Model.Course ?? string.Empty;
        DeadlineFormLabel = "save";
    }

    /// <summary>Merge a parsed .ics file into the timetable. Exact duplicates
    /// (same day/time/title for slots, same date/title for deadlines) are
    /// skipped so re-importing the same file is harmless.</summary>
    public void ImportIcs(string text)
    {
        var parsed = IcsImporter.Parse(text);
        var added = 0;
        var addedDeadlines = 0;

        foreach (var slot in parsed.Slots)
        {
            var dupe = _state.ClassSlots.Any(s =>
                s.Day == slot.Day && s.Start == slot.Start &&
                s.End == slot.End && s.Title == slot.Title);
            if (dupe)
                continue;

            _state.ClassSlots.Add(slot);
            InsertSorted(Slots, new ClassSlotItemViewModel(slot), CompareSlots);
            added++;
        }

        foreach (var deadline in parsed.Deadlines)
        {
            var dupe = _state.Deadlines.Any(d =>
                d.Date == deadline.Date && d.Title == deadline.Title);
            if (dupe)
                continue;

            _state.Deadlines.Add(deadline);
            InsertSorted(Deadlines, new DeadlineItemViewModel(deadline),
                (a, b) => a.Model.Date.CompareTo(b.Model.Date));
            addedDeadlines++;
        }

        ImportSummary = $"imported {added} classes · {addedDeadlines} deadlines" +
                        (parsed.Skipped > 0 ? $" · {parsed.Skipped} skipped" : string.Empty);

        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    /// <summary>Drop any in-flight edit and reset both entry forms — called
    /// when a + button opens a fresh form, and after every save.</summary>
    public void CancelEdits()
    {
        _editingSlot = null;
        _editingDeadline = null;
        SlotFormLabel = "add";
        DeadlineFormLabel = "add";
        NewSlotTitle = string.Empty;
        NewSlotCourse = string.Empty;
        NewDeadlineTitle = string.Empty;
        NewDeadlineCourse = string.Empty;
    }

    [RelayCommand]
    private void RemoveSlot(ClassSlotItemViewModel? item)
    {
        if (item is null)
            return;

        if (item.Model == _editingSlot)
            CancelEdits();

        _state.ClassSlots.Remove(item.Model);
        Slots.Remove(item);

        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    /// <summary>Rebuild the autocomplete source from every place a course can
    /// have been entered — class slots, deadlines, and the parsed today
    /// task template.</summary>
    private void RebuildKnownCourses()
    {
        var fromTemplate = TaskTemplateParser.Parse(_state.TaskTemplate)
            .Select(t => t.Course);

        var courses = fromTemplate
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

    private void UpdateFlags()
    {
        HasSlots = Slots.Count > 0;
        HasDeadlines = Deadlines.Count > 0;
    }

    private static void InsertSorted<T>(ObservableCollection<T> list, T item, Comparison<T> compare)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (compare(item, list[i]) < 0)
            {
                list.Insert(i, item);
                return;
            }
        }
        list.Add(item);
    }
}

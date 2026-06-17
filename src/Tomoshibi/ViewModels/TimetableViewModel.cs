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
/// The timetable destination: a recurring weekly class schedule, plus a
/// deadlines card that is a date-ordered window onto the todo backlog —
/// every "deadline" is an open ticket with a due date. Adding one here
/// creates a ticket; the full editing surface lives on the todo page.
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

    // ---- New deadline form (creates a todo ticket with a due date) ----
    [ObservableProperty] private DateTime? _newDeadlineDate = DateTime.Now;
    [ObservableProperty] private string _newDeadlineTitle = string.Empty;
    [ObservableProperty] private string _newDeadlineCourse = string.Empty;

    // ---- New class slot form (doubles as the edit form) ----
    [ObservableProperty] private WeekDay _newSlotDay = WeekDay.Mon;
    [ObservableProperty] private TimeSpan _newSlotStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan _newSlotEnd = new(10, 0, 0);

    // Hour/minute spinners in the modal bind here; each rebuilds the TimeSpan
    // so the rest of the form keeps reading NewSlotStart / NewSlotEnd.
    public decimal StartHour
    {
        get => NewSlotStart.Hours;
        set => NewSlotStart = new TimeSpan((int)Math.Clamp(value, 0, 23), NewSlotStart.Minutes, 0);
    }
    public decimal StartMinute
    {
        get => NewSlotStart.Minutes;
        set => NewSlotStart = new TimeSpan(NewSlotStart.Hours, (int)Math.Clamp(value, 0, 59), 0);
    }
    public decimal EndHour
    {
        get => NewSlotEnd.Hours;
        set => NewSlotEnd = new TimeSpan((int)Math.Clamp(value, 0, 23), NewSlotEnd.Minutes, 0);
    }
    public decimal EndMinute
    {
        get => NewSlotEnd.Minutes;
        set => NewSlotEnd = new TimeSpan(NewSlotEnd.Hours, (int)Math.Clamp(value, 0, 59), 0);
    }

    partial void OnNewSlotStartChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(StartHour));
        OnPropertyChanged(nameof(StartMinute));
    }

    partial void OnNewSlotEndChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(EndHour));
        OnPropertyChanged(nameof(EndMinute));
    }
    [ObservableProperty] private string _newSlotTitle = string.Empty;
    [ObservableProperty] private string _newSlotCourse = string.Empty;
    [ObservableProperty] private string _slotFormLabel = "add";
    [ObservableProperty] private string _slotModalTitle = "新しい授業 · new class";
    [ObservableProperty] private bool _isSlotModalOpen;
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

        RefreshDeadlines();
        RebuildKnownCourses();
        UpdateFlags();
    }

    /// <summary>Upcoming dated exams from the subjects page, read-only.</summary>
    public ObservableCollection<string> UpcomingExams { get; } = new();

    [ObservableProperty] private bool _hasUpcomingExams;

    /// <summary>Re-window the deadlines card onto the backlog: open tickets
    /// with a due date, soonest first — plus dated, ungraded assessments
    /// from the subjects page as a read-only exams list. Called on
    /// construction, after local changes, and when the user navigates here.</summary>
    public void RefreshDeadlines()
    {
        Deadlines.Clear();
        foreach (var ticket in _state.Todos
                     .Where(t => t.Due is not null && t.Status != TodoStatus.Done)
                     .OrderBy(t => t.Due))
        {
            Deadlines.Add(new DeadlineItemViewModel(ticket));
        }

        HasDeadlines = Deadlines.Count > 0;

        var today = DateOnly.FromDateTime(DateTime.Now);
        UpcomingExams.Clear();
        foreach (var (subject, exam) in _state.Subjects
                     .SelectMany(s => s.Assessments
                         .Where(a => a.Grade is null && a.Date is { } d &&
                                     d >= today && d <= today.AddDays(30))
                         .Select(a => (s, a)))
                     .OrderBy(x => x.a.Date))
        {
            var date = $"{exam.Date:ddd MMM d}".ToLowerInvariant();
            UpcomingExams.Add($"{date} · {exam.Title} — {subject.Code ?? subject.Name}");
        }

        HasUpcomingExams = UpcomingExams.Count > 0;
    }

    /// <summary>Open the slot modal on a fresh form (the + button).</summary>
    [RelayCommand]
    private void OpenAddSlot()
    {
        CancelEdits();
        SlotModalTitle = "新しい授業 · new class";
        IsSlotModalOpen = true;
    }

    /// <summary>Close the slot modal and drop any in-flight edit.</summary>
    [RelayCommand]
    private void CancelSlotModal()
    {
        if (!IsSlotModalOpen)
            return;
        IsSlotModalOpen = false;
        CancelEdits();
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

    /// <summary>Adding a deadline creates an open ticket on the todo backlog
    /// with the due date set — one concept, two views.</summary>
    [RelayCommand]
    private void AddDeadline()
    {
        var title = NewDeadlineTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || NewDeadlineDate is null)
            return;

        _state.Todos.Add(new TodoItem
        {
            Number = _state.NextTodoNumber++,
            Title = title,
            Course = string.IsNullOrWhiteSpace(NewDeadlineCourse) ? null : NewDeadlineCourse.Trim(),
            Due = DateOnly.FromDateTime(NewDeadlineDate.Value)
        });

        CancelEdits();
        RefreshDeadlines();
        RebuildKnownCourses();
        _save();
    }

    /// <summary>Removing a deadline deletes the underlying ticket.</summary>
    [RelayCommand]
    private void RemoveDeadline(DeadlineItemViewModel? item)
    {
        if (item is null)
            return;

        _state.Todos.Remove(item.Model);
        RefreshDeadlines();
        RebuildKnownCourses();
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
        IsSlotModalOpen = false;
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
        SlotModalTitle = "編集 · edit class";
        IsSlotModalOpen = true;
    }

    /// <summary>Merge a parsed .ics file into the timetable. Weekly events
    /// become class slots; one-offs become open tickets with due dates.
    /// Exact duplicates are skipped so re-importing the same file is harmless.</summary>
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
            var dupe = _state.Todos.Any(t =>
                t.Due == deadline.Date && t.Title == deadline.Title);
            if (dupe)
                continue;

            _state.Todos.Add(new TodoItem
            {
                Number = _state.NextTodoNumber++,
                Title = deadline.Title,
                Course = deadline.Course,
                Due = deadline.Date
            });
            addedDeadlines++;
        }

        ImportSummary = $"imported {added} classes · {addedDeadlines} deadlines" +
                        (parsed.Skipped > 0 ? $" · {parsed.Skipped} skipped" : string.Empty);

        RefreshDeadlines();
        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    /// <summary>Drop any in-flight edit and reset both entry forms — called
    /// when a + button opens a fresh form, and after every save.</summary>
    public void CancelEdits()
    {
        _editingSlot = null;
        SlotFormLabel = "add";
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
    /// have been entered — class slots, the todo backlog, and the parsed
    /// today task template.</summary>
    private void RebuildKnownCourses()
    {
        var fromTemplate = TaskTemplateParser.Parse(_state.TaskTemplate)
            .Select(t => t.Course);

        var courses = fromTemplate
            .Concat(_state.ClassSlots.Select(s => s.Course))
            .Concat(_state.Todos.Select(t => t.Course))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        KnownCourses.Clear();
        foreach (var c in courses)
            KnownCourses.Add(c);
    }

    /// <summary>"5 classes" / "1 class" for the schedule-file header.</summary>
    public string SlotsLabel => Slots.Count == 1 ? "1 class" : $"{Slots.Count} classes";

    private void UpdateFlags()
    {
        HasSlots = Slots.Count > 0;
        OnPropertyChanged(nameof(SlotsLabel));
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

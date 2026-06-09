using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

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

    // ---- New deadline form ----
    [ObservableProperty] private DateTime? _newDeadlineDate = DateTime.Now;
    [ObservableProperty] private string _newDeadlineTitle = string.Empty;
    [ObservableProperty] private string _newDeadlineCourse = string.Empty;

    // ---- New class slot form ----
    [ObservableProperty] private WeekDay _newSlotDay = WeekDay.Mon;
    [ObservableProperty] private TimeSpan _newSlotStart = new(9, 0, 0);
    [ObservableProperty] private TimeSpan _newSlotEnd = new(10, 0, 0);
    [ObservableProperty] private string _newSlotTitle = string.Empty;
    [ObservableProperty] private string _newSlotCourse = string.Empty;

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

        var model = new Deadline
        {
            Date = DateOnly.FromDateTime(NewDeadlineDate.Value),
            Title = title,
            Course = string.IsNullOrWhiteSpace(NewDeadlineCourse) ? null : NewDeadlineCourse.Trim()
        };

        _state.Deadlines.Add(model);
        InsertSorted(Deadlines, new DeadlineItemViewModel(model), (a, b) => a.Model.Date.CompareTo(b.Model.Date));

        NewDeadlineTitle = string.Empty;
        NewDeadlineCourse = string.Empty;

        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    [RelayCommand]
    private void RemoveDeadline(DeadlineItemViewModel? item)
    {
        if (item is null)
            return;

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

        var model = new ClassSlot
        {
            Day = NewSlotDay,
            Start = TimeOnly.FromTimeSpan(NewSlotStart),
            End = TimeOnly.FromTimeSpan(NewSlotEnd),
            Title = title,
            Course = string.IsNullOrWhiteSpace(NewSlotCourse) ? null : NewSlotCourse.Trim()
        };

        _state.ClassSlots.Add(model);
        InsertSorted(Slots, new ClassSlotItemViewModel(model),
            (a, b) => a.Model.Day != b.Model.Day
                ? a.Model.Day.CompareTo(b.Model.Day)
                : a.Model.Start.CompareTo(b.Model.Start));

        NewSlotTitle = string.Empty;
        NewSlotCourse = string.Empty;

        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    [RelayCommand]
    private void RemoveSlot(ClassSlotItemViewModel? item)
    {
        if (item is null)
            return;

        _state.ClassSlots.Remove(item.Model);
        Slots.Remove(item);

        RebuildKnownCourses();
        UpdateFlags();
        _save();
    }

    /// <summary>Rebuild the autocomplete source from every place a course can
    /// have been entered.</summary>
    private void RebuildKnownCourses()
    {
        var courses = _state.Tasks.Select(t => t.Course)
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

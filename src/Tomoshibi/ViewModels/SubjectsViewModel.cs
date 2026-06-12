using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The 科目 · subjects destination: every course with its weighted
/// assessments, and the credit-weighted GPA over whatever's graded so far.
/// </summary>
public partial class SubjectsViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    private Subject? _editing;

    public ObservableCollection<SubjectViewModel> Items { get; } = new();

    /// <summary>Course codes seen across the app, for the form autocomplete.</summary>
    public ObservableCollection<string> KnownCourses { get; } = new();

    [ObservableProperty] private bool _hasSubjects;
    [ObservableProperty] private string _gpaLabel = "no grades yet";
    [ObservableProperty] private string _gpaCaption = string.Empty;

    // ---- Detail page (master → detail within this destination) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen))]
    [NotifyCanExecuteChangedFor(nameof(CloseDetailCommand))]
    private SubjectViewModel? _selectedSubject;

    public bool IsDetailOpen => SelectedSubject is not null;

    /// <summary>Cross-app context for the open subject, linked by course
    /// code: open tickets, sessions logged against them, the next class.</summary>
    [ObservableProperty] private string _linkedTicketsLabel = string.Empty;
    [ObservableProperty] private string _linkedClassLabel = string.Empty;
    [ObservableProperty] private bool _hasLinkedInfo;
    [ObservableProperty] private bool _hasNoCode;

    // ---- Add/edit subject modal ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelModalCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmModalCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseDetailCommand))]
    private bool _isModalOpen;

    [ObservableProperty] private string _modalTitle = "新しい科目 · new subject";
    [ObservableProperty] private string _modalAction = "add";

    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private decimal? _formCredits = 1;

    public SubjectsViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;

        foreach (var subject in _state.Subjects.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            Items.Add(new SubjectViewModel(subject, OnSubjectChanged));

        RebuildKnownCourses();
        RecomputeGpa();
    }

    /// <summary>Called on navigation here — picks up course codes added on
    /// other pages for the autocomplete, and re-links the open detail.</summary>
    public void Refresh()
    {
        RebuildKnownCourses();
        if (IsDetailOpen)
            RefreshLinkedInfo();
    }

    /// <summary>Open the full page for one subject.</summary>
    public void OpenDetail(SubjectViewModel row)
    {
        SelectedSubject = row;
        RefreshLinkedInfo();
    }

    [RelayCommand(CanExecute = nameof(CanCloseDetail))]
    private void CloseDetail() => SelectedSubject = null;

    // Gated off while the modal is up so Escape closes the modal first.
    private bool CanCloseDetail() => IsDetailOpen && !IsModalOpen;

    /// <summary>Everything else in the app that carries this subject's
    /// course code: tickets (and the sessions logged on them) and the next
    /// class slot in the coming week.</summary>
    private void RefreshLinkedInfo()
    {
        var code = SelectedSubject?.Code;
        HasNoCode = string.IsNullOrWhiteSpace(code);
        if (HasNoCode)
        {
            HasLinkedInfo = false;
            LinkedTicketsLabel = string.Empty;
            LinkedClassLabel = string.Empty;
            return;
        }

        var matches = (string? c) => string.Equals(c, code, StringComparison.OrdinalIgnoreCase);

        var tickets = _state.Todos.Where(t => matches(t.Course)).ToList();
        var open = tickets.Count(t => t.Status != TodoStatus.Done);
        var sessions = tickets.Sum(t => t.SessionsSpent);
        LinkedTicketsLabel = $"{open} open tickets · {sessions} focus sessions logged";

        // The next class within a week, scanning forward from now.
        var now = DateTime.Now;
        LinkedClassLabel = string.Empty;
        for (var offset = 0; offset < 7 && LinkedClassLabel.Length == 0; offset++)
        {
            var day = (WeekDay)(((int)now.DayOfWeek + 6 + offset) % 7);
            var slot = _state.ClassSlots
                .Where(s => matches(s.Course) && s.Day == day)
                .Where(s => offset > 0 || s.Start > TimeOnly.FromDateTime(now))
                .OrderBy(s => s.Start)
                .FirstOrDefault();

            if (slot is not null)
            {
                var dayWord = offset == 0 ? "today" : $"{slot.Day}".ToLowerInvariant();
                LinkedClassLabel = $"next class · {dayWord} {slot.Start:HH\\:mm} — {slot.Title}";
            }
        }

        HasLinkedInfo = true;
    }

    [RelayCommand]
    private void OpenAdd()
    {
        _editing = null;
        FormName = string.Empty;
        FormCode = string.Empty;
        FormCredits = 1;
        ModalTitle = "新しい科目 · new subject";
        ModalAction = "add";
        IsModalOpen = true;
    }

    public void BeginEdit(SubjectViewModel row)
    {
        _editing = row.Model;
        FormName = row.Model.Name;
        FormCode = row.Model.Code ?? string.Empty;
        FormCredits = (decimal)row.Model.Credits;
        ModalTitle = "編集 · edit subject";
        ModalAction = "save";
        IsModalOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanUseModal))]
    private void CancelModal() => IsModalOpen = false;

    [RelayCommand(CanExecute = nameof(CanUseModal))]
    private void ConfirmModal()
    {
        var name = FormName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var code = string.IsNullOrWhiteSpace(FormCode) ? null : FormCode.Trim();
        var credits = FormCredits is { } c && c > 0 ? (double)c : 1;

        if (_editing is { } subject)
        {
            subject.Name = name;
            subject.Code = code;
            subject.Credits = credits;
            Items.FirstOrDefault(r => r.Model == subject)?.NotifyModelEdited();
        }
        else
        {
            var model = new Subject { Name = name, Code = code, Credits = credits };
            _state.Subjects.Add(model);

            var row = new SubjectViewModel(model, OnSubjectChanged);
            var index = Items.TakeWhile(r =>
                string.Compare(r.Name, name, StringComparison.OrdinalIgnoreCase) < 0).Count();
            Items.Insert(index, row);
        }

        _editing = null;
        IsModalOpen = false;

        HasSubjects = Items.Count > 0;
        RebuildKnownCourses();
        RecomputeGpa();
        if (IsDetailOpen)
            RefreshLinkedInfo();
        _save();
    }

    private bool CanUseModal() => IsModalOpen;

    [RelayCommand]
    private void Remove(SubjectViewModel? row)
    {
        if (row is null)
            return;

        if (row.Model == _editing)
        {
            _editing = null;
            IsModalOpen = false;
        }

        if (row == SelectedSubject)
            SelectedSubject = null;

        _state.Subjects.Remove(row.Model);
        Items.Remove(row);

        HasSubjects = Items.Count > 0;
        RecomputeGpa();
        _save();
    }

    private void OnSubjectChanged()
    {
        RecomputeGpa();
        _save();
    }

    /// <summary>Credit-weighted GPA over subjects that have any grade.</summary>
    private void RecomputeGpa()
    {
        HasSubjects = Items.Count > 0;

        var graded = Items
            .Where(s => s.CurrentPercent is not null)
            .Select(s => (Points: GradeScale.ToPoints(s.CurrentPercent!.Value), s.Model.Credits))
            .ToList();

        if (graded.Count == 0)
        {
            GpaLabel = "no grades yet";
            GpaCaption = HasSubjects
                ? "grade an assessment and the gpa appears"
                : string.Empty;
            return;
        }

        var totalCredits = graded.Sum(g => g.Credits);
        var gpa = graded.Sum(g => g.Points * g.Credits) / totalCredits;

        GpaLabel = $"{gpa:0.00} gpa";
        GpaCaption = $"{graded.Count} of {Items.Count} subjects graded · " +
                     $"{totalCredits:0.#} credits · 4.0 scale";
    }

    private void RebuildKnownCourses()
    {
        var courses = _state.Subjects.Select(s => s.Code)
            .Concat(_state.Todos.Select(t => t.Course))
            .Concat(_state.ClassSlots.Select(s => s.Course))
            .Concat(TaskTemplateParser.Parse(_state.TaskTemplate).Select(t => t.Course))
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

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

/// <summary>A grading-scale choice for the picker.</summary>
public record ScaleOption(GradeScaleKind Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The 科目 · subjects destination: subjects grouped by term, weighted
/// assessments with drop rules and targets, a what-if simulator, and the
/// overall figure in whichever grading system the user's university speaks —
/// US GPA, UK honours classification, ECTS or plain percentages — plus a
/// year-weighted degree projection.
/// </summary>
public partial class SubjectsViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    private Subject? _editing;

    public ObservableCollection<SubjectViewModel> Items { get; } = new();
    public ObservableCollection<TermGroupViewModel> Groups { get; } = new();
    public ObservableCollection<YearWeightViewModel> YearWeights { get; } = new();

    /// <summary>Course codes seen across the app, for the form autocomplete.</summary>
    public ObservableCollection<string> KnownCourses { get; } = new();

    public IReadOnlyList<ScaleOption> ScaleOptions { get; } = new[]
    {
        new ScaleOption(GradeScaleKind.UsGpa, "us 4.0"),
        new ScaleOption(GradeScaleKind.UkHonours, "uk honours"),
        new ScaleOption(GradeScaleKind.Ects, "ects"),
        new ScaleOption(GradeScaleKind.Percentage, "percentage"),
    };

    [ObservableProperty]
    private ScaleOption _selectedScale;

    [ObservableProperty] private bool _hasSubjects;
    [ObservableProperty] private string _gpaLabel = "no grades yet";
    [ObservableProperty] private string _gpaCaption = string.Empty;

    // ---- Degree projection (year-weighted) ----
    [ObservableProperty] private bool _hasDegreeProjection;
    [ObservableProperty] private string _degreeLabel = string.Empty;
    [ObservableProperty] private string _degreeCaption = string.Empty;

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
    [ObservableProperty] private decimal? _formYear = 1;
    [ObservableProperty] private decimal? _formSemester = 1;
    [ObservableProperty] private decimal? _formTarget;
    [ObservableProperty] private string _formDropRules = string.Empty;

    public SubjectsViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;

        _selectedScale = ScaleOptions.FirstOrDefault(o => o.Kind == _state.GradeScale)
                         ?? ScaleOptions[0];

        foreach (var subject in _state.Subjects)
            Items.Add(new SubjectViewModel(subject, OnSubjectChanged, () => _state.GradeScale));

        RebuildAll();
        RebuildKnownCourses();
    }

    partial void OnSelectedScaleChanged(ScaleOption value)
    {
        if (_state.GradeScale == value.Kind)
            return;

        _state.GradeScale = value.Kind;
        foreach (var row in Items)
            row.RefreshScale();
        RebuildAll();
        _save();
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
        FormYear = 1;
        FormSemester = 1;
        FormTarget = null;
        FormDropRules = string.Empty;
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
        FormYear = row.Model.Year;
        FormSemester = row.Model.Semester;
        FormTarget = row.Model.TargetPercent is { } t ? (decimal)t : null;
        FormDropRules = string.Join("; ",
            row.Model.DropRules.Select(r => $"{r.Category}: best {r.KeepBest}"));
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

        var target = _editing ?? new Subject();

        target.Name = name;
        target.Code = string.IsNullOrWhiteSpace(FormCode) ? null : FormCode.Trim();
        target.Credits = FormCredits is { } c && c > 0 ? (double)c : 1;
        target.Year = FormYear is { } y && y >= 1 ? (int)y : 1;
        target.Semester = FormSemester is { } s && s >= 1 ? (int)s : 1;
        target.TargetPercent = FormTarget is { } t ? Math.Clamp((double)t, 0, 100) : null;
        target.DropRules = ParseDropRules(FormDropRules);

        if (_editing is null)
        {
            _state.Subjects.Add(target);
            Items.Add(new SubjectViewModel(target, OnSubjectChanged, () => _state.GradeScale));
        }
        else
        {
            Items.FirstOrDefault(r => r.Model == target)?.NotifyModelEdited();
        }

        _editing = null;
        IsModalOpen = false;

        RebuildAll();
        RebuildKnownCourses();
        if (IsDetailOpen)
            RefreshLinkedInfo();
        _save();
    }

    private bool CanUseModal() => IsModalOpen;

    /// <summary>"quiz: best 8; lab: best 3" → rules. Lenient: malformed
    /// segments are skipped rather than rejected.</summary>
    private static List<DropRule> ParseDropRules(string? text)
    {
        var rules = new List<DropRule>();
        if (string.IsNullOrWhiteSpace(text))
            return rules;

        foreach (var segment in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split(':', 2);
            if (parts.Length != 2)
                continue;

            var category = parts[0].Trim().ToLowerInvariant();
            var keepWord = parts[1].Replace("best", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (category.Length > 0 && int.TryParse(keepWord, out var n) && n > 0)
                rules.Add(new DropRule { Category = category, KeepBest = n });
        }

        return rules;
    }

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

        RebuildAll();
        _save();
    }

    private void OnSubjectChanged()
    {
        RebuildAll();
        _save();
    }

    private void OnYearWeightChanged(int year, double weight)
    {
        _state.YearWeights.RemoveAll(w => w.Year == year);
        if (weight > 0)
            _state.YearWeights.Add(new YearWeight { Year = year, Weight = weight });

        RecomputeDegree();
        _save();
    }

    /// <summary>Groups, overall figure and degree projection in one pass —
    /// they all read the same standings.</summary>
    private void RebuildAll()
    {
        HasSubjects = Items.Count > 0;
        RebuildGroups();
        RecomputeOverall();
        RecomputeDegree();
    }

    private void RebuildGroups()
    {
        Groups.Clear();

        var byTerm = Items
            .OrderBy(r => r.Model.Year)
            .ThenBy(r => r.Model.Semester)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .GroupBy(r => (r.Model.Year, r.Model.Semester))
            .ToList();

        foreach (var term in byTerm)
        {
            var graded = term.Where(r => r.CurrentPercent is not null).ToList();
            var avgLabel = string.Empty;
            if (graded.Count > 0)
            {
                var credits = graded.Sum(r => r.Model.Credits);
                var avg = graded.Sum(r => r.CurrentPercent!.Value * r.Model.Credits) / credits;
                avgLabel = $"{avg:0.#}% avg";
            }

            // A lone default term needs no header noise.
            var header = byTerm.Count == 1 && term.Key == (1, 1)
                ? string.Empty
                : $"year {term.Key.Year} · semester {term.Key.Semester}";

            var group = new TermGroupViewModel
            {
                Header = header,
                AverageLabel = avgLabel,
                HasAverage = avgLabel.Length > 0
            };
            foreach (var row in term)
                group.Items.Add(row);
            Groups.Add(group);
        }
    }

    /// <summary>The headline figure, phrased per scale: credit-weighted GPA
    /// on US 4.0, weighted average + classification elsewhere.</summary>
    private void RecomputeOverall()
    {
        var graded = Items
            .Where(s => s.CurrentPercent is not null)
            .Select(s => (Percent: s.CurrentPercent!.Value, s.Model.Credits))
            .ToList();

        if (graded.Count == 0)
        {
            GpaLabel = "no grades yet";
            GpaCaption = HasSubjects ? "grade an assessment and the figure appears" : string.Empty;
            return;
        }

        var totalCredits = graded.Sum(g => g.Credits);
        var avg = graded.Sum(g => g.Percent * g.Credits) / totalCredits;
        var scale = _state.GradeScale;

        GpaLabel = scale switch
        {
            GradeScaleKind.UsGpa =>
                $"{graded.Sum(g => GradeScale.ToPoints(g.Percent) * g.Credits) / totalCredits:0.00} gpa",
            GradeScaleKind.UkHonours => $"{avg:0.#}% · on track for a {GradeScale.Label(scale, avg)}",
            GradeScaleKind.Ects => $"{avg:0.#}% · {GradeScale.Label(scale, avg)}",
            _ => $"{avg:0.#}% weighted avg"
        };

        GpaCaption = $"{graded.Count} of {Items.Count} subjects graded · " +
                     $"{totalCredits:0.#} credits · {SelectedScale.Label}";
    }

    /// <summary>Year-weighted degree projection: each year's credit-weighted
    /// average, combined by the user's year weights (equal weighting until
    /// any weight is set). Only years with grades count.</summary>
    private void RecomputeDegree()
    {
        var years = Items
            .Where(r => r.CurrentPercent is not null)
            .GroupBy(r => r.Model.Year)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var credits = g.Sum(r => r.Model.Credits);
                var avg = g.Sum(r => r.CurrentPercent!.Value * r.Model.Credits) / credits;
                return (Year: g.Key, Average: avg);
            })
            .ToList();

        YearWeights.Clear();
        foreach (var (year, average) in years)
        {
            var stored = _state.YearWeights.FirstOrDefault(w => w.Year == year)?.Weight ?? 0;
            YearWeights.Add(new YearWeightViewModel(year, $"{average:0.#}%", stored, OnYearWeightChanged));
        }

        if (years.Count < 2)
        {
            HasDegreeProjection = false;
            return;
        }

        var anyWeights = years.Any(y => _state.YearWeights.Any(w => w.Year == y.Year && w.Weight > 0));
        double weightOf(int year) => anyWeights
            ? _state.YearWeights.FirstOrDefault(w => w.Year == year)?.Weight ?? 0
            : 1;

        var totalWeight = years.Sum(y => weightOf(y.Year));
        if (totalWeight <= 0)
        {
            HasDegreeProjection = false;
            return;
        }

        var final = years.Sum(y => y.Average * weightOf(y.Year)) / totalWeight;
        var scale = _state.GradeScale;

        DegreeLabel = scale == GradeScaleKind.UsGpa
            ? $"degree so far · {final:0.#}%"
            : $"degree so far · {final:0.#}% ({GradeScale.Label(scale, final)})";
        DegreeCaption = anyWeights
            ? "weighted by your year weights"
            : "years weighted equally — set weights below";
        HasDegreeProjection = true;
    }

    /// <summary>A markdown transcript of everything — the export feature.</summary>
    public string BuildTranscript()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# tomoshibi transcript · {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine($"- scale: {SelectedScale.Label}");
        sb.AppendLine($"- overall: {GpaLabel}");
        if (HasDegreeProjection)
            sb.AppendLine($"- {DegreeLabel}");
        sb.AppendLine();

        foreach (var group in Groups)
        {
            sb.AppendLine(group.Header.Length > 0
                ? $"## {group.Header}" + (group.HasAverage ? $" — {group.AverageLabel}" : "")
                : "## subjects");
            sb.AppendLine();

            foreach (var row in group.Items)
            {
                var code = row.HasCode ? $" ({row.Code})" : "";
                var grade = row.HasGrade ? $"{row.GradeLabel} {row.LetterLabel}".TrimEnd() : "ungraded";
                sb.AppendLine($"- **{row.Name}**{code} · {row.CreditsLabel} · {grade}");

                foreach (var a in row.Assessments)
                {
                    var g = a.IsGraded ? $"{a.Model.Grade:0.#}%" : "—";
                    var date = a.HasDate ? $" · {a.DateLabel}" : "";
                    sb.AppendLine($"  - {a.Title} ({a.WeightLabel}){date}: {g}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
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

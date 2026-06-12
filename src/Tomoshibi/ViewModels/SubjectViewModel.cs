using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// One subject card: the assessment list with inline grading, and the
/// derived numbers — current standing over the graded weight, letter,
/// grade points, and a warning when the syllabus weights don't reach 100.
/// </summary>
public partial class SubjectViewModel : ViewModelBase
{
    private readonly Action _changed;

    public Subject Model { get; }

    public ObservableCollection<AssessmentViewModel> Assessments { get; } = new();

    // ---- Derived grade state (recomputed on every change) ----
    [ObservableProperty] private bool _hasGrade;
    [ObservableProperty] private string _gradeLabel = "—";
    [ObservableProperty] private string _letterLabel = string.Empty;
    [ObservableProperty] private string _pointsLabel = string.Empty;
    [ObservableProperty] private string _standingCaption = string.Empty;
    [ObservableProperty] private bool _hasWeightWarning;
    [ObservableProperty] private string _weightWarning = string.Empty;

    /// <summary>Best case / floor / required-average lines, shown on the
    /// detail page while the subject is only partially graded.</summary>
    public ObservableCollection<string> OutlookLines { get; } = new();

    [ObservableProperty] private bool _hasOutlook;

    /// <summary>Current standing in percent over graded weight; null when
    /// nothing's graded. The page reads this for the GPA.</summary>
    public double? CurrentPercent { get; private set; }

    // ---- New assessment form (inline in the expanded card) ----
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private decimal? _newWeight;
    [ObservableProperty] private decimal? _newGrade;
    [ObservableProperty] private DateTime? _newDate;

    public string Name => Model.Name;
    public string? Code => Model.Code;
    public bool HasCode => !string.IsNullOrWhiteSpace(Model.Code);
    public string CreditsLabel => $"{Model.Credits:0.#} cr";
    public bool HasAssessments => Assessments.Count > 0;

    public SubjectViewModel(Subject model, Action changed)
    {
        Model = model;
        _changed = changed;

        foreach (var a in model.Assessments)
            Wrap(a);

        Recompute();
    }

    [RelayCommand]
    private void AddAssessment()
    {
        var title = NewTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || NewWeight is not { } w || w <= 0)
            return;

        var assessment = new Assessment
        {
            Title = title,
            Weight = Math.Clamp((double)w, 0.1, 100),
            Grade = NewGrade is { } g ? Math.Clamp((double)g, 0, 100) : null,
            Date = NewDate is { } d ? DateOnly.FromDateTime(d) : null
        };

        Model.Assessments.Add(assessment);
        Wrap(assessment);

        NewTitle = string.Empty;
        NewWeight = null;
        NewGrade = null;
        NewDate = null;

        OnPropertyChanged(nameof(HasAssessments));
        Recompute();
        _changed();
    }

    [RelayCommand]
    private void RemoveAssessment(AssessmentViewModel? row)
    {
        if (row is null)
            return;

        Model.Assessments.Remove(row.Model);
        row.Changed -= OnAssessmentChanged;
        Assessments.Remove(row);

        OnPropertyChanged(nameof(HasAssessments));
        Recompute();
        _changed();
    }

    /// <summary>Re-derive the standing after a name/code/credits edit too —
    /// the header labels read straight from the model.</summary>
    public void NotifyModelEdited()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Code));
        OnPropertyChanged(nameof(HasCode));
        OnPropertyChanged(nameof(CreditsLabel));
        Recompute();
    }

    private void Wrap(Assessment assessment)
    {
        var row = new AssessmentViewModel(assessment);
        row.Changed += OnAssessmentChanged;
        Assessments.Add(row);
    }

    private void OnAssessmentChanged()
    {
        Recompute();
        _changed();
    }

    private void Recompute()
    {
        var graded = Model.Assessments.Where(a => a.Grade is not null).ToList();
        var gradedWeight = graded.Sum(a => a.Weight);
        var totalWeight = Model.Assessments.Sum(a => a.Weight);
        var gradedPoints = graded.Sum(a => a.Grade!.Value * a.Weight);

        if (gradedWeight > 0)
        {
            var pct = gradedPoints / gradedWeight;
            CurrentPercent = pct;
            HasGrade = true;
            GradeLabel = $"{pct:0.#}%";
            LetterLabel = GradeScale.ToLetter(pct);
            PointsLabel = $"{GradeScale.ToPoints(pct):0.0} pts";
            StandingCaption = gradedWeight < totalWeight
                ? $"based on {gradedWeight:0.#}% graded of {totalWeight:0.#}%"
                : "all assessments graded";
        }
        else
        {
            CurrentPercent = null;
            HasGrade = false;
            GradeLabel = "—";
            LetterLabel = string.Empty;
            PointsLabel = string.Empty;
            StandingCaption = Model.Assessments.Count > 0
                ? "nothing graded yet"
                : string.Empty;
        }

        HasWeightWarning = Model.Assessments.Count > 0 && Math.Abs(totalWeight - 100) > 0.01;
        WeightWarning = HasWeightWarning ? $"weights sum to {totalWeight:0.#}%, not 100%" : string.Empty;

        RebuildOutlook(gradedWeight, totalWeight, gradedPoints);
    }

    /// <summary>What's still possible: ace-everything ceiling, bomb-everything
    /// floor, and the average needed on the remaining weight for fixed
    /// targets. Only meaningful while partially graded.</summary>
    private void RebuildOutlook(double gradedWeight, double totalWeight, double gradedPoints)
    {
        OutlookLines.Clear();

        var remaining = totalWeight - gradedWeight;
        if (gradedWeight <= 0 || remaining <= 0)
        {
            HasOutlook = false;
            return;
        }

        var best = (gradedPoints + remaining * 100) / totalWeight;
        var floor = gradedPoints / totalWeight;

        OutlookLines.Add($"best case · {best:0.#}% ({GradeScale.ToLetter(best)}) if you ace the rest");
        OutlookLines.Add($"floor · {floor:0.#}% ({GradeScale.ToLetter(floor)}) if the rest goes to zero");

        foreach (var target in new[] { 70.0, 80.0, 90.0 })
        {
            var need = (target * totalWeight - gradedPoints) / remaining;
            OutlookLines.Add(need switch
            {
                <= 0 => $"{target:0}% overall · already secured",
                > 100 => $"{target:0}% overall · out of reach",
                _ => $"{target:0}% overall · need avg {need:0.#}% on the rest"
            });
        }

        HasOutlook = true;
    }
}

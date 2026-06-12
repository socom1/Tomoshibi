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
/// One subject: the assessment list with inline grading, and everything
/// derived from it — standing over graded weight, target tracking, the
/// what-if simulator, drop-rule handling and a standing-over-time sparkline.
/// Real grades and simulated grades run through the same compute core with
/// a different grade selector, so the two can never disagree on the math.
/// </summary>
public partial class SubjectViewModel : ViewModelBase
{
    private readonly Action _changed;
    private readonly Func<GradeScaleKind> _scale;

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
    [ObservableProperty] private string _dropRulesCaption = string.Empty;
    [ObservableProperty] private bool _hasDropRules;

    /// <summary>Best case / floor / target / required-average lines for the
    /// detail page while the subject is only partially graded.</summary>
    public ObservableCollection<string> OutlookLines { get; } = new();

    [ObservableProperty] private bool _hasOutlook;

    // ---- Target ----
    [ObservableProperty] private bool _hasTarget;
    [ObservableProperty] private string _targetChip = string.Empty;
    [ObservableProperty] private bool _isTargetAtRisk;

    // ---- Simulator ----
    [ObservableProperty] private bool _isSimulating;
    [ObservableProperty] private bool _hasSimulation;
    [ObservableProperty] private string _simulationLabel = string.Empty;

    // ---- Sparkline (standing after each dated, graded assessment) ----
    [ObservableProperty] private string _sparkPoints = string.Empty;
    [ObservableProperty] private bool _hasSpark;

    /// <summary>Current standing in percent over graded weight; null when
    /// nothing's graded. The page reads this for the overall figure.</summary>
    public double? CurrentPercent { get; private set; }

    // ---- New assessment form (inline on the detail page) ----
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newCategory = string.Empty;
    [ObservableProperty] private decimal? _newWeight;
    [ObservableProperty] private decimal? _newGrade;
    [ObservableProperty] private DateTime? _newDate;

    public string Name => Model.Name;
    public string? Code => Model.Code;
    public bool HasCode => !string.IsNullOrWhiteSpace(Model.Code);
    public string CreditsLabel => $"{Model.Credits:0.#} cr";
    public string TermLabel => $"y{Model.Year} · s{Model.Semester}";
    public bool HasAssessments => Assessments.Count > 0;

    public SubjectViewModel(Subject model, Action changed, Func<GradeScaleKind> scale)
    {
        Model = model;
        _changed = changed;
        _scale = scale;

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
            Category = string.IsNullOrWhiteSpace(NewCategory) ? null : NewCategory.Trim().ToLowerInvariant(),
            Weight = Math.Clamp((double)w, 0.1, 100),
            Grade = NewGrade is { } g ? Math.Clamp((double)g, 0, 100) : null,
            Date = NewDate is { } d ? DateOnly.FromDateTime(d) : null
        };

        Model.Assessments.Add(assessment);
        Wrap(assessment);

        NewTitle = string.Empty;
        NewCategory = string.Empty;
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
        row.SimChanged -= OnSimChanged;
        Assessments.Remove(row);

        OnPropertyChanged(nameof(HasAssessments));
        Recompute();
        _changed();
    }

    /// <summary>Flip the what-if sandbox. Turning it off forgets every
    /// hypothetical grade — nothing was ever saved.</summary>
    [RelayCommand]
    private void ToggleSimulation()
    {
        IsSimulating = !IsSimulating;

        foreach (var row in Assessments)
        {
            if (!IsSimulating)
                row.SimGrade = null;
            row.IsSimVisible = IsSimulating && row.IsUngraded;
        }

        RecomputeSimulation();
    }

    /// <summary>Re-derive after a name/code/credits/target/rules edit.</summary>
    public void NotifyModelEdited()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Code));
        OnPropertyChanged(nameof(HasCode));
        OnPropertyChanged(nameof(CreditsLabel));
        OnPropertyChanged(nameof(TermLabel));
        Recompute();
        RecomputeSimulation();
    }

    /// <summary>The grading scale changed — relabel everything.</summary>
    public void RefreshScale()
    {
        Recompute();
        RecomputeSimulation();
    }

    private void Wrap(Assessment assessment)
    {
        var row = new AssessmentViewModel(assessment);
        row.Changed += OnAssessmentChanged;
        row.SimChanged += OnSimChanged;
        Assessments.Add(row);
    }

    private void OnAssessmentChanged()
    {
        // Grading a row mid-simulation retires its sim input.
        foreach (var row in Assessments)
            row.IsSimVisible = IsSimulating && row.IsUngraded;

        Recompute();
        RecomputeSimulation();
        _changed();
    }

    private void OnSimChanged() => RecomputeSimulation();

    // ================== the compute core ==================

    /// <summary>
    /// Weighted totals over the effective assessment set. Drop rules exclude
    /// the worst graded items beyond "best N" of a category (and surplus
    /// ungraded ones once N results are in). The grade selector is what lets
    /// reality and simulation share this path.
    /// </summary>
    private (double GradedPoints, double GradedWeight, double TotalWeight)
        Compute(Func<Assessment, double?> gradeOf)
    {
        var excluded = new HashSet<Assessment>();

        foreach (var rule in Model.DropRules.Where(r => r.KeepBest > 0))
        {
            var inCategory = Model.Assessments
                .Where(a => string.Equals(a.Category, rule.Category, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (inCategory.Count == 0)
                continue;

            var graded = inCategory
                .Where(a => gradeOf(a) is not null)
                .OrderByDescending(a => gradeOf(a)!.Value)
                .ToList();

            foreach (var dropped in graded.Skip(rule.KeepBest))
                excluded.Add(dropped);

            // Once N results exist, the remaining ungraded ones can't count.
            var slotsLeft = Math.Max(rule.KeepBest - graded.Count, 0);
            foreach (var surplus in inCategory.Where(a => gradeOf(a) is null).Skip(slotsLeft))
                excluded.Add(surplus);
        }

        double gradedPoints = 0, gradedWeight = 0, totalWeight = 0;
        foreach (var a in Model.Assessments.Where(a => !excluded.Contains(a)))
        {
            totalWeight += a.Weight;
            if (gradeOf(a) is { } grade)
            {
                gradedPoints += grade * a.Weight;
                gradedWeight += a.Weight;
            }
        }

        return (gradedPoints, gradedWeight, totalWeight);
    }

    private void Recompute()
    {
        var scale = _scale();
        var (gradedPoints, gradedWeight, totalWeight) = Compute(a => a.Grade);

        if (gradedWeight > 0)
        {
            var pct = gradedPoints / gradedWeight;
            CurrentPercent = pct;
            HasGrade = true;
            GradeLabel = $"{pct:0.#}%";
            LetterLabel = GradeScale.Label(scale, pct);
            PointsLabel = GradeScale.PointsLabel(scale, pct);
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
            StandingCaption = Model.Assessments.Count > 0 ? "nothing graded yet" : string.Empty;
        }

        HasWeightWarning = Model.Assessments.Count > 0 && Math.Abs(totalWeight - 100) > 0.01;
        WeightWarning = HasWeightWarning
            ? $"effective weights sum to {totalWeight:0.#}%, not 100%"
            : string.Empty;

        HasDropRules = Model.DropRules.Count > 0;
        DropRulesCaption = HasDropRules
            ? "rules · " + string.Join(" · ", Model.DropRules.Select(r => $"best {r.KeepBest} {r.Category}"))
            : string.Empty;

        HasTarget = Model.TargetPercent is not null;
        TargetChip = Model.TargetPercent is { } t ? $"target {t:0.#}%" : string.Empty;

        RebuildOutlook(scale, gradedPoints, gradedWeight, totalWeight);
        RebuildSparkline();
    }

    private void RebuildOutlook(GradeScaleKind scale, double gradedPoints, double gradedWeight, double totalWeight)
    {
        OutlookLines.Clear();
        IsTargetAtRisk = false;

        var remaining = totalWeight - gradedWeight;
        if (gradedWeight <= 0 || remaining <= 0)
        {
            // Fully graded subject with a target: still report the verdict.
            if (HasTarget && CurrentPercent is { } final && Model.TargetPercent is { } goal)
            {
                OutlookLines.Add(final >= goal
                    ? $"target {goal:0.#}% · hit ({final:0.#}%)"
                    : $"target {goal:0.#}% · missed ({final:0.#}%)");
                IsTargetAtRisk = final < goal;
                HasOutlook = true;
            }
            else
            {
                HasOutlook = false;
            }
            return;
        }

        string NeedLine(double target, string name)
        {
            var need = (target * totalWeight - gradedPoints) / remaining;
            return need switch
            {
                <= 0 => $"{name} · already secured",
                > 100 => $"{name} · out of reach",
                _ => $"{name} · need avg {need:0.#}% on the rest"
            };
        }

        // The user's own target leads; it also drives the at-risk tint.
        if (Model.TargetPercent is { } tp)
        {
            OutlookLines.Add(NeedLine(tp, $"your {tp:0.#}% target"));
            var needForTarget = (tp * totalWeight - gradedPoints) / remaining;
            IsTargetAtRisk = needForTarget > 85;
        }

        var best = (gradedPoints + remaining * 100) / totalWeight;
        var floor = gradedPoints / totalWeight;
        OutlookLines.Add($"best case · {best:0.#}% ({GradeScale.Label(scale, best)}) if you ace the rest");
        OutlookLines.Add($"floor · {floor:0.#}% ({GradeScale.Label(scale, floor)}) if the rest goes to zero");

        // Scale-appropriate fixed milestones.
        var milestones = scale == GradeScaleKind.UkHonours
            ? new[] { 50.0, 60.0, 70.0 }
            : new[] { 70.0, 80.0, 90.0 };
        foreach (var m in milestones)
            OutlookLines.Add(NeedLine(m, $"{m:0}% overall ({GradeScale.Label(scale, m)})"));

        HasOutlook = true;
    }

    private void RecomputeSimulation()
    {
        var sims = Assessments
            .Where(r => r.IsUngraded && r.SimGrade is not null)
            .ToDictionary(r => r.Model, r => (double)r.SimGrade!.Value);

        if (!IsSimulating || sims.Count == 0)
        {
            HasSimulation = false;
            SimulationLabel = string.Empty;
            return;
        }

        var (points, weight, _) = Compute(a =>
            a.Grade ?? (sims.TryGetValue(a, out var s) ? Math.Clamp(s, 0, 100) : null));

        if (weight <= 0)
        {
            HasSimulation = false;
            return;
        }

        var pct = points / weight;
        SimulationLabel = $"simulated · {pct:0.#}% ({GradeScale.Label(_scale(), pct)}) — not saved";
        HasSimulation = true;
    }

    /// <summary>Standing after each dated, graded assessment in date order —
    /// the polyline points for a 220×48 box, built here so the view stays
    /// markup-only.</summary>
    private void RebuildSparkline()
    {
        var dated = Model.Assessments
            .Where(a => a.Grade is not null && a.Date is not null)
            .OrderBy(a => a.Date)
            .ToList();

        if (dated.Count < 2)
        {
            HasSpark = false;
            SparkPoints = string.Empty;
            return;
        }

        const double w = 220, h = 48;
        double runningPoints = 0, runningWeight = 0;
        var sb = new StringBuilder();

        for (var i = 0; i < dated.Count; i++)
        {
            runningPoints += dated[i].Grade!.Value * dated[i].Weight;
            runningWeight += dated[i].Weight;
            var pct = runningPoints / runningWeight;

            var x = dated.Count == 1 ? 0 : i * w / (dated.Count - 1);
            var y = h - (pct / 100.0 * h);
            if (i > 0) sb.Append(' ');
            sb.Append($"{x:0.#},{y:0.#}");
        }

        SparkPoints = sb.ToString();
        HasSpark = true;
    }
}

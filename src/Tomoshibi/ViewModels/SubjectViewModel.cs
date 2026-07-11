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
    private readonly Action<string> _openUrl;

    public Subject Model { get; }

    public ObservableCollection<AssessmentViewModel> Assessments { get; } = new();

    /// <summary>The subject's own resource links — slides, syllabus, drive.</summary>
    public ObservableCollection<StudyLinkViewModel> Resources { get; } = new();

    [ObservableProperty] private bool _hasResources;

    // ---- New-resource form (inline on the detail page) ----
    [ObservableProperty] private string _newResourceTitle = string.Empty;
    [ObservableProperty] private string _newResourceUrl = string.Empty;

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

    // ---- What-if simulation ----
    /// <summary>Any assessment carries a what-if grade — the headline standing
    /// then shows the projected figure and the "clear what-ifs" button appears.</summary>
    [ObservableProperty] private bool _hasSimulation;

    // ---- Visuals: graded share + standing tint ----
    /// <summary>0..1 — how much of the effective weight has results.</summary>
    [ObservableProperty] private double _gradedShare;
    /// <summary>Standing as 0..1 for the detail ring.</summary>
    [ObservableProperty] private double _standingFraction;
    [ObservableProperty] private string _gradedShareCaption = string.Empty;

    /// <summary>Standing vs the target (or 65% when none): on track /
    /// within 10 points / drifting. Drives matcha-amber-sakura tints.</summary>
    [ObservableProperty] private bool _isStandingGood;
    [ObservableProperty] private bool _isStandingWarn;
    [ObservableProperty] private bool _isStandingBad;

    // ---- Sparkline (standing after each dated, graded assessment) ----
    [ObservableProperty] private string _sparkPoints = string.Empty;
    [ObservableProperty] private bool _hasSpark;

    // ---- Trend (which way the standing is moving) ----
    [ObservableProperty] private bool _hasTrend;
    [ObservableProperty] private string _trendLabel = string.Empty;
    [ObservableProperty] private bool _isTrendUp;
    [ObservableProperty] private bool _isTrendDown;

    // ---- "What you need" calculator ----
    /// <summary>The latest real-grade compute, stashed so the calculator can
    /// answer "what do I need for X%?" without recomputing the whole subject.</summary>
    private double _calcGradedPoints, _calcGradedWeight, _calcTotalWeight;

    /// <summary>The overall percent the user is aiming for in the calculator.</summary>
    [ObservableProperty] private decimal? _calcTarget;

    [ObservableProperty] private bool _hasCalc;
    [ObservableProperty] private string _calcResult = string.Empty;
    [ObservableProperty] private string _calcTargetLetter = string.Empty;
    [ObservableProperty] private bool _isCalcGood;
    [ObservableProperty] private bool _isCalcWarn;
    [ObservableProperty] private bool _isCalcBad;

    // ---- Progressive disclosure: the planning tools collapse by default ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanningChevron))]
    private bool _showPlanning;

    /// <summary>The toggle's fold glyph. Kept out of the label string: the
    /// label renders in the CJK family (for the kanji/Latin baseline) which
    /// lacks ▸/▾, so the glyph gets its own default-font TextBlock.</summary>
    public string PlanningChevron => ShowPlanning ? "▾" : "▸";

    [RelayCommand]
    private void TogglePlanning() => ShowPlanning = !ShowPlanning;

    /// <summary>Current standing in percent over graded weight; null when
    /// nothing's graded. The page reads this for the overall figure.</summary>
    public double? CurrentPercent { get; private set; }

    /// <summary>The latest drop-rule-aware compute totals, in subject-weight
    /// terms (~100 across the assessments). The overall-goal planner pools
    /// these across subjects, scaled by credits.</summary>
    public double TotalWeightPct => _calcTotalWeight;
    public double GradedWeightPct => _calcGradedWeight;
    public double GradedPointsPct => _calcGradedPoints;

    // ---- Add / edit assessment modal ----
    [ObservableProperty] private bool _isAssessmentModalOpen;
    [ObservableProperty] private bool _isEditingAssessment;
    [ObservableProperty] private string _assessModalTitle = "新しい評価 · new assessment";
    [ObservableProperty] private string _formAssessTitle = string.Empty;
    [ObservableProperty] private string _formAssessCategory = string.Empty;
    [ObservableProperty] private decimal? _formAssessWeight;
    [ObservableProperty] private decimal? _formAssessGrade;
    [ObservableProperty] private decimal? _formAssessWhatIf;
    [ObservableProperty] private DateTime? _formAssessDate;
    private AssessmentViewModel? _editingAssessment;

    // ---- Bulk paste (add a whole syllabus at once) ----
    [ObservableProperty] private bool _isBulkVisible;
    [ObservableProperty] private string _bulkText = string.Empty;

    public string Name => Model.Name;
    public string? Code => Model.Code;
    public bool HasCode => !string.IsNullOrWhiteSpace(Model.Code);
    public string CreditsLabel => $"{Model.Credits:0.#} cr";
    public string TermLabel => $"y{Model.Year} · s{Model.Semester}";
    public bool HasAssessments => Assessments.Count > 0;

    /// <summary>Free-form course notes. Persisted straight onto the model and
    /// saved on every edit, like the daily intention and sticky notes.</summary>
    public string Notes
    {
        get => Model.Notes;
        set
        {
            if (Model.Notes == value)
                return;
            Model.Notes = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public SubjectViewModel(Subject model, Action changed, Func<GradeScaleKind> scale,
                            Action<string> openUrl)
    {
        Model = model;
        _changed = changed;
        _scale = scale;
        _openUrl = openUrl;

        // Seed the calculator with the subject's own target, or a sensible default.
        _calcTarget = model.TargetPercent is { } t ? (decimal)t : 80m;

        foreach (var a in model.Assessments)
            Wrap(a);

        foreach (var link in model.Resources)
            Resources.Add(WrapResource(link));
        HasResources = Resources.Count > 0;

        Recompute();
    }

    [RelayCommand]
    private void AddResource()
    {
        var url = NewResourceUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        // Be forgiving — a bare drive.google.com/… still opens once schemed.
        if (!url.Contains("://"))
            url = "https://" + url;

        var link = new StudyLink { Title = NewResourceTitle?.Trim() ?? string.Empty, Url = url };
        Model.Resources.Add(link);
        Resources.Add(WrapResource(link));
        HasResources = true;

        NewResourceTitle = string.Empty;
        NewResourceUrl = string.Empty;
        _changed();
    }

    private StudyLinkViewModel WrapResource(StudyLink link) =>
        new(link, l => _openUrl(l.Url), RemoveResource);

    private void RemoveResource(StudyLinkViewModel row)
    {
        Model.Resources.Remove(row.Model);
        Resources.Remove(row);
        HasResources = Resources.Count > 0;
        _changed();
    }

    /// <summary>Open the modal on a blank form (the + button).</summary>
    [RelayCommand]
    private void OpenAddAssessment()
    {
        _editingAssessment = null;
        IsEditingAssessment = false;
        AssessModalTitle = "新しい評価 · new assessment";
        FormAssessTitle = string.Empty;
        FormAssessCategory = string.Empty;
        FormAssessWeight = 100;
        FormAssessGrade = null;
        FormAssessWhatIf = null;
        FormAssessDate = null;
        IsAssessmentModalOpen = true;
    }

    /// <summary>Open the modal on an existing assessment (click its row).</summary>
    public void BeginEditAssessment(AssessmentViewModel row)
    {
        _editingAssessment = row;
        IsEditingAssessment = true;
        AssessModalTitle = "編集 · edit assessment";
        FormAssessTitle = row.Model.Title;
        FormAssessCategory = row.Model.Category ?? string.Empty;
        FormAssessWeight = (decimal)row.Model.Weight;
        FormAssessGrade = row.Model.Grade is { } g ? (decimal)g : null;
        FormAssessWhatIf = row.SimGrade;
        FormAssessDate = row.Model.Date is { } d ? d.ToDateTime(TimeOnly.MinValue) : null;
        IsAssessmentModalOpen = true;
    }

    [RelayCommand]
    private void CancelAssessmentModal() => IsAssessmentModalOpen = false;

    [RelayCommand]
    private void SaveAssessment()
    {
        var title = FormAssessTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || FormAssessWeight is not { } w || w <= 0)
            return;

        var category = string.IsNullOrWhiteSpace(FormAssessCategory)
            ? null : FormAssessCategory.Trim().ToLowerInvariant();
        var weight = Math.Clamp((double)w, 0.1, 100);
        var grade = FormAssessGrade is { } g ? (decimal?)Math.Clamp((double)g, 0, 100) : null;
        var date = FormAssessDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;
        var whatIf = FormAssessWhatIf is { } wi ? (decimal?)Math.Clamp(wi, 0, 100) : null;

        AssessmentViewModel row;
        if (_editingAssessment is { } editing)
        {
            editing.Model.Title = title;
            editing.Model.Category = category;
            editing.Model.Weight = weight;
            editing.Model.Date = date;
            editing.NotifyEdited();
            row = editing;
        }
        else
        {
            var assessment = new Assessment { Title = title, Category = category, Weight = weight, Date = date };
            Model.Assessments.Add(assessment);
            row = Wrap(assessment);
            OnPropertyChanged(nameof(HasAssessments));
        }

        // Grade and what-if fire their own change events (persist / re-simulate).
        row.Grade = grade;
        row.SimGrade = whatIf;

        IsAssessmentModalOpen = false;
        Recompute();
        _changed();
    }

    [RelayCommand]
    private void DeleteAssessment()
    {
        if (_editingAssessment is { } row)
        {
            Model.Assessments.Remove(row.Model);
            row.Changed -= OnAssessmentChanged;
            row.SimChanged -= OnSimChanged;
            Assessments.Remove(row);
            OnPropertyChanged(nameof(HasAssessments));
        }

        IsAssessmentModalOpen = false;
        Recompute();
        _changed();
    }

    /// <summary>Drop every what-if grade and clear the simulated standing.</summary>
    [RelayCommand]
    private void ClearWhatIfs()
    {
        foreach (var assessment in Assessments)
            assessment.SimGrade = null;
        Recompute();
    }

    [RelayCommand]
    private void ToggleBulk() => IsBulkVisible = !IsBulkVisible;

    /// <summary>Add many assessments at once from pasted lines, one per line:
    /// <c>title, weight, [category], [date]</c>. Lenient — blank lines and
    /// <c>#</c>/<c>//</c> comments are skipped; a token that parses as a date
    /// is taken as the date, any other extra token as the category.</summary>
    [RelayCommand]
    private void AddBulk()
    {
        if (string.IsNullOrWhiteSpace(BulkText))
            return;

        var added = 0;
        foreach (var rawLine in BulkText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                continue;

            var parts = line.Split(new[] { ',', '\t' }).Select(p => p.Trim()).ToArray();
            var title = parts[0];
            if (string.IsNullOrWhiteSpace(title))
                continue;

            double weight = 0;
            if (parts.Length > 1)
                double.TryParse(parts[1].Replace("%", "").Trim(), out weight);
            if (weight <= 0)
                weight = 100; // a line with no weight is taken as the whole grade

            string? category = null;
            DateOnly? date = null;
            for (var i = 2; i < parts.Length; i++)
            {
                var tok = parts[i];
                if (tok.Length == 0)
                    continue;
                if (date is null && DateOnly.TryParse(tok, out var d))
                    date = d;
                else
                    category ??= tok.ToLowerInvariant();
            }

            var assessment = new Assessment
            {
                Title = title,
                Weight = Math.Clamp(weight, 0, 100),
                Category = category,
                Date = date
            };
            Model.Assessments.Add(assessment);
            Wrap(assessment);
            added++;
        }

        if (added == 0)
            return;

        BulkText = string.Empty;
        IsBulkVisible = false;
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

    /// <summary>Re-derive after a name/code/credits/target/rules edit.</summary>
    public void NotifyModelEdited()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Code));
        OnPropertyChanged(nameof(HasCode));
        OnPropertyChanged(nameof(CreditsLabel));
        OnPropertyChanged(nameof(TermLabel));
        Recompute();
    }

    /// <summary>The grading scale changed — relabel everything.</summary>
    public void RefreshScale()
    {
        Recompute();
    }

    private AssessmentViewModel Wrap(Assessment assessment)
    {
        var row = new AssessmentViewModel(assessment);
        row.Changed += OnAssessmentChanged;
        row.SimChanged += OnSimChanged;
        Assessments.Add(row);
        return row;
    }

    private void OnAssessmentChanged()
    {
        Recompute();
        _changed();
    }

    private void OnSimChanged() => Recompute();

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

        // Real standing — over graded weight only. This is what the page-level
        // GPA, insights and goal read, so what-ifs never leak into them.
        var (gradedPoints, gradedWeight, totalWeight) = Compute(a => a.Grade);
        CurrentPercent = gradedWeight > 0 ? gradedPoints / gradedWeight : null;

        // What-if standing — the same maths with the hypothetical grades folded
        // in. The headline below shows this when any what-if is set.
        var sims = Assessments
            .Where(r => r.IsUngraded && r.SimGrade is not null)
            .ToDictionary(r => r.Model, r => (double)r.SimGrade!.Value);
        HasSimulation = sims.Count > 0;

        var (dispPoints, dispGradedWeight, dispTotalWeight) = HasSimulation
            ? Compute(a => a.Grade ?? (sims.TryGetValue(a, out var s) ? Math.Clamp(s, 0, 100) : null))
            : (gradedPoints, gradedWeight, totalWeight);

        double? dispPercent = dispGradedWeight > 0 ? dispPoints / dispGradedWeight : null;

        if (dispPercent is { } pct)
        {
            HasGrade = true;
            GradeLabel = $"{pct:0.#}%";
            LetterLabel = GradeScale.Label(scale, pct);
            PointsLabel = GradeScale.PointsLabel(scale, pct);
            StandingCaption = HasSimulation
                ? "projected with your what-ifs — not saved"
                : dispGradedWeight < dispTotalWeight
                    ? $"based on {dispGradedWeight:0.#}% graded of {dispTotalWeight:0.#}%"
                    : "all assessments graded";
        }
        else
        {
            HasGrade = false;
            GradeLabel = "—";
            LetterLabel = string.Empty;
            PointsLabel = string.Empty;
            StandingCaption = Model.Assessments.Count > 0 ? "nothing graded yet" : string.Empty;
        }

        GradedShare = dispTotalWeight > 0 ? dispGradedWeight / dispTotalWeight : 0;
        GradedShareCaption = dispTotalWeight > 0 ? $"{GradedShare:P0} graded" : string.Empty;

        StandingFraction = (dispPercent ?? 0) / 100.0;
        var reference = Model.TargetPercent ?? 65;
        IsStandingGood = HasGrade && dispPercent >= reference;
        IsStandingWarn = HasGrade && !IsStandingGood && dispPercent >= reference - 10;
        IsStandingBad = HasGrade && !IsStandingGood && !IsStandingWarn;

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

        // Stash for the interactive calculator, then refresh it.
        _calcGradedPoints = gradedPoints;
        _calcGradedWeight = gradedWeight;
        _calcTotalWeight = totalWeight;

        RebuildOutlook(scale, gradedPoints, gradedWeight, totalWeight);
        RebuildSparkline();
        RecomputeCalc();
    }

    partial void OnCalcTargetChanged(decimal? value) => RecomputeCalc();

    /// <summary>"To finish on X% overall you need avg Y% on what's left." Reads
    /// the stashed real-grade totals so typing a target is instant. Verdicts:
    /// already secured, a required average (tinted by how hard it is), or out
    /// of reach when even acing the rest can't get there.</summary>
    private void RecomputeCalc()
    {
        var total = _calcTotalWeight;
        HasCalc = total > 0 && Model.Assessments.Count > 0;
        if (!HasCalc)
        {
            CalcResult = string.Empty;
            CalcTargetLetter = string.Empty;
            IsCalcGood = IsCalcWarn = IsCalcBad = false;
            return;
        }

        var target = Math.Clamp((double)(CalcTarget ?? 0), 0, 100);
        CalcTargetLetter = GradeScale.Label(_scale(), target);

        var remaining = total - _calcGradedWeight;
        void Tone(bool good, bool warn) { IsCalcGood = good; IsCalcWarn = warn; IsCalcBad = !good && !warn; }

        if (remaining <= 0.01)
        {
            var final = _calcGradedWeight > 0 ? _calcGradedPoints / _calcGradedWeight : 0;
            var hit = final >= target;
            CalcResult = hit
                ? $"done — you finished at {final:0.#}%, at or above {target:0.#}%"
                : $"the term is fully graded at {final:0.#}% — {target:0.#}% is out of reach now";
            Tone(hit, false);
            return;
        }

        var need = (target * total - _calcGradedPoints) / remaining;
        if (need <= 0)
        {
            CalcResult = $"secured — even 0% on the remaining {remaining:0.#}% stays at or above {target:0.#}%";
            Tone(true, false);
        }
        else if (need > 100)
        {
            var best = (_calcGradedPoints + remaining * 100) / total;
            CalcResult = $"out of reach — acing everything left tops out at {best:0.#}%";
            Tone(false, false);
        }
        else
        {
            CalcResult = $"need avg {need:0.#}% on the remaining {remaining:0.#}% of the grade";
            Tone(need <= 70, need <= 88);
        }
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
            HasTrend = false;
            SparkPoints = string.Empty;
            return;
        }

        const double w = 220, h = 48;
        double runningPoints = 0, runningWeight = 0;
        double firstPct = 0, lastPct = 0;
        var sb = new StringBuilder();

        for (var i = 0; i < dated.Count; i++)
        {
            runningPoints += dated[i].Grade!.Value * dated[i].Weight;
            runningWeight += dated[i].Weight;
            var pct = runningPoints / runningWeight;
            if (i == 0) firstPct = pct;
            lastPct = pct;

            var x = dated.Count == 1 ? 0 : i * w / (dated.Count - 1);
            var y = h - (pct / 100.0 * h);
            if (i > 0) sb.Append(' ');
            sb.Append($"{x:0.#},{y:0.#}");
        }

        SparkPoints = sb.ToString();
        HasSpark = true;

        // Which way the cumulative standing has moved as results landed.
        var delta = lastPct - firstPct;
        IsTrendUp = delta > 0.5;
        IsTrendDown = delta < -0.5;
        HasTrend = true;
        TrendLabel = IsTrendUp
            ? $"▲ trending up {delta:0.#}% across {dated.Count} graded"
            : IsTrendDown
                ? $"▼ slipping {Math.Abs(delta):0.#}% across {dated.Count} graded"
                : $"steady across {dated.Count} graded";
    }
}

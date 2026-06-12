using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// One assessment row. The grade is editable in place — type the result the
/// day it lands. SimGrade is the what-if twin: it never touches the model
/// and never saves, it only feeds the subject's simulated standing.
/// </summary>
public partial class AssessmentViewModel : ViewModelBase
{
    public Assessment Model { get; }

    /// <summary>A real grade changed — persist and recompute.</summary>
    public event Action? Changed;

    /// <summary>A hypothetical grade changed — recompute the simulation only.</summary>
    public event Action? SimChanged;

    /// <summary>Decimal because NumericUpDown binds decimal; null = ungraded.</summary>
    [ObservableProperty]
    private decimal? _grade;

    /// <summary>What-if grade for the simulator. Transient, never saved.</summary>
    [ObservableProperty]
    private decimal? _simGrade;

    /// <summary>Set by the subject when the simulator toggles: the sim input
    /// only shows on ungraded rows while simulating.</summary>
    [ObservableProperty]
    private bool _isSimVisible;

    public string Title => Model.Title;
    public string WeightLabel => $"{Model.Weight:0.#}%";

    public bool HasCategory => !string.IsNullOrWhiteSpace(Model.Category);
    public string? Category => Model.Category;

    public bool HasDate => Model.Date is not null;
    public string DateLabel => Model.Date is { } d ? $"{d:MMM d}".ToLowerInvariant() : string.Empty;

    public bool IsGraded => Model.Grade is not null;
    public bool IsUngraded => Model.Grade is null;

    public AssessmentViewModel(Assessment model)
    {
        Model = model;
        _grade = model.Grade is { } g ? (decimal)g : null;
    }

    partial void OnGradeChanged(decimal? value)
    {
        Model.Grade = value is { } g ? Math.Clamp((double)g, 0, 100) : null;
        OnPropertyChanged(nameof(IsGraded));
        OnPropertyChanged(nameof(IsUngraded));
        Changed?.Invoke();
    }

    partial void OnSimGradeChanged(decimal? value) => SimChanged?.Invoke();
}

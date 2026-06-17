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

    /// <summary>Decimal because the form binds decimal; null = ungraded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGraded))]
    [NotifyPropertyChangedFor(nameof(IsUngraded))]
    [NotifyPropertyChangedFor(nameof(GradeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsSimulated))]
    private decimal? _grade;

    /// <summary>What-if grade for the simulator. Transient, never saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimulated))]
    [NotifyPropertyChangedFor(nameof(SimLabel))]
    private decimal? _simGrade;

    public string Title => Model.Title;
    public string WeightLabel => $"{Model.Weight:0.#}%";

    public bool HasCategory => !string.IsNullOrWhiteSpace(Model.Category);
    public string? Category => Model.Category;

    public bool HasDate => Model.Date is not null;
    public string DateLabel => Model.Date is { } d ? $"{d:MMM d}".ToLowerInvariant() : string.Empty;

    public bool IsGraded => Model.Grade is not null;
    public bool IsUngraded => Model.Grade is null;

    /// <summary>The grade shown on the row — "88%" once results land, "—" until.</summary>
    public string GradeDisplay => Grade is { } g ? $"{g:0.#}%" : "—";

    /// <summary>A what-if grade is set on a still-ungraded assessment — drives
    /// the row's amber "what-if" treatment.</summary>
    public bool IsSimulated => Grade is null && SimGrade is not null;
    public string SimLabel => SimGrade is { } s ? $"what-if {s:0.#}%" : string.Empty;

    public AssessmentViewModel(Assessment model)
    {
        Model = model;
        _grade = model.Grade is { } g ? (decimal)g : null;
    }

    /// <summary>Re-read the labels after the model's title/weight/category/date
    /// were edited through the modal.</summary>
    public void NotifyEdited()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WeightLabel));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(HasCategory));
        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(HasDate));
    }

    partial void OnGradeChanged(decimal? value)
    {
        Model.Grade = value is { } g ? Math.Clamp((double)g, 0, 100) : null;
        Changed?.Invoke();
    }

    partial void OnSimGradeChanged(decimal? value) => SimChanged?.Invoke();
}

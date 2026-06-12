using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// One assessment row. The grade is editable in place — type the result the
/// day it lands; the write-through raises <see cref="Changed"/> so the
/// subject recomputes its standing and the page recomputes the GPA.
/// </summary>
public partial class AssessmentViewModel : ViewModelBase
{
    public Assessment Model { get; }

    public event Action? Changed;

    /// <summary>Decimal because NumericUpDown binds decimal; null = ungraded.</summary>
    [ObservableProperty]
    private decimal? _grade;

    public string Title => Model.Title;
    public string WeightLabel => $"{Model.Weight:0.#}%";

    public bool HasDate => Model.Date is not null;
    public string DateLabel => Model.Date is { } d ? $"{d:MMM d}".ToLowerInvariant() : string.Empty;

    public bool IsGraded => Model.Grade is not null;
    public string LetterLabel => Model.Grade is { } g ? Services.GradeScale.ToLetter(g) : string.Empty;

    public AssessmentViewModel(Assessment model)
    {
        Model = model;
        _grade = model.Grade is { } g ? (decimal)g : null;
    }

    partial void OnGradeChanged(decimal? value)
    {
        Model.Grade = value is { } g ? Math.Clamp((double)g, 0, 100) : null;
        OnPropertyChanged(nameof(IsGraded));
        OnPropertyChanged(nameof(LetterLabel));
        Changed?.Invoke();
    }
}

namespace Tomoshibi.Models;

/// <summary>Which grading system the subjects page speaks. Grades are always
/// *entered* as percentages; the scale only changes how they're labelled and
/// how the overall figure is presented.</summary>
public enum GradeScaleKind
{
    UsGpa,
    UkHonours,
    Ects,
    Percentage
}

namespace Tomoshibi.ViewModels;

/// <summary>One bar in the focus-by-course breakdown: a course, the hours
/// studied, and its share of the busiest course (0..1) for the bar width.</summary>
public class CourseFocusViewModel
{
    public string Course { get; init; } = string.Empty;
    public string HoursLabel { get; init; } = string.Empty;
    public double Fraction { get; init; }
}

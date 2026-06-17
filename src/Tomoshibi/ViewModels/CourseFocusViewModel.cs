namespace Tomoshibi.ViewModels;

/// <summary>One bar in the focus-by-course breakdown: a course, the hours
/// studied, its share of the busiest course (0..1), and that share drawn as
/// a fixed-width block bar (████░░░░) for the terminal look.</summary>
public class CourseFocusViewModel
{
    public string Course { get; init; } = string.Empty;
    public string HoursLabel { get; init; } = string.Empty;
    public double Fraction { get; init; }
    public string Bar { get; init; } = string.Empty;
}

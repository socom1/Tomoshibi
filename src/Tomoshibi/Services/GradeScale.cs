using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Percentage → label conversion for every supported grading system, in one
/// pure place. Grades are always stored as percentages; the scale decides
/// what they're called. US letters/points use the standard table; UK honours
/// uses the classification bands; ECTS uses the common fixed mapping.
/// </summary>
public static class GradeScale
{
    public static double ToPoints(double percent) => percent switch
    {
        >= 93 => 4.0,
        >= 90 => 3.7,
        >= 87 => 3.3,
        >= 83 => 3.0,
        >= 80 => 2.7,
        >= 77 => 2.3,
        >= 73 => 2.0,
        >= 70 => 1.7,
        >= 67 => 1.3,
        >= 63 => 1.0,
        >= 60 => 0.7,
        _ => 0.0
    };

    /// <summary>The short chip next to a grade: "B+", "2:1", "B" — or empty
    /// on the plain percentage scale.</summary>
    public static string Label(GradeScaleKind kind, double percent) => kind switch
    {
        GradeScaleKind.UsGpa => UsLetter(percent),
        GradeScaleKind.UkHonours => percent switch
        {
            >= 70 => "first",
            >= 60 => "2:1",
            >= 50 => "2:2",
            >= 40 => "third",
            _ => "fail"
        },
        GradeScaleKind.Ects => percent switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            >= 50 => "E",
            _ => "F"
        },
        _ => string.Empty
    };

    /// <summary>"3.0 pts" on the US scale; the other scales don't have a
    /// points concept, so empty.</summary>
    public static string PointsLabel(GradeScaleKind kind, double percent) =>
        kind == GradeScaleKind.UsGpa ? $"{ToPoints(percent):0.0} pts" : string.Empty;

    private static string UsLetter(double percent) => percent switch
    {
        >= 93 => "A",
        >= 90 => "A-",
        >= 87 => "B+",
        >= 83 => "B",
        >= 80 => "B-",
        >= 77 => "C+",
        >= 73 => "C",
        >= 70 => "C-",
        >= 67 => "D+",
        >= 63 => "D",
        >= 60 => "D-",
        _ => "F"
    };
}

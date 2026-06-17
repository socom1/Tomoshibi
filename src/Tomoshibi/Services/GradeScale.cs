using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Percentage → label conversion for every supported grading system, in one
/// pure place. Grades are always stored as percentages; the scale decides
/// what they're called. US letters/points use the standard table; UK honours
/// uses the classification bands; ECTS uses the common fixed mapping; and the
/// Custom scale reads the user's own bands from <see cref="CustomBands"/>.
/// </summary>
public static class GradeScale
{
    /// <summary>The active custom bands, pointed at <c>AppState.CustomGradeBands</c>
    /// so edits there are seen here without re-wiring. Null until set.</summary>
    public static IReadOnlyList<GradeBand>? CustomBands { get; set; }

    /// <summary>A reasonable starting scale for first-time custom users to edit.</summary>
    public static List<GradeBand> DefaultCustomBands() => new()
    {
        new GradeBand { MinPercent = 90, Label = "A", Points = 4.0 },
        new GradeBand { MinPercent = 80, Label = "B", Points = 3.0 },
        new GradeBand { MinPercent = 70, Label = "C", Points = 2.0 },
        new GradeBand { MinPercent = 60, Label = "D", Points = 1.0 },
        new GradeBand { MinPercent = 0,  Label = "F", Points = 0.0 },
    };

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
        GradeScaleKind.Custom => CustomBandFor(percent)?.Label ?? string.Empty,
        _ => string.Empty
    };

    /// <summary>"3.0 pts" on scales that carry a points concept (US, or a custom
    /// scale where the bands define points); empty otherwise.</summary>
    public static string PointsLabel(GradeScaleKind kind, double percent) => kind switch
    {
        GradeScaleKind.UsGpa => $"{ToPoints(percent):0.0} pts",
        GradeScaleKind.Custom when UsesPoints(kind) => $"{Points(kind, percent):0.0} pts",
        _ => string.Empty
    };

    /// <summary>GPA points for a percent on a points-bearing scale (US table or
    /// the custom bands). 0 for scales without a points concept.</summary>
    public static double Points(GradeScaleKind kind, double percent) => kind switch
    {
        GradeScaleKind.UsGpa => ToPoints(percent),
        GradeScaleKind.Custom => CustomBandFor(percent)?.Points ?? 0.0,
        _ => 0.0
    };

    /// <summary>True when the scale yields a GPA — always on US, and on a custom
    /// scale once any band carries non-zero points.</summary>
    public static bool UsesPoints(GradeScaleKind kind) => kind switch
    {
        GradeScaleKind.UsGpa => true,
        GradeScaleKind.Custom => CustomBands is { } b && b.Any(x => x.Points > 0),
        _ => false
    };

    /// <summary>The custom band covering a percent: the highest band whose floor
    /// it clears, or the lowest band when it's below them all.</summary>
    private static GradeBand? CustomBandFor(double percent)
    {
        var bands = CustomBands;
        if (bands is null || bands.Count == 0)
            return null;

        return bands.Where(b => percent >= b.MinPercent)
                    .OrderByDescending(b => b.MinPercent)
                    .FirstOrDefault()
               ?? bands.OrderBy(b => b.MinPercent).First();
    }

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

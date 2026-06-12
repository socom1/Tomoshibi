namespace Tomoshibi.Services;

/// <summary>
/// Percentage → letter / 4.0-scale mapping, the standard US table. Pure
/// functions so the conversion is testable and lives in exactly one place;
/// a configurable scale can replace this later without touching callers.
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

    public static string ToLetter(double percent) => percent switch
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

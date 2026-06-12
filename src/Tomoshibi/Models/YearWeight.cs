namespace Tomoshibi.Models;

/// <summary>How much one academic year counts toward the final degree mark —
/// UK-style year weighting (e.g. year 1 0%, year 2 40%, year 3 60%).</summary>
public class YearWeight
{
    public int Year { get; set; }
    public double Weight { get; set; }
}

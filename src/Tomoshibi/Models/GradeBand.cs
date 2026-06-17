namespace Tomoshibi.Models;

/// <summary>
/// One band of a user-defined grade scale: everything at or above
/// <see cref="MinPercent"/> (and below the next band up) gets this label.
/// <see cref="Points"/> drives an optional GPA — leave the points at 0 across
/// the scale to get a plain "percent · label" headline instead.
/// </summary>
public class GradeBand
{
    public double MinPercent { get; set; }
    public string Label { get; set; } = string.Empty;
    public double Points { get; set; }
}

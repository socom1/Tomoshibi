namespace Tomoshibi.Models;

/// <summary>
/// "Best N of a category counts" — e.g. keep the best 8 quizzes. Applied per
/// assessment category when computing the subject standing; works best when
/// the assessments in a category share the same weight (the usual syllabus
/// shape).
/// </summary>
public class DropRule
{
    public string Category { get; set; } = string.Empty;
    public int KeepBest { get; set; }
}

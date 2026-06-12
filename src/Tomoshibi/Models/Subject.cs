using System;
using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// A course being taken — the unit GPA is computed over. Code doubles as the
/// course tag used across tasks, slots and tickets; Credits weight the
/// subject's contribution to the overall GPA.
/// </summary>
public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Course code, e.g. "MATH101". Optional but recommended — it
    /// links the subject to course tags elsewhere in the app.</summary>
    public string? Code { get; set; }

    /// <summary>GPA weight of this subject (credit hours / ECTS / units).</summary>
    public double Credits { get; set; } = 1;

    /// <summary>Which academic year and semester this subject belongs to —
    /// drives the term grouping and the degree-weighting projection.</summary>
    public int Year { get; set; } = 1;
    public int Semester { get; set; } = 1;

    /// <summary>The grade you're aiming for, in percent. Reorients the
    /// outlook around your goal instead of generic targets.</summary>
    public double? TargetPercent { get; set; }

    /// <summary>Best-N-of-category rules, e.g. keep the best 8 quizzes.</summary>
    public List<DropRule> DropRules { get; set; } = new();

    public List<Assessment> Assessments { get; set; } = new();
}

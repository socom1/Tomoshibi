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

    public List<Assessment> Assessments { get; set; } = new();
}

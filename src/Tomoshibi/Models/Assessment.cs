using System;

namespace Tomoshibi.Models;

/// <summary>
/// One graded piece of a subject — an exam, essay, lab, quiz. Weight is its
/// share of the subject grade in percent; Grade stays null until results
/// land, so the subject can show a current standing over what's graded.
/// </summary>
public class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;

    /// <summary>Share of the subject grade, in percent (e.g. 30).</summary>
    public double Weight { get; set; }

    /// <summary>Result in percent (0–100). Null = not graded yet.</summary>
    public double? Grade { get; set; }

    public DateOnly? Date { get; set; }
}

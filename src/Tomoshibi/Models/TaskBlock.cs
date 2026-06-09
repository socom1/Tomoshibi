namespace Tomoshibi.Models;

/// <summary>
/// One parsed task from the task-template source. Transient — produced by
/// <see cref="Services.TaskTemplateParser"/> on every edit, never serialised.
/// The three minute fields mirror the pomodoro settings and let a task
/// declare its own focus and break lengths.
/// </summary>
public class TaskBlock
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Focus block length in minutes.</summary>
    public int? Study { get; set; }

    /// <summary>Short break length in minutes.</summary>
    public int? Short { get; set; }

    /// <summary>Long break length in minutes.</summary>
    public int? Long { get; set; }

    public string? Course { get; set; }
    public bool IsDone { get; set; }

    public bool HasCourse => !string.IsNullOrWhiteSpace(Course);
    public bool HasStudy => Study is not null;

    /// <summary>Compact display of whichever of study/short/long are set,
    /// e.g. "25m" or "25/5" or "25/5/15".</summary>
    public string DurationsLabel
    {
        get
        {
            if (Study is null && Short is null && Long is null) return string.Empty;
            if (Short is null && Long is null && Study is not null) return $"{Study}m";

            var s = Study?.ToString() ?? "–";
            var sh = Short?.ToString() ?? "–";
            if (Long is null) return $"{s}/{sh}";
            return $"{s}/{sh}/{Long}";
        }
    }
}

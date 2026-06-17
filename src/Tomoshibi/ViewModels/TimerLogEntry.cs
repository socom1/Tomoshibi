using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>One line in the timer's session log — a completed focus or break
/// block. Phase-colored in the view via the phase-to-brush converter.</summary>
public class TimerLogEntry
{
    public PomodoroPhase Phase { get; init; }
    public string TimeLabel { get; init; } = string.Empty;
    public string PhaseLabel { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

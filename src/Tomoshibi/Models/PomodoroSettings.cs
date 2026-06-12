namespace Tomoshibi.Models;

/// <summary>
/// Tunable timer lengths. Defaults are the classic 25/5/15 cycle with a
/// long break after every fourth focus round.
/// </summary>
public class PomodoroSettings
{
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int RoundsBeforeLongBreak { get; set; } = 4;

    /// <summary>When a phase completes naturally, start the next one without
    /// waiting for the user to press start.</summary>
    public bool AutoContinue { get; set; }

    /// <summary>Play the soft chime when a phase completes.</summary>
    public bool ChimeEnabled { get; set; } = true;

    /// <summary>Show a native notification when a phase completes.</summary>
    public bool NotificationsEnabled { get; set; } = true;
}

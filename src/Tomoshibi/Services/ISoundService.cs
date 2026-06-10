namespace Tomoshibi.Services;

/// <summary>
/// App sounds behind an interface so view models stay testable and the
/// "shell out to the OS player" detail stays in one place.
/// </summary>
public interface ISoundService
{
    /// <summary>Soft chime played when a pomodoro phase completes naturally.</summary>
    void PlayPhaseChime();
}

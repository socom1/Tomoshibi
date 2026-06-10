namespace Tomoshibi.Services;

/// <summary>
/// Native desktop notifications behind an interface, same shape as
/// <see cref="ISoundService"/> — the OS-specific shell-out stays in one place.
/// </summary>
public interface INotificationService
{
    /// <summary>Show a small system notification, e.g. when a phase ends
    /// while the app is in the background.</summary>
    void Notify(string title, string body);
}

using System.Linq;

namespace Tomoshibi;

/// <summary>
/// The app's version and its "what's new" notes, in one place. Bump
/// <see cref="Version"/> and refresh the lines each release; the settings page
/// reads the version, and the what's-new modal shows the notes once after an
/// update (when the running version differs from the last one launched).
/// </summary>
public static class ReleaseNotes
{
    public const string Version = "1.9.0";

    public static string VersionTag => $"v{Version}";

    public const string Title = "what's new";

    private static readonly string[] Lines =
    {
        "decks import and export as anki-compatible text files",
        "the stats page now writes your week up — a little retrospective",
        "ctrl+alt+P (⌃⌥P on mac) starts or pauses the timer from any app",
        "restore a backup from settings — the other half of the backup button",
        "windows gets native toasts; mac banners now wear the app icon",
        "while a class is on, the timer offers it as a one-click focus",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

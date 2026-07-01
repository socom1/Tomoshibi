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
    public const string Version = "1.8.0";

    public static string VersionTag => $"v{Version}";

    public const string Title = "what's new";

    private static readonly string[] Lines =
    {
        "the morning dashboard — your day at a glance",
        "embers to earn while you focus, and a theme shop to spend them in",
        "spaced-repetition flashcards with a review queue",
        "an end-of-day reflection that banks into a journal look-back",
        "deadline reminders, data exports and a first-run checklist",
        "under the hood: a verified Windows build and a much larger test net",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

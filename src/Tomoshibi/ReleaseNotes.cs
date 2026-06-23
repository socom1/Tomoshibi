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
    public const string Version = "1.5.0";

    public static string VersionTag => $"v{Version}";

    public const string Title = "what's new";

    private static readonly string[] Lines =
    {
        "a getting-started checklist that pays embers as you set up",
        "a daily focus goal with a progress bar on the dashboard",
        "a gentle after-midnight nudge to rest",
        "search todos, decks and reflections from the ⌘K palette",
        "smoother modal animations and a tidier minimal timer",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

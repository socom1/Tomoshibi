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
    public const string Version = "2.1.2";

    public static string VersionTag => $"v{Version}";

    /// <summary>Shown beside the version — this build was checked and signed off
    /// by the creator.</summary>
    public const string VerifiedBy = "verified by the creator";

    public const string Title = "what's new";

    private static readonly string[] Lines =
    {
        "the review card frames its prompt at the top and answer at the bottom instead of floating them",
        "opening a deck, a card or a subject now eases in and out instead of snapping",
        "the dashboard fills a wide screen in two columns; the timetable grid stretches to match",
        "tag chips follow the theme — no more dark chips in the light palette",
        "the pomodoro header icons line up, and the little study-videos list is gone",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

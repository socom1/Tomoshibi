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
    public const string Version = "2.1.0";

    public static string VersionTag => $"v{Version}";

    /// <summary>Shown beside the version — this build was checked and signed off
    /// by the creator.</summary>
    public const string VerifiedBy = "verified by the creator";

    public const string Title = "what's new";

    private static readonly string[] Lines =
    {
        "flashcards rebuilt to Anki level — FSRS scheduling, four grades and learning steps",
        "images, audio and video on cards, cloze deletions, and image occlusion",
        "a searchable card browser with bulk actions, per-deck options, suspend and bury",
        "review heatmap and true-retention stats, plus .apkg import of your Anki decks",
        "every deck gets its own Material icon and accent colour",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

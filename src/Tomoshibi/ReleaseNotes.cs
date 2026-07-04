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
    public const string Version = "2.0.0";

    public static string VersionTag => $"v{Version}";

    public const string Title = "what's new";

    private static readonly string[] Lines =
    {
        "tomoshibi is public now — screenshots, a license and real releases",
        "a quiet launch-time update check, with an off-switch in settings",
        "the ember wallet now carries a tamper seal — edited balances reset",
        "descenders stopped getting clipped in the CJK headings",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

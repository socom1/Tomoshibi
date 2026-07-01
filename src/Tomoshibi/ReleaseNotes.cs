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
        "ticking a task done now hits exactly the one you clicked, even twins",
        "saves swap in atomically — the backup rotates in the same breath",
        "the Windows build is now verified end to end",
        "a much larger test net under the daily reset, migrations and imports",
        "the road to v2.0 is mapped out in the roadmap",
    };

    /// <summary>The notes as one bulleted block for the modal.</summary>
    public static string Body => string.Join("\n", Lines.Select(l => $"›  {l}"));
}

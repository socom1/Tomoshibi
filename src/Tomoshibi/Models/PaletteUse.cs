using System;

namespace Tomoshibi.Models;

/// <summary>How often and how recently one palette row has been run —
/// keyed by "kind:title" in <see cref="AppState.PaletteUsage"/>. Feeds the
/// frecency ordering that floats familiar picks to the top.</summary>
public class PaletteUse
{
    public int Count { get; set; }
    public DateTimeOffset LastUsed { get; set; }
}

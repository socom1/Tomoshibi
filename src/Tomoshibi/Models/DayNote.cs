using System;

namespace Tomoshibi.Models;

/// <summary>
/// A day's bookends — the morning intention and the evening reflection —
/// banked into <see cref="AppState.Journal"/> at the midnight rollover so the
/// stats page can look back over how the days went.
/// </summary>
public class DayNote
{
    public DateOnly Date { get; set; }
    public string Intention { get; set; } = string.Empty;
    public bool IntentionKept { get; set; }
    public string Reflection { get; set; } = string.Empty;
}

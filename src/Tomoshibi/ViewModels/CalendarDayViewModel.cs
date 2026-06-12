namespace Tomoshibi.ViewModels;

/// <summary>
/// One cell on the streak calendar. Plain and immutable — the month is
/// rebuilt wholesale when it changes, so no change notification needed.
/// Tier buckets the day's sessions for the heat tint: 0 none, 1 light,
/// 2 medium, 3 strong (3+ sessions).
/// </summary>
public class CalendarDayViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Tooltip { get; init; } = string.Empty;

    /// <summary>Empty pad cell before the 1st so weekdays line up.</summary>
    public bool IsPlaceholder { get; init; }

    public bool IsToday { get; init; }
    public bool IsFuture { get; init; }

    public bool IsTier1 { get; init; }
    public bool IsTier2 { get; init; }
    public bool IsTier3 { get; init; }
}

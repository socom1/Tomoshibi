namespace Tomoshibi.Models;

/// <summary>
/// Where a card sits in the FSRS lifecycle. New cards have never been seen;
/// Learning cards are stepping through the short intraday steps; Review cards
/// are on the long spaced schedule; Relearning cards lapsed and are stepping
/// back up before rejoining Review.
/// </summary>
public enum CardState
{
    New,
    Learning,
    Review,
    Relearning
}

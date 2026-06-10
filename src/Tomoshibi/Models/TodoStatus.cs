namespace Tomoshibi.Models;

/// <summary>Where a backlog item sits in its little lifecycle. Cycled in
/// order from the row's status glyph: ○ backlog → ◐ doing → ● done.</summary>
public enum TodoStatus
{
    Backlog,
    Doing,
    Done
}

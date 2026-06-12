namespace Tomoshibi.Models;

/// <summary>
/// A page the nav sidebar can switch the main content area to.
/// Add new entries as features grow; the view model decides what
/// content view model each one resolves to.
/// </summary>
public enum Destination
{
    Today,
    Timetable,
    Todo,
    Subjects,
    Stats
}

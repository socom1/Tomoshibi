namespace Tomoshibi.Models;

/// <summary>
/// How the timetable's classes section is rendered: a flat sortable list
/// or a 7-day week grid. The user toggles between them from the section
/// header; the choice persists to <see cref="AppState"/>.
/// </summary>
public enum ClassesView
{
    Grid,
    List
}

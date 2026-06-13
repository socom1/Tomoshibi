using System.Collections.ObjectModel;

namespace Tomoshibi.ViewModels;

/// <summary>One day in the dashboard's 7-day agenda — its label and the
/// classes / due items / exams that fall on it.</summary>
public class AgendaDayViewModel
{
    public string DayLabel { get; init; } = string.Empty;
    public bool IsToday { get; init; }
    public ObservableCollection<AgendaEntryViewModel> Entries { get; } = new();
}

/// <summary>A single agenda line — a class, a due ticket, or an exam. The
/// kind drives the colour (blue / amber / sakura).</summary>
public class AgendaEntryViewModel
{
    public string Text { get; init; } = string.Empty;
    public bool IsClass { get; init; }
    public bool IsDue { get; init; }
    public bool IsExam { get; init; }
}

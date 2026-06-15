using System;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>One day's look-back row in the stats journal: the morning
/// intention (and whether it was kept) and the evening reflection.</summary>
public class JournalEntryViewModel : ViewModelBase
{
    public string DateLabel { get; }
    public bool IsToday { get; }

    public string Intention { get; }
    public bool HasIntention { get; }
    public bool Kept { get; }

    public string Reflection { get; }
    public bool HasReflection { get; }

    public JournalEntryViewModel(DayNote note, bool isToday)
    {
        IsToday = isToday;
        DateLabel = isToday ? "today" : $"{note.Date:ddd MMM d}".ToLowerInvariant();

        Intention = note.Intention;
        HasIntention = !string.IsNullOrWhiteSpace(note.Intention);
        Kept = note.IntentionKept;

        Reflection = note.Reflection;
        HasReflection = !string.IsNullOrWhiteSpace(note.Reflection);
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>One day's look-back row in the stats journal: the morning
/// intention (and whether it was kept) and the evening reflection.</summary>
public partial class JournalEntryViewModel : ViewModelBase
{
    /// <summary>The day this row stands for — used to find it again when the
    /// command palette reveals a specific reflection.</summary>
    public DateOnly Date { get; }

    public string DateLabel { get; }
    public bool IsToday { get; }

    public string Intention { get; }
    public bool HasIntention { get; }
    public bool Kept { get; }

    public string Reflection { get; }
    public bool HasReflection { get; }

    /// <summary>Set when the palette jumps to this entry, so the view can flag
    /// the row the user was looking for. Cleared on the next reveal.</summary>
    [ObservableProperty] private bool _highlighted;

    public JournalEntryViewModel(DayNote note, bool isToday)
    {
        Date = note.Date;
        IsToday = isToday;
        DateLabel = isToday ? "today" : $"{note.Date:ddd MMM d}".ToLowerInvariant();

        Intention = note.Intention;
        HasIntention = !string.IsNullOrWhiteSpace(note.Intention);
        Kept = note.IntentionKept;

        Reflection = note.Reflection;
        HasReflection = !string.IsNullOrWhiteSpace(note.Reflection);
    }
}

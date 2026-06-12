using System.Collections.ObjectModel;

namespace Tomoshibi.ViewModels;

/// <summary>One term's worth of subjects on the master list, with its own
/// header ("year 2 · semester 1") and term average.</summary>
public class TermGroupViewModel
{
    public string Header { get; init; } = string.Empty;
    public string AverageLabel { get; init; } = string.Empty;
    public bool HasAverage { get; init; }
    public ObservableCollection<SubjectViewModel> Items { get; } = new();
}

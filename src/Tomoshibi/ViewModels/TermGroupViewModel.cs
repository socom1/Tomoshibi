using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tomoshibi.ViewModels;

/// <summary>One term's worth of subjects on the master list, with its own
/// header ("year 2 · semester 1") and term average. Past terms fold down to
/// the header row so the current semester keeps the page; the owner hears
/// about toggles so the choice survives the next rebuild.</summary>
public partial class TermGroupViewModel : ViewModelBase
{
    private readonly Action<bool>? _onExpandedChanged;

    public TermGroupViewModel(Action<bool>? onExpandedChanged = null)
    {
        _onExpandedChanged = onExpandedChanged;
    }

    public string Header { get; init; } = string.Empty;
    public string AverageLabel { get; init; } = string.Empty;
    public bool HasAverage { get; init; }
    public ObservableCollection<SubjectViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chevron))]
    [NotifyPropertyChangedFor(nameof(SummaryLabel))]
    private bool _isExpanded = true;

    public string Chevron => IsExpanded ? "▾" : "▸";

    /// <summary>The right-hand end of the header: just the average while the
    /// term is open; average + subject count once folded, so a collapsed
    /// semester still tells its story.</summary>
    public string SummaryLabel
    {
        get
        {
            if (IsExpanded)
                return AverageLabel;

            var count = Items.Count == 1 ? "1 subject" : $"{Items.Count} subjects";
            return HasAverage ? $"{AverageLabel} · {count}" : count;
        }
    }

    partial void OnIsExpandedChanged(bool value) => _onExpandedChanged?.Invoke(value);

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

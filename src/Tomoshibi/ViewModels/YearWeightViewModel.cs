using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tomoshibi.ViewModels;

/// <summary>One row in the degree-weighting editor: a year, its average so
/// far, and the editable weight it carries toward the final degree mark.</summary>
public partial class YearWeightViewModel : ViewModelBase
{
    private readonly Action<int, double> _weightChanged;

    public int Year { get; }
    public string YearLabel => $"year {Year}";
    public string AverageLabel { get; }

    [ObservableProperty]
    private decimal? _weight;

    public YearWeightViewModel(int year, string averageLabel, double weight, Action<int, double> weightChanged)
    {
        Year = year;
        AverageLabel = averageLabel;
        _weightChanged = weightChanged;
        _weight = weight > 0 ? (decimal)weight : null;
    }

    partial void OnWeightChanged(decimal? value) =>
        _weightChanged(Year, value is { } w ? (double)w : 0);
}

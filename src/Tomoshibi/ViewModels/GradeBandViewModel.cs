using System;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>One editable row of the custom grade scale — min %, label, and
/// optional GPA points. Edits write straight onto the model and notify the
/// owner so the whole page re-grades.</summary>
public class GradeBandViewModel : ViewModelBase
{
    public GradeBand Model { get; }
    private readonly Action _changed;

    public GradeBandViewModel(GradeBand model, Action changed)
    {
        Model = model;
        _changed = changed;
    }

    public decimal Min
    {
        get => (decimal)Model.MinPercent;
        set
        {
            var v = Math.Clamp((double)value, 0, 100);
            if (Model.MinPercent == v) return;
            Model.MinPercent = v;
            OnPropertyChanged();
            _changed();
        }
    }

    public string Label
    {
        get => Model.Label;
        set
        {
            if (Model.Label == value) return;
            Model.Label = value ?? string.Empty;
            OnPropertyChanged();
            _changed();
        }
    }

    public decimal Points
    {
        get => (decimal)Model.Points;
        set
        {
            var v = Math.Max(0, (double)value);
            if (Model.Points == v) return;
            Model.Points = v;
            OnPropertyChanged();
            _changed();
        }
    }
}

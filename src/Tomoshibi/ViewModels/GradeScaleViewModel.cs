using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// How percentages become grades: which scale is in force, and — when it's the
/// custom one — the editable bands behind it.
///
/// Lifted out of SubjectsViewModel, which had grown to carry this alongside
/// the subject list, the add/edit form, the detail page, the study goals and
/// the degree projection. This part answers to nothing else on that page, so
/// it reads better on its own; the page keeps it as <c>Scale</c>.
///
/// Every change here re-grades the whole app, so the owner hands in a
/// <paramref name="regrade"/> callback rather than this reaching back into the
/// subject rows itself.
/// </summary>
public partial class GradeScaleViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _regrade;

    public GradeScaleViewModel(AppState state, Action regrade)
    {
        _state = state;
        _regrade = regrade;

        _selected = Options.FirstOrDefault(o => o.Kind == _state.GradeScale) ?? Options[0];

        // Seeding the starter scale and pointing the engine at it used to
        // happen here. Both are app-wide effects and neither is this page's to
        // cause — building a view model shouldn't change how every grade in the
        // app is labelled. MainWindowViewModel does it once, where state is
        // assembled; the engine already holds the live list this page edits.
        RebuildBands();
    }

    public IReadOnlyList<ScaleOption> Options { get; } = new[]
    {
        new ScaleOption(GradeScaleKind.UsGpa, "us 4.0"),
        new ScaleOption(GradeScaleKind.UkHonours, "uk honours"),
        new ScaleOption(GradeScaleKind.Ects, "ects"),
        new ScaleOption(GradeScaleKind.Percentage, "percentage"),
        new ScaleOption(GradeScaleKind.Custom, "custom"),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    private ScaleOption _selected;

    /// <summary>True when the custom scale is selected — gates the band editor.</summary>
    public bool IsCustom => Selected?.Kind == GradeScaleKind.Custom;

    /// <summary>The editable rows of the custom grade scale.</summary>
    public ObservableCollection<GradeBandViewModel> Bands { get; } = new();

    partial void OnSelectedChanged(ScaleOption value)
    {
        if (_state.GradeScale == value.Kind)
            return;

        _state.GradeScale = value.Kind;
        _regrade();
    }

    private void RebuildBands()
    {
        Bands.Clear();
        foreach (var band in _state.CustomGradeBands.OrderByDescending(b => b.MinPercent))
            Bands.Add(new GradeBandViewModel(band, OnBandChanged));
    }

    /// <summary>A band's min/label/points changed — re-grade everything against
    /// the new scale.</summary>
    private void OnBandChanged()
    {
        // No re-pointing needed: the engine holds this exact list, and every
        // edit path here mutates it in place rather than replacing it. The
        // assignment that used to sit here had been a no-op since the day the
        // engine started taking the live reference.
        _regrade();
    }

    [RelayCommand]
    private void AddBand()
    {
        var band = new GradeBand { MinPercent = 0, Label = "new", Points = 0 };
        _state.CustomGradeBands.Add(band);
        Bands.Add(new GradeBandViewModel(band, OnBandChanged));
        OnBandChanged();
    }

    [RelayCommand]
    private void RemoveBand(GradeBandViewModel? row)
    {
        if (row is null) return;
        _state.CustomGradeBands.Remove(row.Model);
        Bands.Remove(row);
        OnBandChanged();
    }

    [RelayCommand]
    private void ResetBands()
    {
        _state.CustomGradeBands.Clear();
        _state.CustomGradeBands.AddRange(GradeScale.DefaultCustomBands());
        RebuildBands();
        OnBandChanged();
    }
}

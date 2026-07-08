using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Edits one deck's scheduling options in a modal. Learning/relearning steps
/// are typed as space-separated minutes ("1 10 30"); the retention is a
/// percentage the slider drives. Every change writes straight to the model and
/// saves.
/// </summary>
public partial class DeckOptionsViewModel : ViewModelBase
{
    private readonly DeckOptions _options;
    private readonly Action _changed;
    private bool _loading;

    public string DeckName { get; }

    [ObservableProperty] private int _newPerDay;
    [ObservableProperty] private int _reviewsPerDay;

    /// <summary>Desired retention as a whole percentage (70–97) for the slider.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RetentionLabel))]
    private double _retentionPercent;

    [ObservableProperty] private string _learningStepsText = string.Empty;
    [ObservableProperty] private string _relearningStepsText = string.Empty;
    [ObservableProperty] private int _leechThreshold;
    [ObservableProperty] private bool _suspendLeeches;
    [ObservableProperty] private int _autoRevealSeconds;
    [ObservableProperty] private bool _burySiblings;

    public string RetentionLabel => $"{RetentionPercent:0}% recall target";

    public DeckOptionsViewModel(Deck deck, Action changed)
    {
        _options = deck.Options;
        _changed = changed;
        DeckName = deck.Name;

        _loading = true;
        _newPerDay = _options.NewPerDay;
        _reviewsPerDay = _options.ReviewsPerDay;
        _retentionPercent = Math.Round(_options.DesiredRetention * 100);
        _learningStepsText = string.Join(" ", _options.LearningStepsMinutes);
        _relearningStepsText = string.Join(" ", _options.RelearningStepsMinutes);
        _leechThreshold = _options.LeechThreshold;
        _suspendLeeches = _options.SuspendLeeches;
        _autoRevealSeconds = _options.AutoRevealSeconds;
        _burySiblings = _options.BurySiblings;
        _loading = false;
    }

    partial void OnNewPerDayChanged(int value) => Save(() => _options.NewPerDay = Math.Max(0, value));
    partial void OnReviewsPerDayChanged(int value) => Save(() => _options.ReviewsPerDay = Math.Max(0, value));
    partial void OnRetentionPercentChanged(double value) =>
        Save(() => _options.DesiredRetention = Math.Clamp(value / 100.0, 0.70, 0.97));
    partial void OnLeechThresholdChanged(int value) => Save(() => _options.LeechThreshold = Math.Max(1, value));
    partial void OnSuspendLeechesChanged(bool value) => Save(() => _options.SuspendLeeches = value);
    partial void OnAutoRevealSecondsChanged(int value) => Save(() => _options.AutoRevealSeconds = Math.Max(0, value));
    partial void OnBurySiblingsChanged(bool value) => Save(() => _options.BurySiblings = value);

    partial void OnLearningStepsTextChanged(string value) =>
        Save(() => { var s = ParseSteps(value); if (s.Count > 0) _options.LearningStepsMinutes = s; });

    partial void OnRelearningStepsTextChanged(string value) =>
        Save(() => _options.RelearningStepsMinutes = ParseSteps(value));

    private void Save(Action apply)
    {
        if (_loading) return;
        apply();
        _changed();
    }

    /// <summary>Parse "1 10 30" (or comma-separated) into positive minutes,
    /// ignoring anything that isn't a number.</summary>
    private static List<int> ParseSteps(string text)
        => text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(p => int.TryParse(p, out var n) ? n : -1)
               .Where(n => n > 0)
               .ToList();
}

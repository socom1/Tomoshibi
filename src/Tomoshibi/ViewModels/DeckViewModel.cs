using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>One deck: the card-list editor plus the live card / due counts the
/// review list and dashboard read.</summary>
public partial class DeckViewModel : ViewModelBase
{
    private readonly Action _changed;
    public Deck Model { get; }

    public ObservableCollection<FlashcardViewModel> Cards { get; } = new();

    [ObservableProperty] private int _cardCount;
    [ObservableProperty] private int _dueCount;
    [ObservableProperty] private bool _hasDue;
    [ObservableProperty] private string _countsLabel = string.Empty;

    /// <summary>A █/░ meter of how much of the deck is due right now.</summary>
    [ObservableProperty] private string _dueBar = string.Empty;

    // ---- New-card form ----
    [ObservableProperty] private string _newCardFront = string.Empty;
    [ObservableProperty] private string _newCardBack = string.Empty;

    public DeckViewModel(Deck model, Action changed)
    {
        Model = model;
        _changed = changed;

        foreach (var card in model.Cards)
            Cards.Add(new FlashcardViewModel(card, changed));

        RefreshCounts();
    }

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public string Course
    {
        get => Model.Course ?? string.Empty;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (Model.Course == v) return;
            Model.Course = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCourse));
            _changed();
        }
    }

    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);
    public bool HasCards => Model.Cards.Count > 0;

    [RelayCommand]
    private void AddCard()
    {
        var front = NewCardFront?.Trim();
        var back = NewCardBack?.Trim();
        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(back))
            return;

        var card = new Flashcard
        {
            Front = front,
            Back = back,
            Due = DateOnly.FromDateTime(DateTime.Now) // new cards are due now
        };
        Model.Cards.Add(card);
        Cards.Add(new FlashcardViewModel(card, _changed));

        NewCardFront = string.Empty;
        NewCardBack = string.Empty;

        OnPropertyChanged(nameof(HasCards));
        RefreshCounts();
        _changed();
    }

    [RelayCommand]
    private void RemoveCard(FlashcardViewModel? row)
    {
        if (row is null) return;

        Model.Cards.Remove(row.Model);
        Cards.Remove(row);

        OnPropertyChanged(nameof(HasCards));
        RefreshCounts();
        _changed();
    }

    /// <summary>Recompute the card / due counts after an edit or a review.</summary>
    public void RefreshCounts()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        CardCount = Model.Cards.Count;
        DueCount = Model.Cards.Count(c => ReviewScheduler.IsDue(c, today));
        HasDue = DueCount > 0;
        CountsLabel = CardCount == 0
            ? "no cards yet"
            : DueCount > 0
                ? $"{DueCount} due · {CardCount} cards"
                : $"caught up · {CardCount} cards";
        DueBar = ReviewViewModel.AsciiBar(DueCount, Math.Max(CardCount, 1), 10);
    }
}

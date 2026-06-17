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
/// The 復習 · review destination — spaced-repetition flashcards. Decks live in
/// state; the cards due today surface here (and on the dashboard). Working a
/// card forward schedules its next review and earns a small ember spark for
/// the cards you get through, tying recall into the same economy as focus.
/// </summary>
public partial class ReviewViewModel : ViewModelBase
{
    /// <summary>Embers earned per card cleared (not for "again" repeats).</summary>
    private const int EmberPerCard = 2;

    private readonly AppState _state;
    private readonly Action _save;
    private readonly WalletViewModel _wallet;

    public ObservableCollection<DeckViewModel> Decks { get; } = new();

    [ObservableProperty] private bool _hasDecks;
    [ObservableProperty] private bool _hasDue;
    [ObservableProperty] private int _totalDue;
    [ObservableProperty] private int _totalCards;
    [ObservableProperty] private string _summaryLabel = string.Empty;

    // ---- Deck editor (master → detail) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeckOpen))]
    [NotifyCanExecuteChangedFor(nameof(CloseDeckCommand))]
    private DeckViewModel? _selectedDeck;

    public bool IsDeckOpen => SelectedDeck is not null;

    // ---- New-deck form ----
    [ObservableProperty] private string _newDeckName = string.Empty;
    [ObservableProperty] private string _newDeckCourse = string.Empty;

    // ---- Review session ----
    private readonly List<Flashcard> _queue = new();
    private DateOnly _sessionDate;
    private int _sessionTotal;
    private int _passed;
    private int _embersEarned;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EndReviewCommand))]
    private bool _isReviewing;

    [ObservableProperty] private bool _isFlipped;
    [ObservableProperty] private bool _isSessionDone;
    [ObservableProperty] private string _cardFront = string.Empty;
    [ObservableProperty] private string _cardBack = string.Empty;
    [ObservableProperty] private string _reviewScopeLabel = string.Empty;
    [ObservableProperty] private string _progressLabel = string.Empty;
    [ObservableProperty] private string _progressBar = string.Empty;
    [ObservableProperty] private string _sessionDoneLabel = string.Empty;

    public ReviewViewModel(AppState state, Action save, WalletViewModel wallet)
    {
        _state = state;
        _save = save;
        _wallet = wallet;

        foreach (var deck in _state.Decks)
            Decks.Add(new DeckViewModel(deck, OnDeckChanged));

        Refresh();
    }

    /// <summary>Re-read every deck's counts and the rollup. Called on landing
    /// here and after a review session changes what's due.</summary>
    public void Refresh()
    {
        foreach (var deck in Decks)
            deck.RefreshCounts();

        HasDecks = Decks.Count > 0;
        TotalCards = Decks.Sum(d => d.CardCount);
        TotalDue = Decks.Sum(d => d.DueCount);
        HasDue = TotalDue > 0;

        SummaryLabel = !HasDecks
            ? "make a deck and add cards to start reviewing"
            : TotalDue > 0
                ? $"{TotalDue} cards due across {Decks.Count(d => d.DueCount > 0)} decks"
                : "nothing due — you're caught up";
    }

    private void OnDeckChanged()
    {
        Refresh();
        _save();
    }

    // ---- Deck management ----

    [RelayCommand]
    private void AddDeck()
    {
        var name = NewDeckName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var deck = new Deck
        {
            Name = name,
            Course = string.IsNullOrWhiteSpace(NewDeckCourse) ? null : NewDeckCourse.Trim()
        };
        _state.Decks.Add(deck);
        Decks.Add(new DeckViewModel(deck, OnDeckChanged));

        NewDeckName = string.Empty;
        NewDeckCourse = string.Empty;
        Refresh();
        _save();
    }

    [RelayCommand]
    private void OpenDeck(DeckViewModel? deck)
    {
        if (deck is not null)
            SelectedDeck = deck;
    }

    [RelayCommand(CanExecute = nameof(CanCloseDeck))]
    private void CloseDeck() => SelectedDeck = null;

    private bool CanCloseDeck() => IsDeckOpen;

    [RelayCommand]
    private void DeleteDeck(DeckViewModel? deck)
    {
        if (deck is null) return;

        if (deck == SelectedDeck)
            SelectedDeck = null;

        _state.Decks.Remove(deck.Model);
        Decks.Remove(deck);
        Refresh();
        _save();
    }

    // ---- Review session ----

    [RelayCommand]
    private void ReviewAll() =>
        StartReview(_state.Decks.SelectMany(d => d.Cards), "all decks");

    [RelayCommand]
    private void ReviewDeck(DeckViewModel? deck)
    {
        if (deck is not null)
            StartReview(deck.Model.Cards, deck.Model.Name);
    }

    private void StartReview(IEnumerable<Flashcard> cards, string scope)
    {
        _sessionDate = DateOnly.FromDateTime(DateTime.Now);

        _queue.Clear();
        _queue.AddRange(cards.Where(c => ReviewScheduler.IsDue(c, _sessionDate)));
        if (_queue.Count == 0)
            return;

        Shuffle(_queue); // don't always drill a deck front-to-back

        _sessionTotal = _queue.Count;
        _passed = 0;
        _embersEarned = 0;
        ReviewScopeLabel = scope;
        IsSessionDone = false;
        IsReviewing = true;
        ShowNext();
    }

    private void ShowNext()
    {
        IsFlipped = false;

        if (_queue.Count == 0)
        {
            IsSessionDone = true;
            CardFront = string.Empty;
            CardBack = string.Empty;
            SessionDoneLabel = _embersEarned > 0
                ? $"{_passed} reviewed · +{_embersEarned} 火種"
                : $"{_passed} reviewed";
            return;
        }

        var card = _queue[0];
        CardFront = card.Front;
        CardBack = card.Back;
        ProgressLabel = $"{Math.Min(_passed + 1, _sessionTotal)} / {_sessionTotal} · {ReviewScopeLabel}";
        ProgressBar = AsciiBar(_passed, _sessionTotal, 24);
    }

    /// <summary>A fixed-width █/░ meter — the terminal-style progress readout.</summary>
    internal static string AsciiBar(int done, int total, int width)
    {
        if (total <= 0) return new string('░', width);
        var filled = Math.Clamp((int)Math.Round((double)done / total * width), 0, width);
        return new string('█', filled) + new string('░', width - filled);
    }

    [RelayCommand]
    private void Flip() => IsFlipped = true;

    [RelayCommand]
    private void GradeAgain() => Grade(ReviewGrade.Again);

    [RelayCommand]
    private void GradeGood() => Grade(ReviewGrade.Good);

    [RelayCommand]
    private void GradeEasy() => Grade(ReviewGrade.Easy);

    private void Grade(ReviewGrade grade)
    {
        if (!IsReviewing || IsSessionDone || !IsFlipped || _queue.Count == 0)
            return;

        var card = _queue[0];
        _queue.RemoveAt(0);
        ReviewScheduler.Apply(card, grade, _sessionDate);

        if (grade == ReviewGrade.Again)
        {
            _queue.Add(card); // come back to it before the session ends
        }
        else
        {
            // Cleared it — a small spark, once. Re-queued "agains" don't pay
            // again, so the reward can't be farmed by toggling.
            _passed++;
            _wallet.Add(EmberPerCard);
            _embersEarned += EmberPerCard;
        }

        _save();
        ShowNext();
    }

    [RelayCommand(CanExecute = nameof(CanEndReview))]
    private void EndReview()
    {
        IsReviewing = false;
        IsSessionDone = false;
        _queue.Clear();
        Refresh(); // the cards just reviewed are no longer due
    }

    private bool CanEndReview() => IsReviewing;

    private static void Shuffle(List<Flashcard> list)
    {
        var rng = Random.Shared;
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

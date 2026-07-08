using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The 復習 · review destination — spaced-repetition flashcards on FSRS. Decks
/// live in state; the cards due now surface here (and on the dashboard).
/// Grading a card advances its FSRS schedule, appends a line to the review log,
/// and earns a small ember spark the first time each card is cleared, tying
/// recall into the same economy as focus.
/// </summary>
public partial class ReviewViewModel : ViewModelBase
{
    /// <summary>Embers earned the first time a card is cleared in a session
    /// (not for "again" repeats or re-shows).</summary>
    private const int EmberPerCard = 2;

    private readonly AppState _state;
    private readonly Action _save;
    private readonly WalletViewModel _wallet;
    private readonly IReviewLogService _log;
    private readonly MediaStore _media;

    public ObservableCollection<DeckViewModel> Decks { get; } = new();

    /// <summary>The card browser, shown as a mode over the deck list.</summary>
    public CardBrowserViewModel Browser { get; }

    [ObservableProperty] private bool _hasDecks;
    [ObservableProperty] private bool _hasDue;
    [ObservableProperty] private int _totalDue;
    [ObservableProperty] private int _totalCards;
    [ObservableProperty] private string _summaryLabel = string.Empty;

    // ---- Card browser + deck options (modes/modals over the deck list) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDeckArea))]
    private bool _isBrowserOpen;

    /// <summary>The deck list / editor area shows only when not reviewing and
    /// not in the browser.</summary>
    public bool ShowDeckArea => !IsReviewing && !IsBrowserOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeckOptionsOpen))]
    private DeckOptionsViewModel? _deckOptionsEditor;

    public bool IsDeckOptionsOpen => DeckOptionsEditor is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOcclusionEditorOpen))]
    private OcclusionEditorViewModel? _occlusionEditor;

    public bool IsOcclusionEditorOpen => OcclusionEditor is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApkgImportOpen))]
    private ApkgImportViewModel? _apkgImport;

    public bool IsApkgImportOpen => ApkgImport is not null;

    // ---- Auto-reveal (speed focus) ----
    private readonly DispatcherTimer _autoRevealTimer;
    private int _autoRevealRemaining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutoReveal))]
    private string _autoRevealLabel = string.Empty;

    public bool HasAutoReveal => !string.IsNullOrEmpty(AutoRevealLabel);

    // ---- Deck editor (master → detail) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeckOpen))]
    [NotifyCanExecuteChangedFor(nameof(CloseDeckCommand))]
    private DeckViewModel? _selectedDeck;

    public bool IsDeckOpen => SelectedDeck is not null;

    // ---- New-deck form ----
    [ObservableProperty] private string _newDeckName = string.Empty;
    [ObservableProperty] private string _newDeckCourse = string.Empty;

    /// <summary>What the last deck import did — shown under the page header.</summary>
    [ObservableProperty] private string _importSummary = string.Empty;

    // ---- Review session ----
    private readonly List<ScheduledCard> _queue = new();
    private readonly HashSet<Guid> _paid = new();
    private DateTime _sessionNow;
    private int _sessionTotal;
    private int _reviewed;
    private int _embersEarned;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EndReviewCommand))]
    [NotifyPropertyChangedFor(nameof(ShowDeckArea))]
    private bool _isReviewing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFront))]
    [NotifyPropertyChangedFor(nameof(ShowDivider))]
    [NotifyPropertyChangedFor(nameof(CurrentSide))]
    private bool _isFlipped;

    [ObservableProperty] private bool _isSessionDone;

    // Image-occlusion cards render an image + masks instead of text.
    [ObservableProperty] private Note? _currentNote;
    [ObservableProperty] private bool _isOcclusionCard;

    /// <summary>The face the occlusion view should draw — flips with the card.</summary>
    public CardSide CurrentSide => IsFlipped ? CardSide.Back : CardSide.Front;

    /// <summary>Whether the prompt stays visible. Always before flipping; after
    /// flipping it depends on the "replace prompt on reveal" setting.</summary>
    public bool ShowFront => !(IsFlipped && _state.ReviewHideFrontOnReveal);

    /// <summary>The rule between prompt and answer only makes sense when both
    /// are on screen.</summary>
    public bool ShowDivider => IsFlipped && ShowFront;
    [ObservableProperty] private string _cardFrontSource = string.Empty;
    [ObservableProperty] private string _cardBackSource = string.Empty;
    [ObservableProperty] private int _cardClozeOrd;
    [ObservableProperty] private string _reviewScopeLabel = string.Empty;
    [ObservableProperty] private string _progressLabel = string.Empty;
    [ObservableProperty] private string _progressBar = string.Empty;

    /// <summary>The current card's deck colour — the session tints its progress
    /// bar to match the deck being studied.</summary>
    [ObservableProperty] private IBrush _sessionAccent = DeckViewModel.BrushForColor(null);
    [ObservableProperty] private string _sessionDoneLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLeechNotice))]
    private string _leechNotice = string.Empty;

    public bool HasLeechNotice => !string.IsNullOrEmpty(LeechNotice);

    // Interval previews on the four grade buttons.
    [ObservableProperty] private string _againLabel = string.Empty;
    [ObservableProperty] private string _hardLabel = string.Empty;
    [ObservableProperty] private string _goodLabel = string.Empty;
    [ObservableProperty] private string _easyLabel = string.Empty;

    public ReviewViewModel(AppState state, Action save, WalletViewModel wallet,
                           IReviewLogService log, MediaStore media)
    {
        _state = state;
        _save = save;
        _wallet = wallet;
        _log = log;
        _media = media;

        foreach (var deck in _state.Decks)
            Decks.Add(new DeckViewModel(deck, OnDeckChanged, _media, OpenOcclusion));

        Browser = new CardBrowserViewModel(_state, _save, RebuildDecks, _media);

        _autoRevealTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoRevealTimer.Tick += OnAutoRevealTick;

        Refresh();
    }

    /// <summary>Rebuild the deck rows from state — used after the browser moves
    /// or deletes cards behind the deck list.</summary>
    private void RebuildDecks()
    {
        Decks.Clear();
        foreach (var deck in _state.Decks)
            Decks.Add(new DeckViewModel(deck, OnDeckChanged, _media, OpenOcclusion));
        Refresh();
    }

    private void OpenOcclusion(Note note)
        => OcclusionEditor = new OcclusionEditorViewModel(note, _save, _media);

    [RelayCommand]
    private void CloseOcclusionEditor()
    {
        OcclusionEditor = null;
        RebuildDecks(); // mask edits changed the cards
    }

    /// <summary>Open the .apkg import dialog with a chosen file (called by the
    /// view after the file picker returns).</summary>
    public void OpenApkgImport(string path)
    {
        var vm = new ApkgImportViewModel(_state, _media, _save, RebuildDecks);
        vm.SetFile(path);
        ApkgImport = vm;
    }

    [RelayCommand]
    private void CloseApkgImport() => ApkgImport = null;

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

        var reviewedToday = _log.All().Count(e =>
            DateOnly.FromDateTime(e.Timestamp) == DateOnly.FromDateTime(DateTime.Now));
        var doneNote = reviewedToday > 0 ? $" · {reviewedToday} reviewed today" : string.Empty;

        SummaryLabel = !HasDecks
            ? "make a deck and add cards to start reviewing"
            : TotalDue > 0
                ? $"{TotalDue} cards due across {Decks.Count(d => d.DueCount > 0)} decks{doneNote}"
                : $"nothing due — you're caught up{doneNote}";
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
        Decks.Add(new DeckViewModel(deck, OnDeckChanged, _media, OpenOcclusion));

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

    // ---- Card browser + deck options ----

    [RelayCommand]
    private void OpenBrowser()
    {
        SelectedDeck = null;
        Browser.Reload();
        IsBrowserOpen = true;
    }

    [RelayCommand]
    private void CloseBrowser()
    {
        IsBrowserOpen = false;
        RebuildDecks(); // browser edits may have changed counts
    }

    [RelayCommand]
    private void OpenDeckOptions(DeckViewModel? deck)
    {
        if (deck is not null)
            DeckOptionsEditor = new DeckOptionsViewModel(deck.Model, _save);
    }

    [RelayCommand]
    private void CloseDeckOptions() => DeckOptionsEditor = null;

    // ---- Deck import / export (text/CSV, as a new deck) ----

    /// <summary>Bring a CSV/TSV export in as a brand-new deck named after the
    /// file (Anki front/back text works too). Every card arrives due.</summary>
    public void ImportDeck(string name, string text)
    {
        var notes = CsvCards.Import(text);
        if (notes.Count == 0)
        {
            ImportSummary = "no cards found in that file";
            return;
        }

        var deck = new Deck
        {
            Name = string.IsNullOrWhiteSpace(name) ? "imported deck" : name.Trim()
        };
        deck.Notes.AddRange(notes);

        _state.Decks.Add(deck);
        Decks.Add(new DeckViewModel(deck, OnDeckChanged, _media, OpenOcclusion));

        ImportSummary = $"imported {notes.Count} cards into “{deck.Name}”";
        Refresh();
        _save();
    }

    /// <summary>The open deck as CSV for the export picker.</summary>
    public string BuildDeckTsv(DeckViewModel deck) => CsvCards.Export(deck.Model.Notes);

    // ---- Review session ----

    [RelayCommand]
    private void ReviewAll() => StartReview(_state.Decks, "all decks");

    [RelayCommand]
    private void ReviewDeck(DeckViewModel? deck)
    {
        if (deck is not null)
            StartReview(new[] { deck.Model }, deck.Model.Name);
    }

    private void StartReview(IEnumerable<Deck> decks, string scope)
    {
        _sessionNow = DateTime.Now;
        var today = DateOnly.FromDateTime(_sessionNow);

        var queue = Scheduler.BuildQueue(
            decks, _sessionNow,
            deckId => _log.CountToday(deckId, CardState.Review, today),
            deckId => _log.CountToday(deckId, CardState.New, today),
            Random.Shared);

        if (queue.Count == 0)
            return;

        _queue.Clear();
        _queue.AddRange(queue);
        _paid.Clear();

        _sessionTotal = _queue.Select(x => x.Card.Id).Distinct().Count();
        _reviewed = 0;
        _embersEarned = 0;
        LeechNotice = string.Empty;
        ReviewScopeLabel = scope;
        IsSessionDone = false;
        IsReviewing = true;
        ShowNext();
    }

    private void ShowNext()
    {
        IsFlipped = false;
        StopAutoReveal();

        if (_queue.Count == 0)
        {
            IsSessionDone = true;
            CardFrontSource = string.Empty;
            CardBackSource = string.Empty;
            SessionDoneLabel = _embersEarned > 0
                ? $"{_reviewed} reviewed · +{_embersEarned} 火種"
                : $"{_reviewed} reviewed";
            return;
        }

        var (deck, note, card) = _queue[0];
        SessionAccent = DeckViewModel.BrushForColor(deck.Color);
        CurrentNote = note;
        IsOcclusionCard = note.Type == NoteType.ImageOcclusion;
        var (front, back, clozeOrd) = RenderSources(note, card);
        CardClozeOrd = clozeOrd;
        CardFrontSource = front;
        CardBackSource = back;
        ProgressLabel = $"{Math.Min(_reviewed + 1, _sessionTotal)} / {_sessionTotal} · {ReviewScopeLabel}";
        ProgressBar = AsciiBar(_reviewed, _sessionTotal, 24);
        UpdatePreview();
        StartAutoReveal(deck.Options.AutoRevealSeconds);
    }

    // ---- Auto-reveal (speed focus) ----

    private void StartAutoReveal(int seconds)
    {
        if (seconds <= 0) return;
        _autoRevealRemaining = seconds;
        AutoRevealLabel = $"reveal in {_autoRevealRemaining}s";
        _autoRevealTimer.Start();
    }

    private void StopAutoReveal()
    {
        _autoRevealTimer.Stop();
        AutoRevealLabel = string.Empty;
    }

    private void OnAutoRevealTick(object? sender, EventArgs e)
    {
        _autoRevealRemaining--;
        if (_autoRevealRemaining <= 0)
        {
            StopAutoReveal();
            if (IsReviewing && !IsSessionDone && !IsFlipped)
                Flip();
        }
        else
        {
            AutoRevealLabel = $"reveal in {_autoRevealRemaining}s";
        }
    }

    private void UpdatePreview()
    {
        if (_queue.Count == 0) return;
        var (deck, _, card) = _queue[0];
        var preview = Scheduler.Preview(deck.Options, card, _sessionNow);
        AgainLabel = Scheduler.FormatInterval(preview[0].Interval);
        HardLabel = Scheduler.FormatInterval(preview[1].Interval);
        GoodLabel = Scheduler.FormatInterval(preview[2].Interval);
        EasyLabel = Scheduler.FormatInterval(preview[3].Interval);
    }

    /// <summary>Raw field sources + cloze ord for rendering a card. The renderer
    /// interprets cloze/media/emphasis; here we just pick which fields make the
    /// front and back for the note type. Cloze cards show the same field on both
    /// faces — the renderer hides the target on the front and reveals it on the
    /// back — plus the optional back-extra field.</summary>
    private static (string front, string back, int clozeOrd) RenderSources(Note note, Card card)
    {
        var f0 = note.Fields.Count > 0 ? note.Fields[0] : string.Empty;
        var f1 = note.Fields.Count > 1 ? note.Fields[1] : string.Empty;

        return note.Type switch
        {
            // Occlusion: the image + masks render separately; the text fields are
            // the optional header (front) and back-extra (back). Ord selects the mask.
            NoteType.ImageOcclusion => (f1, note.Fields.Count > 2 ? note.Fields[2] : string.Empty, card.Ord),
            NoteType.Cloze => (f0, string.IsNullOrWhiteSpace(f1) ? f0 : f0 + "\n" + f1, card.Ord),
            NoteType.BasicReversed when card.Ord == 1 => (f1, f0, 0),
            _ => (f0, f1, 0)
        };
    }

    /// <summary>A fixed-width █/░ meter — the terminal-style progress readout.</summary>
    internal static string AsciiBar(int done, int total, int width)
    {
        if (total <= 0) return new string('░', width);
        var filled = Math.Clamp((int)Math.Round((double)done / total * width), 0, width);
        return new string('█', filled) + new string('░', width - filled);
    }

    [RelayCommand]
    private void Flip()
    {
        StopAutoReveal();
        IsFlipped = true;
    }

    [RelayCommand]
    private void GradeAgain() => Grade(ReviewGrade.Again);

    [RelayCommand]
    private void GradeHard() => Grade(ReviewGrade.Hard);

    [RelayCommand]
    private void GradeGood() => Grade(ReviewGrade.Good);

    [RelayCommand]
    private void GradeEasy() => Grade(ReviewGrade.Easy);

    private void Grade(ReviewGrade grade)
    {
        if (!IsReviewing || IsSessionDone || !IsFlipped || _queue.Count == 0)
            return;

        var item = _queue[0];
        _queue.RemoveAt(0);

        var wasSuspended = item.Card.Suspended;
        var entry = Scheduler.Apply(item.Deck, item.Note, item.Card, grade, _sessionNow, Random.Shared);
        _log.Append(entry);

        // A small spark the first time a card is cleared this session — re-shows
        // and "again" repeats can't farm it.
        if (grade != ReviewGrade.Again && _paid.Add(item.Card.Id))
        {
            _wallet.Add(EmberPerCard);
            _embersEarned += EmberPerCard;
            _state.Today.ReviewedCards++;
        }

        // Just became a leech and got suspended — mention it once.
        if (!wasSuspended && item.Card.Suspended)
            LeechNotice = "a card was suspended as a leech";

        // Bury the note's other cards until tomorrow so siblings don't pile up.
        if (item.Deck.Options.BurySiblings)
            BurySiblings(item);

        // Cards still stepping through learning/relearning come back later this
        // session; graduated and reviewed cards are done.
        if (item.Card.State is CardState.Learning or CardState.Relearning && !item.Card.Suspended)
            _queue.Add(item);
        else
            _reviewed++;

        _save();
        ShowNext();
    }

    private void BurySiblings(ScheduledCard item)
    {
        var tomorrow = DateOnly.FromDateTime(_sessionNow).AddDays(1);
        foreach (var sibling in item.Note.Cards)
        {
            if (sibling.Id == item.Card.Id) continue;
            sibling.BuriedUntil = tomorrow;
        }
        _queue.RemoveAll(q => q.Note == item.Note && q.Card.Id != item.Card.Id);
    }

    /// <summary>Suspend the current card and move on — it won't come back until
    /// unsuspended (in the browser).</summary>
    [RelayCommand]
    private void SuspendCurrent()
    {
        if (!IsReviewing || IsSessionDone || _queue.Count == 0) return;
        var item = _queue[0];
        _queue.RemoveAt(0);
        item.Card.Suspended = true;
        _queue.RemoveAll(q => q.Card.Id == item.Card.Id);
        _save();
        ShowNext();
    }

    /// <summary>Bury the current card (and its siblings) until tomorrow.</summary>
    [RelayCommand]
    private void BuryCurrent()
    {
        if (!IsReviewing || IsSessionDone || _queue.Count == 0) return;
        var item = _queue[0];
        _queue.RemoveAt(0);
        var tomorrow = DateOnly.FromDateTime(_sessionNow).AddDays(1);
        item.Card.BuriedUntil = tomorrow;
        foreach (var sibling in item.Note.Cards)
            sibling.BuriedUntil = tomorrow;
        _queue.RemoveAll(q => q.Note == item.Note);
        _save();
        ShowNext();
    }

    [RelayCommand(CanExecute = nameof(CanEndReview))]
    private void EndReview()
    {
        StopAutoReveal();
        IsReviewing = false;
        IsSessionDone = false;
        _queue.Clear();
        Refresh(); // the cards just reviewed are no longer due
    }

    private bool CanEndReview() => IsReviewing;
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The card browser: a searchable, flat list of every card across every deck,
/// with checkbox multi-select and bulk actions (suspend, bury, delete, move
/// deck, tag). Search uses <see cref="SearchQueryParser"/>; filtering is
/// debounced so typing over a big collection stays smooth. Editing a row opens
/// the shared note editor.
/// </summary>
public partial class CardBrowserViewModel : ViewModelBase
{
    private const int MaxRows = 1000;

    private readonly AppState _state;
    private readonly Action _save;
    private readonly Action _refreshDecks;
    private readonly MediaStore _media;
    private readonly DispatcherTimer _filterTimer;

    public ObservableCollection<BrowserRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _resultLabel = string.Empty;
    [ObservableProperty] private string _bulkTagText = string.Empty;
    [ObservableProperty] private Deck? _moveTargetDeck;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    private NoteEditorViewModel? _editor;

    private BrowserRowViewModel? _editingRow;
    public bool IsEditing => Editor is not null;

    public IReadOnlyList<Deck> Decks => _state.Decks;

    public CardBrowserViewModel(AppState state, Action save, Action refreshDecks, MediaStore media)
    {
        _state = state;
        _save = save;
        _refreshDecks = refreshDecks;
        _media = media;

        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); ApplyFilter(); };
    }

    /// <summary>Rebuild the list from scratch — called when the browser opens.</summary>
    public void Reload()
    {
        CloseEditor();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void ApplyFilter()
    {
        var predicate = SearchQueryParser.Parse(SearchText, DateTime.Now);
        Rows.Clear();

        var shown = 0;
        var total = 0;
        foreach (var deck in _state.Decks)
        foreach (var note in deck.Notes)
        foreach (var card in note.Cards)
        {
            if (!predicate(new CardMatch(deck, note, card))) continue;
            total++;
            if (shown < MaxRows)
            {
                Rows.Add(new BrowserRowViewModel(deck, note, card));
                shown++;
            }
        }

        ResultLabel = total > MaxRows
            ? $"showing {MaxRows} of {total} cards"
            : total == 1 ? "1 card" : $"{total} cards";
    }

    private IEnumerable<BrowserRowViewModel> Selected => Rows.Where(r => r.IsSelected);

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var r in Rows) r.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var r in Rows) r.IsSelected = false;
    }

    [RelayCommand]
    private void SuspendSelected() => Bulk(r => r.Card.Suspended = true);

    [RelayCommand]
    private void UnsuspendSelected() => Bulk(r => r.Card.Suspended = false);

    [RelayCommand]
    private void BurySelected()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Now).AddDays(1);
        Bulk(r => r.Card.BuriedUntil = tomorrow);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var victims = Selected.ToList();
        if (victims.Count == 0) return;

        foreach (var r in victims)
        {
            r.Note.Cards.Remove(r.Card);
            // A note with no cards left is empty — drop it too.
            if (r.Note.Cards.Count == 0)
                r.Deck.Notes.Remove(r.Note);
            Rows.Remove(r);
        }

        AfterBulk();
    }

    [RelayCommand]
    private void MoveSelected()
    {
        if (MoveTargetDeck is null) return;

        foreach (var r in Selected.ToList())
        {
            if (r.Deck == MoveTargetDeck) continue;
            r.Deck.Notes.Remove(r.Note);
            if (!MoveTargetDeck.Notes.Contains(r.Note))
                MoveTargetDeck.Notes.Add(r.Note);
        }

        AfterBulk();
        ApplyFilter(); // deck column changed for moved rows
    }

    [RelayCommand]
    private void AddTag()
    {
        var tag = BulkTagText.Trim();
        if (tag.Length == 0) return;
        Bulk(r => { if (!r.Note.Tags.Contains(tag)) r.Note.Tags.Add(tag); });
        BulkTagText = string.Empty;
    }

    [RelayCommand]
    private void RemoveTag()
    {
        var tag = BulkTagText.Trim();
        if (tag.Length == 0) return;
        Bulk(r => r.Note.Tags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
        BulkTagText = string.Empty;
    }

    private void Bulk(Action<BrowserRowViewModel> action)
    {
        var any = false;
        foreach (var r in Selected.ToList())
        {
            action(r);
            r.Refresh();
            any = true;
        }
        if (any) AfterBulk();
    }

    private void AfterBulk()
    {
        _save();
        _refreshDecks();
    }

    // ---- edit a note ----

    [RelayCommand]
    private void EditRow(BrowserRowViewModel? row)
    {
        if (row is null) return;
        _editingRow = row;
        Editor = new NoteEditorViewModel(row.Note, OnNoteEdited, _media);
    }

    [RelayCommand]
    private void CloseEditor()
    {
        _editingRow?.Refresh();
        _editingRow = null;
        Editor = null;
    }

    private void OnNoteEdited()
    {
        _editingRow?.Refresh();
        _save();
        _refreshDecks();
    }
}

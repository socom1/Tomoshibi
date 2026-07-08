using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>One selectable accent colour with its brush, for the deck swatches.</summary>
public sealed record DeckColorSwatch(string Hex, IBrush Brush);

/// <summary>One deck: the note-list editor plus the live card / due counts the
/// review list and dashboard read. Notes are edited in a detail editor rather
/// than inline.</summary>
public partial class DeckViewModel : ViewModelBase
{
    private readonly Action _changed;
    private readonly MediaStore _media;
    private readonly Action<Note> _openOcclusion;
    public Deck Model { get; }

    public ObservableCollection<NoteRowViewModel> Notes { get; } = new();

    [ObservableProperty] private int _cardCount;
    [ObservableProperty] private int _dueCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DueBarOpacity))]
    private bool _hasDue;

    [ObservableProperty] private string _countsLabel = string.Empty;

    /// <summary>Full-strength when there's something due; dimmed when caught up.</summary>
    public double DueBarOpacity => HasDue ? 1.0 : 0.4;

    /// <summary>A █/░ meter of how much of the deck is due right now.</summary>
    [ObservableProperty] private string _dueBar = string.Empty;

    // ---- Note editor (list → detail) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingNote))]
    private NoteEditorViewModel? _editor;

    private NoteRowViewModel? _editingRow;

    public bool IsEditingNote => Editor is not null;

    public DeckViewModel(Deck model, Action changed, MediaStore media, Action<Note> openOcclusion)
    {
        Model = model;
        _changed = changed;
        _media = media;
        _openOcclusion = openOcclusion;

        foreach (var note in model.Notes)
            Notes.Add(new NoteRowViewModel(note));

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
    public bool HasNotes => Model.Notes.Count > 0;

    // ---- Deck look: icon + colour ----

    private const string DefaultColor = "#9ECE6A"; // matcha
    private const MaterialIconKind DefaultIcon = MaterialIconKind.CardsOutline;

    /// <summary>Preset Material Design icons offered in the deck editor.</summary>
    public MaterialIconKind[] IconChoices { get; } =
    {
        MaterialIconKind.CardsOutline, MaterialIconKind.Brain, MaterialIconKind.BookOpenPageVariant,
        MaterialIconKind.Flask, MaterialIconKind.Calculator, MaterialIconKind.Earth,
        MaterialIconKind.Laptop, MaterialIconKind.Palette, MaterialIconKind.Stethoscope,
        MaterialIconKind.MusicClefTreble, MaterialIconKind.ScaleBalance, MaterialIconKind.Atom,
        MaterialIconKind.Translate, MaterialIconKind.Dna, MaterialIconKind.Gavel,
        MaterialIconKind.Feather
    };

    /// <summary>Preset accent colours (Tokyo Night palette).</summary>
    public IReadOnlyList<DeckColorSwatch> ColorChoices { get; } =
        new[] { "#9ECE6A", "#7AA2F7", "#F7768E", "#E0AF68", "#BB9AF7", "#73DACA" }
            .Select(h => new DeckColorSwatch(h, new SolidColorBrush(Color.Parse(h)))).ToList();

    /// <summary>The deck's icon as a Material icon kind (falls back to a card).</summary>
    public MaterialIconKind IconKind =>
        Enum.TryParse<MaterialIconKind>(Model.Icon, out var kind) ? kind : DefaultIcon;

    public IBrush ColorBrush => BrushForColor(Model.Color);

    /// <summary>A brush for a deck colour hex (falls back to matcha). Shared so
    /// the review session can tint itself to the deck being studied.</summary>
    public static IBrush BrushForColor(string? hex)
    {
        try { return new SolidColorBrush(Color.Parse(string.IsNullOrEmpty(hex) ? DefaultColor : hex)); }
        catch { return new SolidColorBrush(Color.Parse(DefaultColor)); }
    }

    [RelayCommand]
    private void SetIcon(MaterialIconKind icon)
    {
        Model.Icon = icon.ToString();
        OnPropertyChanged(nameof(IconKind));
        _changed();
    }

    [RelayCommand]
    private void SetColor(string? color)
    {
        Model.Color = color;
        OnPropertyChanged(nameof(ColorBrush));
        _changed();
    }

    private IEnumerable<Card> AllCards => Model.Notes.SelectMany(n => n.Cards);

    // ---- Note management ----

    [RelayCommand]
    private void NewNote()
    {
        var note = new Note { Type = NoteType.Basic, Fields = { string.Empty, string.Empty } };
        CardGenerator.Sync(note);
        Model.Notes.Add(note);

        var row = new NoteRowViewModel(note);
        Notes.Add(row);
        OnPropertyChanged(nameof(HasNotes));

        OpenEditor(row);
        _changed();
    }

    [RelayCommand]
    private void EditNote(NoteRowViewModel? row)
    {
        if (row is null) return;
        if (row.Model.Type == NoteType.ImageOcclusion)
            _openOcclusion(row.Model);   // image-occlusion notes use the canvas editor
        else
            OpenEditor(row);
    }

    [RelayCommand]
    private void NewOcclusion()
    {
        var note = new Note { Type = NoteType.ImageOcclusion, Fields = { string.Empty, string.Empty, string.Empty } };
        Model.Notes.Add(note);
        Notes.Add(new NoteRowViewModel(note));
        OnPropertyChanged(nameof(HasNotes));
        _changed();
        _openOcclusion(note);
    }

    private void OpenEditor(NoteRowViewModel row)
    {
        _editingRow = row;
        Editor = new NoteEditorViewModel(row.Model, OnNoteEdited, _media);
    }

    [RelayCommand]
    private void CloseEditor()
    {
        // Drop an untouched, empty new note rather than leaving a blank row.
        if (_editingRow is not null && IsBlank(_editingRow.Model))
            RemoveRow(_editingRow);

        _editingRow?.Refresh();
        _editingRow = null;
        Editor = null;
        RefreshCounts();
    }

    [RelayCommand]
    private void DeleteNote(NoteRowViewModel? row)
    {
        if (row is null) return;
        if (row == _editingRow) CloseEditor();
        RemoveRow(row);
        RefreshCounts();
        _changed();
    }

    private void RemoveRow(NoteRowViewModel row)
    {
        Model.Notes.Remove(row.Model);
        Notes.Remove(row);
        OnPropertyChanged(nameof(HasNotes));
    }

    private void OnNoteEdited()
    {
        _editingRow?.Refresh();
        RefreshCounts();
        _changed();
    }

    // ---- CSV import / export ----

    /// <summary>This deck's notes as CSV text.</summary>
    public string ExportCsv() => CsvCards.Export(Model.Notes);

    /// <summary>Append notes parsed from CSV/TSV text into this deck.</summary>
    public int ImportCsv(string text)
    {
        var notes = CsvCards.Import(text);
        foreach (var note in notes)
        {
            Model.Notes.Add(note);
            Notes.Add(new NoteRowViewModel(note));
        }
        if (notes.Count > 0)
        {
            OnPropertyChanged(nameof(HasNotes));
            RefreshCounts();
            _changed();
        }
        return notes.Count;
    }

    private static bool IsBlank(Note note)
        => note.Fields.All(string.IsNullOrWhiteSpace) && note.Tags.Count == 0;

    /// <summary>Recompute the card / due counts after an edit or a review.</summary>
    public void RefreshCounts()
    {
        var now = DateTime.Now;
        var cards = AllCards.ToList();
        var due = cards.Where(c => Scheduler.IsDue(c, now)).ToList();

        CardCount = cards.Count;
        DueCount = due.Count;
        HasDue = DueCount > 0;

        if (CardCount == 0)
        {
            CountsLabel = "no cards yet";
        }
        else if (DueCount == 0)
        {
            CountsLabel = $"caught up · {CardCount} cards";
        }
        else
        {
            var parts = new List<string>();
            var news = due.Count(c => c.State == CardState.New);
            var learning = due.Count(c => c.State is CardState.Learning or CardState.Relearning);
            var review = due.Count(c => c.State == CardState.Review);
            if (news > 0) parts.Add($"{news} new");
            if (learning > 0) parts.Add($"{learning} learning");
            if (review > 0) parts.Add($"{review} due");
            CountsLabel = string.Join(" · ", parts);
        }

        DueBar = ReviewViewModel.AsciiBar(DueCount, Math.Max(CardCount, 1), 10);
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>One card as a row in the card browser: its deck, a peek at the note,
/// its scheduling state and a checkbox for bulk actions.</summary>
public partial class BrowserRowViewModel : ViewModelBase
{
    public Deck Deck { get; }
    public Note Note { get; }
    public Card Card { get; }

    [ObservableProperty] private bool _isSelected;

    public BrowserRowViewModel(Deck deck, Note note, Card card)
    {
        Deck = deck;
        Note = note;
        Card = card;
    }

    public string Preview
    {
        get
        {
            var first = Note.Fields.Count > 0 ? Note.Fields[0] : string.Empty;
            var text = ContentTokenizer.ToPlainText(first);
            if (Note.Type == NoteType.Cloze) text = $"cloze {Card.Ord}: {text}";
            return text.Length > 80 ? text[..80] + "…" : text;
        }
    }

    public string DeckName => Deck.Name;
    public string TypeLabel => Note.Type switch
    {
        NoteType.Basic => "basic",
        NoteType.BasicReversed => "reversed",
        NoteType.Cloze => "cloze",
        NoteType.ImageOcclusion => "occlusion",
        _ => "note"
    };

    public string StateLabel
    {
        get
        {
            if (Card.Suspended) return "suspended";
            if (Card.BuriedUntil is { } b && b > DateOnly.FromDateTime(DateTime.Now)) return "buried";
            return Card.State switch
            {
                CardState.New => "new",
                CardState.Learning => "learning",
                CardState.Relearning => "relearning",
                _ => "review"
            };
        }
    }

    public string DueLabel => Card.State == CardState.New
        ? "—"
        : Card.Due.Date <= DateTime.Now.Date
            ? "due"
            : Card.Due.ToString("MMM d");

    public string TagsLabel => Note.Tags.Count == 0 ? string.Empty : string.Join(" ", Note.Tags);
    public bool HasTags => Note.Tags.Count > 0;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(DueLabel));
        OnPropertyChanged(nameof(TagsLabel));
        OnPropertyChanged(nameof(HasTags));
    }
}

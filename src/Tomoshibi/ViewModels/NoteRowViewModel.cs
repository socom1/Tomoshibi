using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>A note as a single row in the deck editor's list: a type badge, a
/// plain-text peek at the first field, its tags and card count. Editing happens
/// in the <see cref="NoteEditorViewModel"/>, not inline.</summary>
public class NoteRowViewModel : ViewModelBase
{
    public Note Model { get; }

    public NoteRowViewModel(Note model) => Model = model;

    public string Preview
    {
        get
        {
            var first = Model.Fields.Count > 0 ? Model.Fields[0] : string.Empty;
            var text = ContentTokenizer.ToPlainText(first);
            return text.Length > 90 ? text[..90] + "…" : text;
        }
    }

    public string TypeLabel => Model.Type switch
    {
        NoteType.Basic => "basic",
        NoteType.BasicReversed => "reversed",
        NoteType.Cloze => "cloze",
        NoteType.ImageOcclusion => "occlusion",
        _ => "note"
    };

    public string TagsLabel => string.Join(" ", Model.Tags.Select(t => "#" + t));
    public bool HasTags => Model.Tags.Count > 0;
    public string CardsLabel => Model.Cards.Count == 1 ? "1 card" : $"{Model.Cards.Count} cards";

    /// <summary>Refresh the display after the editor changes the note.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(TagsLabel));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(CardsLabel));
    }
}

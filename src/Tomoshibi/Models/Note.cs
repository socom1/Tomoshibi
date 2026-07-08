using System;
using System.Collections.Generic;

namespace Tomoshibi.Models;

/// <summary>
/// A unit of content authored by the user. A note owns the cards it generates
/// (see <see cref="CardGenerator"/>): the note holds the fields, tags and
/// occlusion data; each <see cref="Card"/> holds its own scheduling state.
/// Splitting content from schedule is how Anki lets one edit fix every card.
/// </summary>
public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public NoteType Type { get; set; } = NoteType.Basic;

    /// <summary>
    /// Field contents, positional by note type:
    /// Basic / Reversed — [front, back]; Cloze — [text, back-extra];
    /// ImageOcclusion — [imageToken, header, back-extra].
    /// </summary>
    public List<string> Fields { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    /// <summary>Image-occlusion masks; card at ord <c>i</c> hides
    /// <c>Occlusions[i]</c>. Empty for every other note type.</summary>
    public List<OcclusionRect> Occlusions { get; set; } = new();

    /// <summary>Image-occlusion reveal mode. True (default) = show every mask
    /// on the front and quiz the highlighted one (hide-all-guess-one); false =
    /// only the target mask is hidden (hide-one-guess-one).</summary>
    public bool HideAll { get; set; } = true;

    /// <summary>The cards this note generates. Owned here so scheduling state
    /// travels with the note when it moves decks.</summary>
    public List<Card> Cards { get; set; } = new();
}

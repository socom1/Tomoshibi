namespace Tomoshibi.Models;

/// <summary>
/// The shape of a note — which fields it carries and how many cards it
/// generates. Mirrors Anki's built-in note types.
/// </summary>
public enum NoteType
{
    /// <summary>Front → Back. One card.</summary>
    Basic,

    /// <summary>Front → Back and Back → Front. Two cards.</summary>
    BasicReversed,

    /// <summary>One text field with {{cN::…}} deletions; one card per number.</summary>
    Cloze,

    /// <summary>An image with rectangular masks; one card per mask.</summary>
    ImageOcclusion
}

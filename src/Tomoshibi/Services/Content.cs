using System;
using System.Collections.Generic;

namespace Tomoshibi.Services;

/// <summary>Which face of a card is being rendered — decides how cloze
/// deletions and occlusion masks show.</summary>
public enum CardSide { Front, Back }

/// <summary>The kind of embedded media a token points at.</summary>
public enum MediaKind { Image, Audio, Video }

/// <summary>One piece of parsed field content. The tokenizer turns a field
/// string into a sequence of these, grouped into <see cref="ContentBlock"/>
/// paragraphs; the renderer walks them to build the visual tree.</summary>
public abstract class ContentSegment { }

/// <summary>A run of text, optionally bold and/or italic.</summary>
public sealed class TextSegment : ContentSegment
{
    public string Text { get; init; } = string.Empty;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
}

/// <summary>An embedded image / audio / video, by media-store token name.</summary>
public sealed class MediaSegment : ContentSegment
{
    public MediaKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>A cloze deletion: the answer text hidden behind {{cN::…}}, plus an
/// optional hint. How it displays depends on the card's ord and the side being
/// shown (see the renderer).</summary>
public sealed class ClozeSegment : ContentSegment
{
    public int Ord { get; init; }
    public string Answer { get; init; } = string.Empty;
    public string? Hint { get; init; }
}

/// <summary>A paragraph of segments (one source line).</summary>
public sealed class ContentBlock
{
    public IReadOnlyList<ContentSegment> Segments { get; init; } = Array.Empty<ContentSegment>();
}

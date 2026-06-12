using System;

namespace Tomoshibi.Models;

/// <summary>A sticky on the corkboard. Colour is one of the palette accents.</summary>
public class StickyNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public StickyColor Color { get; set; } = StickyColor.Amber;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public enum StickyColor
{
    Amber,
    Sakura,
    Matcha,
    Blue
}

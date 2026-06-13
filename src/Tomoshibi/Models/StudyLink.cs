using System;

namespace Tomoshibi.Models;

/// <summary>A saved study/lofi video or playlist link. Opened in the user's
/// browser — the app curates the links, the browser plays them.</summary>
public class StudyLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

namespace Tomoshibi.Models;

/// <summary>A checklist step inside a todo item.</summary>
public class Subtask
{
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}

using System.Collections.Generic;
using System;

namespace Tomoshibi.Models;

/// <summary>
/// Root object persisted to disk as JSON. One file holds everything the app
/// needs to restore between launches.
/// </summary>
public class AppState
{
    public string DailyIntention { get; set; } = string.Empty;
    public DateOnly IntentionDate { get; set; }

    public DailyStats Today { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public PomodoroSettings Settings { get; set; } = new();
}

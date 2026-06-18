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

    /// <summary>Set once the user marks the day's intention as kept. Earns a
    /// one-off ember reward and resets with the intention at midnight.</summary>
    public bool IntentionKept { get; set; }

    /// <summary>The day's end-of-day reflection — how it went. Banked into
    /// <see cref="Journal"/> with the intention at the midnight rollover.</summary>
    public string DailyReflection { get; set; } = string.Empty;

    /// <summary>Past days' intentions + reflections, oldest first. Appended at
    /// the midnight rollover for any day that had something written.</summary>
    public List<DayNote> Journal { get; set; } = new();

    public DailyStats Today { get; set; } = new();

    /// <summary>Past days' stats, appended at the midnight rollover. The
    /// streak display reads this; the future streak calendar will too.</summary>
    public List<DailyStats> History { get; set; } = new();

    public PomodoroSettings Settings { get; set; } = new();

    /// <summary>Strips the timer down to just the clock + gauge, hiding the
    /// session log and keybind footer for a calmer view.</summary>
    public bool SimpleTimer { get; set; }

    /// <summary>The user's "task code" — what they've typed in the today
    /// editor. Parsed at the view-model layer; persisted as-is.</summary>
    public string TaskTemplate { get; set; } = string.Empty;

    /// <summary>Legacy task list. Kept around so existing JSON files still
    /// deserialise; on first load the contents are migrated into
    /// <see cref="TaskTemplate"/> and this list is cleared.</summary>
    public List<TaskItem> Tasks { get; set; } = new();

    // Deadline / exam reminders
    public bool RemindersEnabled { get; set; } = true;

    /// <summary>Show the gentle "rest soon" line on the timer after midnight.</summary>
    public bool SleepReminderEnabled { get; set; } = true;

    /// <summary>Keys of reminders already fired (item + threshold + date), so
    /// a deadline never notifies twice. Swept as dates pass.</summary>
    public List<string> NotifiedReminders { get; set; } = new();

    // Navigation
    public bool IsNavOpen { get; set; } = true;
    public bool ShowWelcome { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool LightTheme { get; set; }

    // Currency + cosmetics
    public int Embers { get; set; }
    public string ActiveThemeId { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> OwnedThemeIds { get; set; } = new();
    public Destination ActiveDestination { get; set; } = Destination.Dashboard;

    // Window placement (0 width = never saved, use the XAML defaults)
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }

    // Timetable
    public List<ClassSlot> ClassSlots { get; set; } = new();
    public ClassesView ClassesView { get; set; } = ClassesView.Grid;

    /// <summary>Legacy: standalone deadlines were folded into the todo
    /// backlog as tickets with due dates. Kept so old state files still
    /// deserialise; migrated and emptied on load.</summary>
    public List<Deadline> Deadlines { get; set; } = new();

    // Todo backlog
    public List<TodoItem> Todos { get; set; } = new();
    public int NextTodoNumber { get; set; } = 1;

    // Study video links (opened in the browser)
    public List<StudyLink> StudyLinks { get; set; } = new();

    // Music player
    public bool MusicEnabled { get; set; } = true;
    public bool MusicAutoplay { get; set; }
    public string? MusicFolder { get; set; }
    public double MusicVolume { get; set; } = 70;
    public bool MusicShuffle { get; set; } = true;

    // Flashcard decks (spaced repetition)
    public List<Deck> Decks { get; set; } = new();

    // Subjects + grades
    public List<Subject> Subjects { get; set; } = new();
    public GradeScaleKind GradeScale { get; set; } = GradeScaleKind.UsGpa;
    public List<YearWeight> YearWeights { get; set; } = new();

    /// <summary>The user's own grade boundaries, used when the scale is set to
    /// Custom. Seeded with a sensible default on first use.</summary>
    public List<GradeBand> CustomGradeBands { get; set; } = new();

    /// <summary>The overall percentage the user is aiming for across all
    /// subjects — drives the grade-goal planner. Null = not set yet.</summary>
    public double? OverallGoalPercent { get; set; }
}

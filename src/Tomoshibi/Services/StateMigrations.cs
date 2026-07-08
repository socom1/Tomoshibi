using System;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Forward migrations run once at load, before any view model reads the
/// state. Each one maps a shape an older build wrote onto what the current
/// build expects, and is a no-op on already-migrated files. Pure state-in,
/// state-out so every rule stays testable.
/// </summary>
public static class StateMigrations
{
    /// <summary>Run every migration in order. Callers save afterwards.</summary>
    public static void Apply(AppState state)
    {
        // Seal first: it must judge the file exactly as it was read, before
        // any migration below rewrites a sealed field (MigrateTheme fills an
        // empty ActiveThemeId, for one). Saves re-stamp, so the seal on disk
        // always describes post-migration state anyway.
        EnforceEmberSeal(state);

        MigrateDeadlinesToTickets(state);
        MigrateLegacyTasks(state);
        MigrateTheme(state);
        MigrateFlashcardsToNotes(state);
    }

    /// <summary>The flat front/back <see cref="Flashcard"/> list on each deck
    /// became the note/card model. Fold every legacy card into a Basic
    /// <see cref="Note"/> with one <see cref="Card"/>, mapping the old SM-2
    /// interval/ease onto an approximate FSRS stability/difficulty, then clear
    /// the legacy list. No-op once a deck's cards have been migrated.</summary>
    private static void MigrateFlashcardsToNotes(AppState state)
    {
        foreach (var deck in state.Decks)
        {
            if (deck.Cards.Count == 0)
                continue;

            foreach (var old in deck.Cards)
            {
                var card = new Card { Ord = 0 };
                var brandNew = old.Reps == 0 && old.Due == default;
                if (brandNew)
                {
                    card.State = CardState.New;
                    card.Due = DateTime.Now;
                }
                else
                {
                    card.State = CardState.Review;
                    // At retention 0.9, FSRS interval == stability, so the old
                    // interval maps straight onto stability. Ease (SM-2, ~1.3–2.5+)
                    // maps inversely onto difficulty (1 easy … 10 hard).
                    card.Stability = Math.Max(0.1, old.Interval);
                    card.Difficulty = Math.Clamp(5 + (2.5 - old.Ease) * 4.5, 1, 10);
                    card.Reps = old.Reps;
                    card.Due = old.Due.ToDateTime(TimeOnly.MinValue);
                    card.LastReviewed = old.LastReviewed?.ToDateTime(TimeOnly.MinValue);
                }

                deck.Notes.Add(new Note
                {
                    Type = NoteType.Basic,
                    Fields = { old.Front, old.Back },
                    Cards = { card }
                });
            }

            deck.Cards.Clear();
        }
    }

    /// <summary>Standalone deadlines became todo tickets with due dates. Old
    /// state files may still carry a deadlines list — convert each to a ticket
    /// (skipping ones that already exist) and empty the legacy list.</summary>
    private static void MigrateDeadlinesToTickets(AppState state)
    {
        if (state.Deadlines.Count == 0)
            return;

        foreach (var d in state.Deadlines)
        {
            var exists = state.Todos.Any(t => t.Due == d.Date && t.Title == d.Title);
            if (!exists)
            {
                state.Todos.Add(new TodoItem
                {
                    Number = state.NextTodoNumber++,
                    Title = d.Title,
                    Course = d.Course,
                    Due = d.Date
                });
            }
        }

        state.Deadlines.Clear();
    }

    /// <summary>The legacy checkbox task list became the code template. Turn
    /// any leftover TaskItems into template text — but never overwrite a
    /// template the user has already written.</summary>
    private static void MigrateLegacyTasks(AppState state)
    {
        if (!string.IsNullOrEmpty(state.TaskTemplate) || state.Tasks.Count == 0)
            return;

        state.TaskTemplate = TaskTemplateParser.FromTaskItems(state.Tasks);
        state.Tasks.Clear();
    }

    /// <summary>Map the old light-theme bool onto the named-theme system, and
    /// make sure the two free themes are always owned.</summary>
    private static void MigrateTheme(AppState state)
    {
        if (string.IsNullOrEmpty(state.ActiveThemeId))
            state.ActiveThemeId = state.LightTheme ? "light" : "dark";

        foreach (var free in new[] { "dark", "light" })
            if (!state.OwnedThemeIds.Contains(free))
                state.OwnedThemeIds.Add(free);
    }

    /// <summary>Check the wallet against its tamper seal. A file last saved
    /// by a build from before the seal existed is trusted once and stamped —
    /// upgrades never empty anyone's wallet. From then on the file always
    /// carries a seal, so a missing or wrong one means a hand edit, and the
    /// wallet resets. Runs on every load and on backup restore.</summary>
    private static void EnforceEmberSeal(AppState state)
    {
        var preSeal = string.IsNullOrEmpty(state.LastSeenVersion) ||
                      (Version.TryParse(state.LastSeenVersion, out var last) &&
                       last < EmberSeal.IntroducedIn);

        if (string.IsNullOrEmpty(state.EmberSeal) && preSeal)
        {
            EmberSeal.Apply(state);
            return;
        }

        if (!EmberSeal.Verify(state))
            EmberSeal.Reset(state);
    }
}

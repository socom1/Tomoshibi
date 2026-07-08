using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Migrations run against every state file ever written by an older
/// build, so each rule needs to hold for both the legacy shape and a file
/// that has already been migrated (idempotence).</summary>
public class StateMigrationsTests
{
    // ---- deadlines → todo tickets ----

    [Fact]
    public void Legacy_deadlines_become_numbered_tickets_and_the_list_empties()
    {
        var state = new AppState { NextTodoNumber = 5 };
        state.Deadlines.Add(new Deadline
        {
            Date = new DateOnly(2026, 7, 10),
            Title = "algorithms exam",
            Course = "CS210"
        });

        StateMigrations.Apply(state);

        var ticket = Assert.Single(state.Todos);
        Assert.Equal(5, ticket.Number);
        Assert.Equal("algorithms exam", ticket.Title);
        Assert.Equal("CS210", ticket.Course);
        Assert.Equal(new DateOnly(2026, 7, 10), ticket.Due);
        Assert.Equal(6, state.NextTodoNumber);
        Assert.Empty(state.Deadlines);
    }

    [Fact]
    public void A_deadline_matching_an_existing_ticket_is_not_duplicated()
    {
        var state = new AppState();
        state.Todos.Add(new TodoItem
        {
            Title = "algorithms exam",
            Due = new DateOnly(2026, 7, 10)
        });
        state.Deadlines.Add(new Deadline
        {
            Date = new DateOnly(2026, 7, 10),
            Title = "algorithms exam"
        });

        StateMigrations.Apply(state);

        Assert.Single(state.Todos);
        Assert.Empty(state.Deadlines);
    }

    // ---- legacy task list → template text ----

    [Fact]
    public void Legacy_tasks_become_template_text_and_the_list_empties()
    {
        var state = new AppState();
        state.Tasks.Add(new TaskItem { Title = "read chapter 4", Course = "PHYS150", IsDone = true });
        state.Tasks.Add(new TaskItem { Title = "problem set" });

        StateMigrations.Apply(state);

        Assert.Empty(state.Tasks);
        var blocks = TaskTemplateParser.Parse(state.TaskTemplate);
        Assert.Equal(2, blocks.Count);
        Assert.Equal("read chapter 4", blocks[0].Title);
        Assert.Equal("PHYS150", blocks[0].Course);
        Assert.True(blocks[0].IsDone);
        Assert.Equal("problem set", blocks[1].Title);
    }

    [Fact]
    public void An_existing_template_is_never_overwritten_by_legacy_tasks()
    {
        var state = new AppState { TaskTemplate = "// my plan\nstudy: 50" };
        state.Tasks.Add(new TaskItem { Title = "stale leftover" });

        StateMigrations.Apply(state);

        Assert.Equal("// my plan\nstudy: 50", state.TaskTemplate);
    }

    // ---- light-theme bool → named themes ----

    [Fact]
    public void Theme_id_is_seeded_from_the_legacy_bool()
    {
        var dark = new AppState();
        StateMigrations.Apply(dark);
        Assert.Equal("dark", dark.ActiveThemeId);

        var light = new AppState { LightTheme = true };
        StateMigrations.Apply(light);
        Assert.Equal("light", light.ActiveThemeId);
    }

    [Fact]
    public void A_chosen_theme_survives_and_free_themes_are_always_owned()
    {
        var state = new AppState { ActiveThemeId = "ember" };
        state.OwnedThemeIds.Add("ember");

        StateMigrations.Apply(state);

        Assert.Equal("ember", state.ActiveThemeId);
        Assert.Contains("dark", state.OwnedThemeIds);
        Assert.Contains("light", state.OwnedThemeIds);
    }

    [Fact]
    public void Running_migrations_twice_changes_nothing_more()
    {
        var state = new AppState { NextTodoNumber = 1 };
        state.Deadlines.Add(new Deadline { Date = new DateOnly(2026, 7, 10), Title = "exam" });
        state.Tasks.Add(new TaskItem { Title = "read" });

        StateMigrations.Apply(state);
        var todosAfterFirst = state.Todos.Count;
        var templateAfterFirst = state.TaskTemplate;
        var ownedAfterFirst = state.OwnedThemeIds.Count;

        StateMigrations.Apply(state);

        Assert.Equal(todosAfterFirst, state.Todos.Count);
        Assert.Equal(templateAfterFirst, state.TaskTemplate);
        Assert.Equal(ownedAfterFirst, state.OwnedThemeIds.Count);
    }

    // ---- legacy flashcards → notes + FSRS cards ----

    [Fact]
    public void A_legacy_flashcard_becomes_a_basic_note_with_one_card()
    {
        var state = new AppState();
        var deck = new Deck { Name = "kanji" };
        deck.Cards.Add(new Flashcard { Front = "火", Back = "fire" });
        state.Decks.Add(deck);

        StateMigrations.Apply(state);

        Assert.Empty(deck.Cards);
        var note = Assert.Single(deck.Notes);
        Assert.Equal(NoteType.Basic, note.Type);
        Assert.Equal("火", note.Fields[0]);
        Assert.Equal("fire", note.Fields[1]);
        var card = Assert.Single(note.Cards);
        Assert.Equal(CardState.New, card.State);
    }

    [Fact]
    public void A_reviewed_legacy_card_maps_its_interval_and_ease_onto_fsrs()
    {
        var state = new AppState();
        var deck = new Deck();
        deck.Cards.Add(new Flashcard
        {
            Front = "q", Back = "a",
            Interval = 12, Ease = 2.5, Reps = 4,
            Due = new DateOnly(2026, 7, 20),
            LastReviewed = new DateOnly(2026, 7, 8)
        });
        state.Decks.Add(deck);

        StateMigrations.Apply(state);

        var card = deck.Notes.Single().Cards.Single();
        Assert.Equal(CardState.Review, card.State);
        Assert.Equal(12, card.Stability); // interval → stability at r=0.9
        Assert.Equal(5.0, card.Difficulty, 6); // ease 2.5 → mid difficulty
        Assert.Equal(4, card.Reps);
        Assert.Equal(new DateTime(2026, 7, 20), card.Due);
    }

    [Fact]
    public void Flashcard_migration_is_idempotent()
    {
        var state = new AppState();
        var deck = new Deck();
        deck.Cards.Add(new Flashcard { Front = "q", Back = "a", Interval = 3, Reps = 1 });
        state.Decks.Add(deck);

        StateMigrations.Apply(state);
        var notesAfterFirst = deck.Notes.Count;

        StateMigrations.Apply(state);

        Assert.Equal(notesAfterFirst, deck.Notes.Count);
        Assert.Empty(deck.Cards);
    }
}

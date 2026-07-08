using System;
using System.Text.Json;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Restore replaces the user's whole world, so the reader must be
/// all-or-nothing: a real backup comes back intact and migrated, anything
/// else comes back null and changes nothing.</summary>
public class BackupRestoreTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"dailyIntention\": ")] // cut short mid-write
    [InlineData("null")]
    public void Anything_that_is_not_a_backup_is_refused(string json)
    {
        Assert.Null(BackupRestore.Parse(json));
    }

    [Fact]
    public void A_backup_round_trips_with_its_data_intact()
    {
        var original = new AppState
        {
            DailyIntention = "finish the lab write-up",
            Embers = 42,
            FocusGoalMinutes = 90
        };
        original.Todos.Add(new TodoItem { Number = 1, Title = "revise chapter 3" });
        original.Decks.Add(new Deck { Name = "kanji" });
        original.Decks[0].Cards.Add(new Flashcard { Front = "犬", Back = "dog" });

        var restored = BackupRestore.Parse(JsonSerializer.Serialize(original, CamelCase));

        Assert.NotNull(restored);
        Assert.Equal("finish the lab write-up", restored.DailyIntention);
        Assert.Equal(42, restored.Embers);
        Assert.Equal(90, restored.FocusGoalMinutes);
        Assert.Equal("revise chapter 3", Assert.Single(restored.Todos).Title);

        // The legacy flashcard is migrated into a Basic note on the way in
        // (same as a load from disk), so it lands in Notes, not the old list.
        var deck = Assert.Single(restored.Decks);
        Assert.Empty(deck.Cards);
        Assert.Equal("dog", Assert.Single(deck.Notes).Fields[1]);
    }

    [Fact]
    public void A_backup_from_an_older_build_is_migrated_on_the_way_in()
    {
        // Standalone deadlines predate the todo backlog — the migration
        // converts them, same as a load from disk would.
        var old = new AppState();
        old.Deadlines.Add(new Deadline
        {
            Date = new DateOnly(2026, 12, 11),
            Title = "essay due",
            Course = "ENG201"
        });

        var restored = BackupRestore.Parse(JsonSerializer.Serialize(old, CamelCase));

        Assert.NotNull(restored);
        Assert.Empty(restored.Deadlines);
        var ticket = Assert.Single(restored.Todos);
        Assert.Equal("essay due", ticket.Title);
        Assert.Equal(new DateOnly(2026, 12, 11), ticket.Due);
    }

    [Fact]
    public void The_settings_page_backup_export_reads_back_in()
    {
        // The same serializer settings the settings page uses for the export —
        // the pair must stay round-trippable or the restore button is a lie.
        var state = new AppState { DailyIntention = "早起き" };
        var export = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Equal("早起き", BackupRestore.Parse(export)?.DailyIntention);
    }
}

using System;
using System.IO;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The review log is append-only and read back for stats and daily
/// caps; it must round-trip, survive a truncated final line (a crash
/// mid-append), and count a day's work per deck.</summary>
public class ReviewLogServiceTests : IDisposable
{
    private readonly string _dir;

    public ReviewLogServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tomoshibi-log-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static ReviewLogEntry Entry(Guid deck, CardState before, DateTime ts) => new()
    {
        Timestamp = ts,
        CardId = Guid.NewGuid(),
        DeckId = deck,
        Grade = 3,
        StateBefore = before,
        IntervalBefore = 5,
        Stability = 10,
        Difficulty = 5
    };

    [Fact]
    public void Appended_entries_reload_in_a_fresh_service()
    {
        var deck = Guid.NewGuid();
        var log = new ReviewLogService(_dir);
        log.Append(Entry(deck, CardState.New, DateTime.Now));
        log.Append(Entry(deck, CardState.Review, DateTime.Now));

        var reloaded = new ReviewLogService(_dir);

        Assert.Equal(2, reloaded.All().Count);
    }

    [Fact]
    public void A_truncated_final_line_is_skipped_not_fatal()
    {
        var deck = Guid.NewGuid();
        var log = new ReviewLogService(_dir);
        log.Append(Entry(deck, CardState.Review, DateTime.Now));

        // Simulate a crash mid-append: a half-written trailing line.
        File.AppendAllText(log.Location, "{ \"ts\": \"2026-07-06T12:00:0");

        var reloaded = new ReviewLogService(_dir);

        Assert.Single(reloaded.All());
    }

    [Fact]
    public void Count_today_filters_by_deck_state_and_day()
    {
        var deckA = Guid.NewGuid();
        var deckB = Guid.NewGuid();
        var today = new DateOnly(2026, 7, 6);
        var noon = today.ToDateTime(new TimeOnly(12, 0));

        var log = new ReviewLogService(_dir);
        log.Append(Entry(deckA, CardState.New, noon));
        log.Append(Entry(deckA, CardState.New, noon));
        log.Append(Entry(deckA, CardState.Review, noon));
        log.Append(Entry(deckB, CardState.New, noon));
        log.Append(Entry(deckA, CardState.New, noon.AddDays(-1))); // yesterday

        Assert.Equal(2, log.CountToday(deckA, CardState.New, today));
        Assert.Equal(1, log.CountToday(deckA, CardState.Review, today));
        Assert.Equal(1, log.CountToday(deckB, CardState.New, today));
    }

    [Fact]
    public void Entries_keep_their_terse_field_shape_on_disk()
    {
        var log = new ReviewLogService(_dir);
        log.Append(Entry(Guid.NewGuid(), CardState.Review, DateTime.Now));

        var line = File.ReadLines(log.Location).First();

        // Compact property names keep the file small.
        Assert.Contains("\"ts\":", line);
        Assert.Contains("\"g\":", line);
        Assert.Contains("\"st\":", line);
    }
}

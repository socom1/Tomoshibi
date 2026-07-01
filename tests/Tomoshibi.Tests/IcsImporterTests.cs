using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The .ics reader faces whatever a university portal exports, so
/// the mapping rules — weekly recurrences to slots, one-shots to deadlines,
/// everything else counted and skipped — need to hold on real-world quirks
/// like folded lines and missing DTEND.</summary>
public class IcsImporterTests
{
    private static string Calendar(params string[] events) =>
        "BEGIN:VCALENDAR\n" +
        string.Join("\n", events.Select(e => $"BEGIN:VEVENT\n{e}\nEND:VEVENT")) +
        "\nEND:VCALENDAR";

    [Fact]
    public void Empty_input_yields_nothing()
    {
        var result = IcsImporter.Parse("");
        Assert.Empty(result.Slots);
        Assert.Empty(result.Deadlines);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void A_weekly_event_with_byday_becomes_a_slot_per_listed_day()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Algorithms lecture\n" +
            "DTSTART:20260907T090000\n" +
            "DTEND:20260907T103000\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=MO,WE\n" +
            "CATEGORIES:CS210"));

        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(new[] { WeekDay.Mon, WeekDay.Wed }, result.Slots.Select(s => s.Day));
        Assert.All(result.Slots, s =>
        {
            Assert.Equal("Algorithms lecture", s.Title);
            Assert.Equal("CS210", s.Course);
            Assert.Equal(new TimeOnly(9, 0), s.Start);
            Assert.Equal(new TimeOnly(10, 30), s.End);
        });
    }

    [Fact]
    public void A_weekly_event_without_byday_lands_on_the_start_days_weekday()
    {
        // 2026-09-08 is a Tuesday.
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Stats seminar\n" +
            "DTSTART:20260908T140000\n" +
            "RRULE:FREQ=WEEKLY"));

        var slot = Assert.Single(result.Slots);
        Assert.Equal(WeekDay.Tue, slot.Day);
    }

    [Fact]
    public void A_missing_dtend_defaults_the_slot_to_one_hour()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Lab\n" +
            "DTSTART:20260907T140000\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=FR"));

        var slot = Assert.Single(result.Slots);
        Assert.Equal(new TimeOnly(14, 0), slot.Start);
        Assert.Equal(new TimeOnly(15, 0), slot.End);
    }

    [Fact]
    public void A_one_off_event_becomes_a_deadline()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Essay due\n" +
            "DTSTART:20261211T170000\n" +
            "CATEGORIES:ENG201,other"));

        var deadline = Assert.Single(result.Deadlines);
        Assert.Equal("Essay due", deadline.Title);
        Assert.Equal(new DateOnly(2026, 12, 11), deadline.Date);
        Assert.Equal("ENG201", deadline.Course);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public void A_date_only_one_off_still_maps_to_a_deadline()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Exam\nDTSTART:20261215"));

        Assert.Equal(new DateOnly(2026, 12, 15), Assert.Single(result.Deadlines).Date);
    }

    [Theory]
    [InlineData("SUMMARY:Daily standup\nDTSTART:20260907T090000\nRRULE:FREQ=DAILY")]
    [InlineData("SUMMARY:Monthly review\nDTSTART:20260907T090000\nRRULE:FREQ=MONTHLY")]
    [InlineData("SUMMARY:All-day weekly\nDTSTART:20260907\nRRULE:FREQ=WEEKLY")]
    [InlineData("SUMMARY:No start at all")]
    [InlineData("DTSTART:20260907T090000")]
    [InlineData("SUMMARY:Bad stamp\nDTSTART:tomorrow-ish")]
    public void Unmappable_events_are_counted_and_skipped(string ev)
    {
        var result = IcsImporter.Parse(Calendar(ev));

        Assert.Equal(1, result.Skipped);
        Assert.Empty(result.Slots);
        Assert.Empty(result.Deadlines);
    }

    [Fact]
    public void Folded_lines_are_unfolded_before_parsing()
    {
        // RFC 5545: a line starting with a space continues the previous one,
        // with the fold marker (newline + one space) removed on unfold.
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Advanced quantum\n  mechanics lecture\n" +
            "DTSTART:20260907T110000\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=TH"));

        Assert.Equal("Advanced quantum mechanics lecture", Assert.Single(result.Slots).Title);
    }

    [Fact]
    public void Property_parameters_and_a_trailing_z_are_tolerated()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Lecture\n" +
            "DTSTART;TZID=Europe/Vilnius:20260907T090000Z\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=MO"));

        var slot = Assert.Single(result.Slots);
        // Times are read as wall-clock by design; the Z changes nothing.
        Assert.Equal(new TimeOnly(9, 0), slot.Start);
    }

    [Fact]
    public void An_end_before_the_start_falls_back_to_one_hour()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Odd export\n" +
            "DTSTART:20260907T150000\n" +
            "DTEND:20260907T140000\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=MO"));

        var slot = Assert.Single(result.Slots);
        Assert.Equal(new TimeOnly(16, 0), slot.End);
    }

    [Fact]
    public void Ordinal_byday_prefixes_are_read_as_plain_weekdays()
    {
        var result = IcsImporter.Parse(Calendar(
            "SUMMARY:Tutorial\n" +
            "DTSTART:20260907T100000\n" +
            "RRULE:FREQ=WEEKLY;BYDAY=2MO"));

        Assert.Equal(WeekDay.Mon, Assert.Single(result.Slots).Day);
    }
}

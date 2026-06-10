using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>What an .ics import produced — ready-to-merge models plus a count
/// of events that couldn't be mapped.</summary>
public class IcsImportResult
{
    public List<ClassSlot> Slots { get; } = new();
    public List<Deadline> Deadlines { get; } = new();
    public int Skipped { get; set; }
}

/// <summary>
/// A deliberately small .ics reader for university timetable exports:
/// weekly-recurring VEVENTs (FREQ=WEEKLY, with or without BYDAY) become
/// class slots; one-off events become deadlines. CATEGORIES doubles as the
/// course tag. Anything else — other recurrence rules, malformed dates —
/// is counted and skipped rather than guessed at. Times are taken as
/// wall-clock; timezone conversion is out of scope for a study timetable.
/// </summary>
public static class IcsImporter
{
    public static IcsImportResult Parse(string text)
    {
        var result = new IcsImportResult();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var ev in Events(Unfold(text)))
        {
            var summary = ev.GetValueOrDefault("SUMMARY", "").Trim();
            var dtStart = ev.GetValueOrDefault("DTSTART");
            if (string.IsNullOrEmpty(summary) || dtStart is null ||
                !TryParseStamp(dtStart, out var start, out var hasTime))
            {
                result.Skipped++;
                continue;
            }

            var course = ev.GetValueOrDefault("CATEGORIES")?.Split(',')[0].Trim();
            if (string.IsNullOrWhiteSpace(course))
                course = null;

            var rrule = ev.GetValueOrDefault("RRULE");

            if (rrule is null)
            {
                result.Deadlines.Add(new Deadline
                {
                    Date = DateOnly.FromDateTime(start),
                    Title = summary,
                    Course = course
                });
                continue;
            }

            if (!rrule.Contains("FREQ=WEEKLY", StringComparison.OrdinalIgnoreCase) || !hasTime)
            {
                result.Skipped++;
                continue;
            }

            var end = ev.GetValueOrDefault("DTEND") is { } dtEnd &&
                      TryParseStamp(dtEnd, out var parsedEnd, out _)
                ? parsedEnd
                : start.AddHours(1);

            var startTime = TimeOnly.FromDateTime(start);
            var endTime = TimeOnly.FromDateTime(end);
            if (endTime <= startTime)
                endTime = startTime.AddHours(1);

            foreach (var day in RecurrenceDays(rrule, start))
            {
                result.Slots.Add(new ClassSlot
                {
                    Day = day,
                    Start = startTime,
                    End = endTime,
                    Title = summary,
                    Course = course
                });
            }
        }

        return result;
    }

    /// <summary>RFC 5545 line unfolding: a line starting with a space or tab
    /// continues the previous one.</summary>
    private static List<string> Unfold(string text)
    {
        var raw = text.Replace("\r\n", "\n").Split('\n');
        var lines = new List<string>();

        foreach (var line in raw)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && lines.Count > 0)
                lines[^1] += line[1..];
            else
                lines.Add(line);
        }

        return lines;
    }

    /// <summary>Yield one property dictionary per VEVENT. Property parameters
    /// (";TZID=...") are dropped — only the bare name keys the dictionary.</summary>
    private static IEnumerable<Dictionary<string, string>> Events(List<string> lines)
    {
        Dictionary<string, string>? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            if (line.StartsWith("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                    yield return current;
                current = null;
                continue;
            }
            if (current is null)
                continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var name = line[..colon].Split(';')[0].Trim().ToUpperInvariant();
            current[name] = line[(colon + 1)..].Trim();
        }
    }

    /// <summary>Parse "20260914T090000(Z)" or "20260914". A trailing Z is
    /// accepted but the time is still read as wall-clock.</summary>
    private static bool TryParseStamp(string value, out DateTime stamp, out bool hasTime)
    {
        stamp = default;
        hasTime = value.Contains('T');
        var cleaned = value.TrimEnd('Z');

        var formats = hasTime
            ? new[] { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm" }
            : new[] { "yyyyMMdd" };

        return DateTime.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out stamp);
    }

    /// <summary>BYDAY=MO,WE,FR → those weekdays; no BYDAY → DTSTART's day.</summary>
    private static IEnumerable<WeekDay> RecurrenceDays(string rrule, DateTime start)
    {
        var byDay = rrule.Split(';')
            .Select(part => part.Split('='))
            .Where(kv => kv.Length == 2 && kv[0].Equals("BYDAY", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv[1])
            .FirstOrDefault();

        if (byDay is null)
        {
            yield return FromDayOfWeek(start.DayOfWeek);
            yield break;
        }

        foreach (var code in byDay.Split(','))
        {
            // Strip any ordinal prefix ("2MO" → "MO").
            var bare = new string(code.Trim().Where(char.IsLetter).ToArray());
            WeekDay? day = bare.ToUpperInvariant() switch
            {
                "MO" => WeekDay.Mon,
                "TU" => WeekDay.Tue,
                "WE" => WeekDay.Wed,
                "TH" => WeekDay.Thu,
                "FR" => WeekDay.Fri,
                "SA" => WeekDay.Sat,
                "SU" => WeekDay.Sun,
                _ => null
            };
            if (day is { } d)
                yield return d;
        }
    }

    private static WeekDay FromDayOfWeek(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => WeekDay.Mon,
        DayOfWeek.Tuesday => WeekDay.Tue,
        DayOfWeek.Wednesday => WeekDay.Wed,
        DayOfWeek.Thursday => WeekDay.Thu,
        DayOfWeek.Friday => WeekDay.Fri,
        DayOfWeek.Saturday => WeekDay.Sat,
        _ => WeekDay.Sun
    };
}

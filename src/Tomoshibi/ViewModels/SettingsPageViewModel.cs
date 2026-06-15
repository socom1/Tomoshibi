using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The 設定 · settings destination — one page gathering what was scattered:
/// the timer numbers and alert toggles (shared instance with the today gear,
/// so they stay in sync), startup behaviour, the grading scale, the music
/// player's knobs, and where the data lives on disk.
/// </summary>
public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    /// <summary>The same instance the today-page gear flyout binds — edits
    /// in either place are one set of values.</summary>
    public SettingsViewModel Timer { get; }

    /// <summary>The floating player's state, for folder/shuffle/volume.</summary>
    public MusicPlayerViewModel Music { get; }

    /// <summary>The subjects page, for the grading-scale picker.</summary>
    public SubjectsViewModel Subjects { get; }

    /// <summary>Where the JSON state file lives.</summary>
    public string DataLocation { get; }

    [ObservableProperty]
    private bool _showWelcome;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _remindersEnabled;


    public string VersionLabel => "灯火 · tomoshibi — v1.5";

    public SettingsPageViewModel(AppState state, Action save,
                                 SettingsViewModel timer,
                                 MusicPlayerViewModel music,
                                 SubjectsViewModel subjects,
                                 string dataLocation)
    {
        _state = state;
        _save = save;
        Timer = timer;
        Music = music;
        Subjects = subjects;
        DataLocation = dataLocation;

        _showWelcome = state.ShowWelcome;
        _closeToTray = state.CloseToTray;
        _remindersEnabled = state.RemindersEnabled;
    }

    partial void OnShowWelcomeChanged(bool value)
    {
        _state.ShowWelcome = value;
        _save();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _state.CloseToTray = value;
        _save();
    }

    partial void OnRemindersEnabledChanged(bool value)
    {
        _state.RemindersEnabled = value;
        _save();
    }

    // ===================== Export / backup =====================

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Every subject and assessment as a spreadsheet — one row per
    /// assessment (subjects with none get a single summary row). The standing
    /// comes straight off the live subject rows so drop rules are respected.</summary>
    public string BuildGradesCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("subject,code,year,semester,credits,target_percent," +
                      "target_hours_per_week,standing_percent,assessment,category," +
                      "weight_percent,grade_percent,date");

        foreach (var row in Subjects.Items)
        {
            var m = row.Model;
            var prefix = string.Join(",",
                Csv(m.Name), Csv(m.Code),
                m.Year.ToString(Inv), m.Semester.ToString(Inv),
                m.Credits.ToString("0.#", Inv),
                m.TargetPercent?.ToString("0.#", Inv) ?? "",
                m.TargetHoursPerWeek?.ToString("0.#", Inv) ?? "",
                row.CurrentPercent?.ToString("0.#", Inv) ?? "");

            if (m.Assessments.Count == 0)
            {
                sb.AppendLine($"{prefix},,,,,");
                continue;
            }

            foreach (var a in m.Assessments)
            {
                sb.AppendLine(string.Join(",", prefix,
                    Csv(a.Title), Csv(a.Category),
                    a.Weight.ToString("0.#", Inv),
                    a.Grade?.ToString("0.#", Inv) ?? "",
                    a.Date?.ToString("yyyy-MM-dd") ?? ""));
            }
        }

        return sb.ToString();
    }

    /// <summary>The timetable as an .ics calendar: classes as weekly-recurring
    /// events, dated exams and open due-dated tickets as all-day events. Drops
    /// into Google/Apple Calendar.</summary>
    public string BuildTimetableIcs()
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n"); // RFC 5545 wants CRLF

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//tomoshibi//timetable//EN");
        Line("CALSCALE:GREGORIAN");

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", Inv);
        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var slot in _state.ClassSlots)
        {
            var first = NextDateForDay(today, slot.Day);
            var start = first.ToDateTime(slot.Start);
            var end = first.ToDateTime(slot.End);
            var summary = string.IsNullOrWhiteSpace(slot.Course)
                ? slot.Title : $"{slot.Title} ({slot.Course})";

            Line("BEGIN:VEVENT");
            Line($"UID:{Guid.NewGuid()}@tomoshibi");
            Line($"DTSTAMP:{stamp}");
            Line($"DTSTART:{start:yyyyMMdd'T'HHmmss}");
            Line($"DTEND:{end:yyyyMMdd'T'HHmmss}");
            Line($"RRULE:FREQ=WEEKLY;BYDAY={IcsDay(slot.Day)}");
            Line($"SUMMARY:{IcsText(summary)}");
            Line("END:VEVENT");
        }

        void AllDay(DateOnly date, string summary)
        {
            Line("BEGIN:VEVENT");
            Line($"UID:{Guid.NewGuid()}@tomoshibi");
            Line($"DTSTAMP:{stamp}");
            Line($"DTSTART;VALUE=DATE:{date:yyyyMMdd}");
            Line($"DTEND;VALUE=DATE:{date.AddDays(1):yyyyMMdd}");
            Line($"SUMMARY:{IcsText(summary)}");
            Line("END:VEVENT");
        }

        foreach (var subject in _state.Subjects)
            foreach (var a in subject.Assessments.Where(a => a.Date is not null))
                AllDay(a.Date!.Value, $"{a.Title} ({subject.Code ?? subject.Name})");

        foreach (var t in _state.Todos.Where(t => t.Due is not null && t.Status != TodoStatus.Done))
            AllDay(t.Due!.Value, $"due: {t.Title}");

        Line("END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>The whole state as indented JSON — a portable backup that the
    /// app can load straight back. Same shape the live file is written in.</summary>
    public string BuildBackupJson() =>
        JsonSerializer.Serialize(_state, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    /// <summary>Next date on/after <paramref name="from"/> falling on the given
    /// academic weekday (Mon-first), for the first occurrence of a recurrence.</summary>
    private static DateOnly NextDateForDay(DateOnly from, WeekDay day)
    {
        var target = (DayOfWeek)(((int)day + 1) % 7); // WeekDay Mon=0 → DayOfWeek Mon=1
        var diff = ((int)target - (int)from.DayOfWeek + 7) % 7;
        return from.AddDays(diff);
    }

    private static string IcsDay(WeekDay day) => day switch
    {
        WeekDay.Mon => "MO", WeekDay.Tue => "TU", WeekDay.Wed => "WE",
        WeekDay.Thu => "TH", WeekDay.Fri => "FR", WeekDay.Sat => "SA", _ => "SU"
    };

    private static string IcsText(string s) => s
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private static string Csv(string? field)
    {
        field ??= string.Empty;
        return field.Contains(',') || field.Contains('"') || field.Contains('\n')
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
    }
}

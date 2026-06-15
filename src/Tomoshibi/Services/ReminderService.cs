using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Fires desktop reminders for upcoming deadlines and exams — once when an
/// item first comes within three days, and again on the day itself. Fired
/// keys are remembered in state so a reminder never repeats, and keys for
/// dates that have passed are swept so the set can't grow without bound.
/// </summary>
public class ReminderService
{
    /// <summary>Cap per check so a fresh backlog doesn't burst all at once;
    /// the rest fire on the next day-watcher tick.</summary>
    private const int MaxPerCheck = 4;

    private readonly INotificationService _notify;

    public ReminderService(INotificationService notify) => _notify = notify;

    private record Reminder(string Key, string Title, string Body, DateOnly Date);

    /// <summary>Evaluate the state's deadlines and fire any due reminders.
    /// Returns true when something changed (a fire or a sweep) so the caller
    /// can persist the updated fired-keys set.</summary>
    public bool Check(AppState state)
    {
        if (!state.RemindersEnabled)
            return false;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var changed = false;

        // Drop keys whose date has passed.
        var swept = state.NotifiedReminders.RemoveAll(k => KeyDate(k) is { } d && d < today);
        if (swept > 0)
            changed = true;

        var seen = new HashSet<string>(state.NotifiedReminders);
        var fired = 0;

        foreach (var r in CollectDue(state, today).OrderBy(r => r.Date))
        {
            if (fired >= MaxPerCheck)
                break;
            if (!seen.Add(r.Key))
                continue; // already notified

            _notify.Notify(r.Title, r.Body);
            state.NotifiedReminders.Add(r.Key);
            fired++;
            changed = true;
        }

        return changed;
    }

    private static IEnumerable<Reminder> CollectDue(AppState state, DateOnly today)
    {
        // Open, due-dated tickets.
        foreach (var t in state.Todos.Where(t => t.Status != TodoStatus.Done && t.Due is not null))
        {
            var d = t.Due!.Value;
            if (Threshold(d, today) is not { } th)
                continue;
            yield return new Reminder($"todo:{t.Id}:{th}:{d:yyyyMMdd}",
                $"deadline {Word(d, today)}", t.Title, d);
        }

        // Dated, still-ungraded assessments (exams, hand-ins).
        foreach (var s in state.Subjects)
            foreach (var a in s.Assessments.Where(a => a.Grade is null && a.Date is not null))
            {
                var d = a.Date!.Value;
                if (Threshold(d, today) is not { } th)
                    continue;
                var code = s.Code ?? s.Name;
                yield return new Reminder($"exam:{a.Id}:{th}:{d:yyyyMMdd}",
                    $"exam {Word(d, today)}", $"{a.Title} · {code}", d);
            }
    }

    /// <summary>"today" on the day, "soon" within three days, null otherwise
    /// (too far out, or already overdue — no nagging).</summary>
    private static string? Threshold(DateOnly date, DateOnly today)
    {
        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "today",
            >= 1 and <= 3 => "soon",
            _ => null
        };
    }

    private static string Word(DateOnly date, DateOnly today) =>
        (date.DayNumber - today.DayNumber) switch
        {
            <= 0 => "today",
            1 => "tomorrow",
            var n => $"in {n} days"
        };

    private static DateOnly? KeyDate(string key)
    {
        var i = key.LastIndexOf(':');
        if (i < 0 || i + 1 >= key.Length)
            return null;
        return DateOnly.TryParseExact(key[(i + 1)..], "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}

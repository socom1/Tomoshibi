using System;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// The midnight rollover. When the calendar date moves past the one stored in
/// state, the finished day is banked — its stats into <see cref="AppState.History"/>,
/// its intention + reflection into <see cref="AppState.Journal"/> — and the
/// live fields are cleared for the new day. Pure state-in, state-out so the
/// banking rules (which the streak and the look-back depend on) stay testable.
/// </summary>
public static class DailyReset
{
    /// <summary>A year of history/look-back is plenty; keeps the file small.</summary>
    private const int MaxBankedDays = 400;

    /// <summary>Roll the state over to <paramref name="today"/> if the stored
    /// date is older. Returns true when anything changed (callers save).</summary>
    public static bool Apply(AppState state, DateOnly today)
    {
        var changed = false;

        if (state.Today.Date != today)
        {
            // Bank the finished day before replacing it — the streak and the
            // future calendar need the history. Empty days aren't recorded.
            if (state.Today.CompletedSessions > 0 || state.Today.FocusedMinutes > 0)
            {
                state.History.RemoveAll(d => d.Date == state.Today.Date);
                state.History.Add(state.Today);

                if (state.History.Count > MaxBankedDays)
                    state.History.RemoveRange(0, state.History.Count - MaxBankedDays);
            }

            state.Today = new DailyStats { Date = today };
            changed = true;
        }

        if (state.IntentionDate != today)
        {
            // Bank the finished day's intention + reflection into the journal
            // before clearing them, so the stats page keeps the look-back.
            if (!string.IsNullOrWhiteSpace(state.DailyIntention) ||
                !string.IsNullOrWhiteSpace(state.DailyReflection))
            {
                state.Journal.RemoveAll(n => n.Date == state.IntentionDate);
                state.Journal.Add(new DayNote
                {
                    Date = state.IntentionDate,
                    Intention = state.DailyIntention,
                    IntentionKept = state.IntentionKept,
                    Reflection = state.DailyReflection
                });

                if (state.Journal.Count > MaxBankedDays)
                    state.Journal.RemoveRange(0, state.Journal.Count - MaxBankedDays);
            }

            state.DailyIntention = string.Empty;
            state.IntentionKept = false;
            state.DailyReflection = string.Empty;
            state.IntentionDate = today;
            changed = true;
        }

        return changed;
    }
}

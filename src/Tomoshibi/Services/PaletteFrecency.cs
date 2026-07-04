using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Frecency for the command palette: what you run often and recently floats
/// to the top, especially on an empty query. The score is count decayed by
/// a one-week half-life — four uses a fortnight ago weigh the same as one
/// use today, and everything fades toward zero rather than accumulating
/// forever. The usage table is capped; the coldest entries fall off first.
/// </summary>
public static class PaletteFrecency
{
    private const int MaxEntries = 50;
    private const double HalfLifeDays = 7;

    /// <summary>Note a run: bump the count, refresh the timestamp, and keep
    /// the table small by dropping the coldest entries past the cap.</summary>
    public static void Record(Dictionary<string, PaletteUse> usage, string key,
        DateTimeOffset now)
    {
        if (usage.TryGetValue(key, out var use))
        {
            use.Count++;
            use.LastUsed = now;
        }
        else
        {
            usage[key] = new PaletteUse { Count = 1, LastUsed = now };
        }

        while (usage.Count > MaxEntries)
        {
            var coldest = usage.MinBy(kv => Score(kv.Value, now)).Key;
            usage.Remove(coldest);
        }
    }

    /// <summary>The key's current warmth; 0 for anything never run.</summary>
    public static double Score(Dictionary<string, PaletteUse> usage, string key,
        DateTimeOffset now) =>
        usage.TryGetValue(key, out var use) ? Score(use, now) : 0;

    private static double Score(PaletteUse use, DateTimeOffset now)
    {
        var days = Math.Max(0, (now - use.LastUsed).TotalDays);
        return use.Count * Math.Pow(0.5, days / HalfLifeDays);
    }
}

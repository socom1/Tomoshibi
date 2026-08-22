using System;
using System.Collections.Generic;

namespace Tomoshibi.Services;

/// <summary>
/// Splits overlapping time ranges into side-by-side lanes, the way a calendar
/// week view does. Two classes at the same hour used to draw on top of each
/// other, so whichever sorted last simply hid the other — no error, no hint,
/// just a timetable quietly missing a class.
///
/// <para>Lanes are assigned per cluster rather than per day: a run of ranges
/// that actually overlap each other shares a width, and a later class that
/// clears the whole cluster starts again at full width. Without that, one
/// double-booked morning would squeeze every other class on that day.</para>
/// </summary>
public static class SlotLanes
{
    /// <summary>Which lane a range sits in, and how many lanes its cluster
    /// needs — <c>Index</c> of <c>Count</c>, both 0-based/1-based as named.</summary>
    public readonly record struct Lane(int Index, int Count);

    /// <summary>Assign lanes to one day's ranges.
    ///
    /// <para><paramref name="ranges"/> must be ordered by start; the caller
    /// already sorts that way to draw them. Returns one lane per input, in the
    /// same order.</para></summary>
    public static IReadOnlyList<Lane> Assign(IReadOnlyList<(TimeOnly Start, TimeOnly End)> ranges)
    {
        var lanes = new Lane[ranges.Count];
        var cluster = 0;

        while (cluster < ranges.Count)
        {
            // Grow the cluster while anything still starts before the latest
            // end seen so far — that's what makes a chain of partial overlaps
            // one group rather than several.
            var clusterEnd = Max(ranges[cluster]);
            var next = cluster + 1;
            while (next < ranges.Count && ranges[next].Start < clusterEnd)
            {
                clusterEnd = Later(clusterEnd, Max(ranges[next]));
                next++;
            }

            // First lane whose previous occupant has finished, or a new one.
            var laneEnds = new List<TimeOnly>();
            var chosen = new int[next - cluster];

            for (var i = cluster; i < next; i++)
            {
                var placed = -1;
                for (var lane = 0; lane < laneEnds.Count; lane++)
                {
                    if (laneEnds[lane] <= ranges[i].Start)
                    {
                        laneEnds[lane] = Max(ranges[i]);
                        placed = lane;
                        break;
                    }
                }

                if (placed < 0)
                {
                    laneEnds.Add(Max(ranges[i]));
                    placed = laneEnds.Count - 1;
                }

                chosen[i - cluster] = placed;
            }

            for (var i = cluster; i < next; i++)
                lanes[i] = new Lane(chosen[i - cluster], laneEnds.Count);

            cluster = next;
        }

        return lanes;
    }

    /// <summary>A range that ends at or before it starts still occupies its
    /// start instant, so it can't swallow the ones after it.</summary>
    private static TimeOnly Max((TimeOnly Start, TimeOnly End) range) =>
        range.End > range.Start ? range.End : range.Start;

    private static TimeOnly Later(TimeOnly a, TimeOnly b) => a > b ? a : b;
}

using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Lane assignment for the week grid. Two classes at the same hour
/// used to draw on top of each other, so one simply vanished — no error, just
/// a timetable quietly missing a class. The interesting cases are the ones
/// that aren't a clean pair: chains of partial overlaps, a gap that frees a
/// lane up again, and a class that clears the whole pile.</summary>
public class SlotLanesTests
{
    private static (TimeOnly, TimeOnly) At(string start, string end) =>
        (TimeOnly.Parse(start), TimeOnly.Parse(end));

    private static IReadOnlyList<SlotLanes.Lane> Assign(params (TimeOnly, TimeOnly)[] ranges)
        => SlotLanes.Assign(ranges);

    [Fact]
    public void A_day_with_no_clashes_leaves_every_class_full_width()
    {
        var lanes = Assign(At("09:00", "10:00"), At("11:00", "12:00"));

        Assert.All(lanes, l => Assert.Equal(new SlotLanes.Lane(0, 1), l));
    }

    [Fact]
    public void Two_classes_at_the_same_hour_get_a_lane_each()
    {
        var lanes = Assign(At("09:00", "10:00"), At("09:00", "10:00"));

        Assert.Equal(new SlotLanes.Lane(0, 2), lanes[0]);
        Assert.Equal(new SlotLanes.Lane(1, 2), lanes[1]);
    }

    [Fact]
    public void A_partial_overlap_still_counts_as_a_clash()
    {
        var lanes = Assign(At("09:00", "10:30"), At("10:00", "11:00"));

        Assert.Equal(2, lanes[0].Count);
        Assert.NotEqual(lanes[0].Index, lanes[1].Index);
    }

    [Fact]
    public void Touching_at_the_boundary_is_not_a_clash()
    {
        // A class ending at 10:00 and one starting at 10:00 don't overlap —
        // treating them as a clash would halve the width of half the timetable.
        var lanes = Assign(At("09:00", "10:00"), At("10:00", "11:00"));

        Assert.All(lanes, l => Assert.Equal(1, l.Count));
    }

    [Fact]
    public void A_chain_of_partial_overlaps_is_one_cluster()
    {
        // A overlaps B, B overlaps C, A and C don't touch. All three share a
        // cluster so they're measured against the same width — but only two
        // lanes are needed, because C can take the one A has finished with.
        var lanes = Assign(At("09:00", "10:30"), At("10:00", "11:30"), At("11:00", "12:00"));

        Assert.All(lanes, l => Assert.Equal(2, l.Count));
        Assert.NotEqual(lanes[0].Index, lanes[1].Index);   // A and B clash
        Assert.NotEqual(lanes[1].Index, lanes[2].Index);   // B and C clash
        Assert.Equal(lanes[0].Index, lanes[2].Index);      // A and C don't
    }

    [Fact]
    public void A_lane_frees_up_once_its_class_has_finished()
    {
        // The long one spans both short ones, which follow each other. Two
        // lanes is enough — the third class reuses the first's.
        var lanes = Assign(At("09:00", "12:00"), At("09:00", "10:00"), At("10:00", "11:00"));

        Assert.All(lanes, l => Assert.Equal(2, l.Count));
        Assert.Equal(lanes[1].Index, lanes[2].Index);
    }

    [Fact]
    public void A_later_class_that_clears_the_pile_goes_back_to_full_width()
    {
        // The whole point of clustering rather than counting per day: an
        // afternoon class shouldn't be narrowed by a double-booked morning.
        var lanes = Assign(At("09:00", "10:00"), At("09:00", "10:00"), At("14:00", "15:00"));

        Assert.Equal(2, lanes[0].Count);
        Assert.Equal(2, lanes[1].Count);
        Assert.Equal(new SlotLanes.Lane(0, 1), lanes[2]);
    }

    [Fact]
    public void A_zero_length_class_cant_swallow_the_ones_after_it()
    {
        // End at or before start is nonsense a hand-edited file can produce.
        // It must not read as an interval that never closes.
        var lanes = Assign(At("09:00", "09:00"), At("10:00", "11:00"));

        Assert.All(lanes, l => Assert.Equal(1, l.Count));
    }

    [Fact]
    public void An_empty_day_is_fine()
    {
        Assert.Empty(SlotLanes.Assign(Array.Empty<(TimeOnly, TimeOnly)>()));
    }

    [Fact]
    public void Every_class_in_a_cluster_gets_a_distinct_lane_under_its_count()
    {
        var lanes = Assign(At("09:00", "12:00"), At("09:30", "12:00"), At("10:00", "12:00"));

        Assert.All(lanes, l =>
        {
            Assert.InRange(l.Index, 0, l.Count - 1);
            Assert.Equal(3, l.Count);
        });
        Assert.Equal(3, lanes.Select(l => l.Index).Distinct().Count());
    }
}

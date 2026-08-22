using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.ViewModels;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The week grid measures itself from the timetable, and a slot's
/// position is derived from that window. Both were regressions during
/// development — every class stacked in one row, then every block an hour
/// low — and neither was catchable without driving the view model.</summary>
public class TimetableViewModelTests
{
    private static ClassSlot Slot(WeekDay day, string start, string end, string title = "Lecture")
        => new()
        {
            Day = day,
            Start = TimeOnly.Parse(start),
            End = TimeOnly.Parse(end),
            Title = title,
            Course = "CS201"
        };

    private static TimetableViewModel Vm(params ClassSlot[] slots)
    {
        var state = new AppState();
        state.ClassSlots.AddRange(slots);
        return new TimetableViewModel(state, () => { });
    }

    [Fact]
    public void An_empty_timetable_falls_back_to_the_default_window()
    {
        var vm = Vm();

        Assert.Equal(ClassSlotItemViewModel.DefaultEndHour - ClassSlotItemViewModel.DefaultStartHour,
                     vm.HourLabels.Count);
        Assert.Equal("08", vm.HourLabels[0]);
    }

    [Fact]
    public void The_window_wraps_the_hours_actually_scheduled()
    {
        // 09:00–16:00 of classes: an hour of air either side → 08..17.
        var vm = Vm(Slot(WeekDay.Mon, "09:00", "11:00"),
                    Slot(WeekDay.Fri, "14:00", "16:00"));

        Assert.Equal("08", vm.HourLabels[0]);
        Assert.Equal("16", vm.HourLabels[^1]);
        Assert.Equal(9, vm.HourLabels.Count);
    }

    [Fact]
    public void A_late_evening_class_stretches_the_window_to_reach_it()
    {
        var vm = Vm(Slot(WeekDay.Wed, "18:00", "21:00"));

        Assert.Contains("20", vm.HourLabels);
        Assert.Equal("17", vm.HourLabels[0]);
    }

    [Fact]
    public void A_single_short_class_still_gets_a_readable_grid()
    {
        // One 30-minute class shouldn't collapse the page to two rows.
        var vm = Vm(Slot(WeekDay.Tue, "10:00", "10:30"));

        Assert.True(vm.HourLabels.Count >= 6);
    }

    [Fact]
    public void A_block_sits_where_its_real_time_puts_it()
    {
        var vm = Vm(Slot(WeekDay.Mon, "09:00", "11:00"));
        var block = Assert.Single(vm.Slots);

        // Window starts at 08, so a 09:00 class is one row down and two tall.
        Assert.Equal(ClassSlotItemViewModel.RowHeight, block.BlockMargin.Top);
        Assert.Equal(ClassSlotItemViewModel.RowHeight * 2, block.BlockHeight);
    }

    [Fact]
    public void A_half_past_class_lands_halfway_down_the_row()
    {
        // The whole reason for placing by offset rather than by grid row:
        // 11:30 used to round to 11:00.
        var vm = Vm(Slot(WeekDay.Wed, "11:30", "12:15"));
        var block = Assert.Single(vm.Slots);

        var hoursFromTop = block.BlockMargin.Top / ClassSlotItemViewModel.RowHeight;
        Assert.Equal(11.5 - int.Parse(vm.HourLabels[0]), hoursFromTop, 3);
    }

    [Fact]
    public void Adding_a_class_re_measures_the_window()
    {
        var vm = Vm(Slot(WeekDay.Mon, "09:00", "10:00"));
        var before = vm.HourLabels.Count;

        vm.NewSlotDay = WeekDay.Thu;
        vm.NewSlotStart = new TimeSpan(19, 0, 0);
        vm.NewSlotEnd = new TimeSpan(21, 0, 0);
        vm.NewSlotTitle = "Evening lab";
        vm.AddSlotCommand.Execute(null);

        Assert.Equal(2, vm.Slots.Count);
        Assert.True(vm.HourLabels.Count > before, "the grid should have grown to reach 21:00");
        Assert.Contains("20", vm.HourLabels);
    }

    [Fact]
    public void A_class_with_no_name_is_refused_and_says_why()
    {
        var vm = Vm();
        vm.NewSlotTitle = "   ";

        vm.AddSlotCommand.Execute(null);

        Assert.Empty(vm.Slots);
        Assert.True(vm.IsSlotTitleInvalid);
        Assert.True(vm.HasSlotError);
        Assert.Contains("name", vm.SlotError);
    }

    [Fact]
    public void An_end_before_the_start_is_refused_and_says_why()
    {
        var vm = Vm();
        vm.NewSlotTitle = "Backwards";
        vm.NewSlotStart = new TimeSpan(15, 0, 0);
        vm.NewSlotEnd = new TimeSpan(14, 0, 0);

        vm.AddSlotCommand.Execute(null);

        Assert.Empty(vm.Slots);
        Assert.False(vm.IsSlotTitleInvalid);      // the title was fine
        Assert.Contains("end time", vm.SlotError);
    }

    [Fact]
    public void The_complaint_clears_as_soon_as_theyre_fixing_it()
    {
        var vm = Vm();
        vm.AddSlotCommand.Execute(null);
        Assert.True(vm.IsSlotTitleInvalid);

        vm.NewSlotTitle = "Algorithms";

        Assert.False(vm.IsSlotTitleInvalid);
        Assert.False(vm.HasSlotError);
    }

    [Fact]
    public void Clearing_a_time_box_doesnt_throw()
    {
        // NumericUpDown.Value is decimal?; binding null to a decimal used to
        // raise InvalidCastException in the user's face.
        var vm = Vm();
        var before = vm.NewSlotStart;

        vm.StartHour = null;
        vm.StartMinute = null;

        Assert.Equal(before, vm.NewSlotStart);
    }

    // ---- clashing classes share the day column ----

    [Fact]
    public void Two_classes_at_the_same_hour_each_get_half_the_day()
    {
        var vm = Vm(Slot(WeekDay.Mon, "09:00", "10:00", "Algorithms"),
                    Slot(WeekDay.Mon, "09:00", "10:00", "Statistics"));

        Assert.All(vm.Slots, s => Assert.Equal(2, s.LaneCount));
        Assert.Equal(new[] { 0, 6 }, vm.Slots.Select(s => s.LaneColumn).OrderBy(c => c));
        Assert.All(vm.Slots, s => Assert.Equal(6, s.LaneSpan));
    }

    [Fact]
    public void A_clash_on_one_day_doesnt_narrow_another()
    {
        var vm = Vm(Slot(WeekDay.Mon, "09:00", "10:00", "Algorithms"),
                    Slot(WeekDay.Mon, "09:00", "10:00", "Statistics"),
                    Slot(WeekDay.Tue, "09:00", "10:00", "Japanese"));

        var tuesday = Assert.Single(vm.Slots, s => s.Model.Day == WeekDay.Tue);
        Assert.Equal(1, tuesday.LaneCount);
        Assert.Equal(12, tuesday.LaneSpan);   // the whole day column
        Assert.Equal(0, tuesday.LaneColumn);
    }

    [Fact]
    public void A_class_with_the_hour_to_itself_spans_the_whole_column()
    {
        var vm = Vm(Slot(WeekDay.Wed, "14:00", "15:00"));

        var only = Assert.Single(vm.Slots);
        Assert.Equal(0, only.LaneColumn);
        Assert.Equal(12, only.LaneSpan);
    }

    [Fact]
    public void Beyond_four_at_once_the_extras_stack_rather_than_becoming_slivers()
    {
        var vm = Vm(Slot(WeekDay.Thu, "09:00", "10:00", "one"),
                    Slot(WeekDay.Thu, "09:00", "10:00", "two"),
                    Slot(WeekDay.Thu, "09:00", "10:00", "three"),
                    Slot(WeekDay.Thu, "09:00", "10:00", "four"),
                    Slot(WeekDay.Thu, "09:00", "10:00", "five"));

        // Five lanes would be three columns wide each at best; the grid stops
        // splitting at four and the fifth shares the last lane.
        Assert.All(vm.Slots, s => Assert.Equal(3, s.LaneSpan));
        Assert.All(vm.Slots, s => Assert.InRange(s.LaneColumn, 0, 9));
        Assert.Equal(4, vm.Slots.Select(s => s.LaneColumn).Distinct().Count());
    }
}

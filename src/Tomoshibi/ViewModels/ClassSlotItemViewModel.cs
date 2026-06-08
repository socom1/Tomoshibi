using System;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Read-only row representation of a <see cref="ClassSlot"/>. Holds the model
/// it wraps and exposes both the human-readable labels the row template binds
/// to and the hour-row / day-column indices the week grid uses to place the
/// slot block. Slot start/end snap to the nearest hour for grid placement;
/// the real times still show inside the block.
/// </summary>
public class ClassSlotItemViewModel : ViewModelBase
{
    /// <summary>First hour rendered as a row in the week grid.</summary>
    public const int GridStartHour = 8;

    /// <summary>Last hour rendered as a row in the week grid (inclusive label).</summary>
    public const int GridEndHour = 22;

    public ClassSlot Model { get; }

    public string DayLabel => Model.Day switch
    {
        WeekDay.Mon => "mon",
        WeekDay.Tue => "tue",
        WeekDay.Wed => "wed",
        WeekDay.Thu => "thu",
        WeekDay.Fri => "fri",
        WeekDay.Sat => "sat",
        WeekDay.Sun => "sun",
        _ => "?"
    };

    public string TimeLabel => $"{Model.Start:HH\\:mm}–{Model.End:HH\\:mm}";
    public string Title => Model.Title;
    public string? Course => Model.Course;
    public bool HasCourse => !string.IsNullOrWhiteSpace(Model.Course);

    /// <summary>0..6, Mon..Sun. Used as the grid column for slot placement.</summary>
    public int DayIndex => (int)Model.Day;

    /// <summary>0-based row in the grid (0 = the 8 o'clock row).</summary>
    public int HourRow => Math.Clamp(Model.Start.Hour - GridStartHour,
                                     0, GridEndHour - GridStartHour - 1);

    /// <summary>How many hour rows the block spans; floors and ceilings to the
    /// nearest hour and clamps so a slot never overflows the grid.</summary>
    public int HourSpan
    {
        get
        {
            var hours = Math.Max(1, (int)Math.Ceiling((Model.End - Model.Start).TotalHours));
            return Math.Min(hours, GridEndHour - GridStartHour - HourRow);
        }
    }

    public ClassSlotItemViewModel(ClassSlot model)
    {
        Model = model;
    }
}

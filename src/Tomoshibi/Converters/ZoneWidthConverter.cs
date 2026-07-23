using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tomoshibi.Converters;

/// <summary>
/// Turns a container's available width into a column width for a two-up zone
/// layout in a <c>WrapPanel</c>. Above <see cref="Breakpoint"/> it returns half
/// the width (so two zones sit side by side); below it, the full width (so the
/// second zone wraps underneath and the page reads as one column). The zone
/// carries a fixed horizontal margin, subtracted here so the row still fits.
///
/// Binds against the WrapPanel's own <c>Bounds.Width</c> — which is
/// available-driven (the panel is Stretch + MaxWidth), not child-driven — so
/// there's no measure cycle.
/// </summary>
public class ZoneWidthConverter : IValueConverter
{
    /// <summary>Two columns at or above this width; one below it.</summary>
    private const double Breakpoint = 980;

    /// <summary>Half the horizontal margin a zone carries (matches the 8px in
    /// the zone's <c>Margin</c>), so widths + margins sum back to the row.</summary>
    private const double SideMargin = 8;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width || double.IsNaN(width) || width <= 0)
            return double.NaN; // not measured yet — fall back to Auto for a frame

        // Two zones each carry 2*SideMargin; one zone carries 2*SideMargin.
        // The extra 2px in the two-up case is slack so rounding can't tip the
        // row over its width and wrap the second zone.
        return width >= Breakpoint
            ? Math.Max(0, (width - SideMargin * 4 - 2) / 2)
            : Math.Max(0, width - SideMargin * 2);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

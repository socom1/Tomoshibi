using System;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Data.Converters;

namespace Tomoshibi.Converters;

/// <summary>
/// Maps a 0..1 fraction to a StrokeDashArray that draws that share of a
/// stroked circle. ConverterParameter is "radius,thickness" in pixels.
/// </summary>
public class RingDashConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value switch
        {
            double d => Math.Clamp(d, 0.0, 1.0),
            null => 0.0,
            _ => 0.0
        };

        var parts = (parameter as string ?? "44,6").Split(',');
        var radius = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var thickness = double.Parse(parts[1], CultureInfo.InvariantCulture);

        // Dash entries are in units of stroke thickness.
        var circumference = 2 * Math.PI * radius;
        var fill = Math.Max(0.0001, fraction * circumference / thickness);
        var gap = Math.Max(0.0001, (1 - fraction) * circumference / thickness);

        return new AvaloniaList<double> { fill, gap };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

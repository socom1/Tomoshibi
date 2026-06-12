using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Tomoshibi.Converters;

/// <summary>
/// "x,y x,y …" → the point list a Polyline wants. Lets view models hand the
/// sparkline geometry over as a plain string instead of referencing Avalonia.
/// </summary>
public class StringToPointsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var points = new Points();
        if (value is not string s || s.Length == 0)
            return points;

        foreach (var pair in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = pair.Split(',');
            if (xy.Length == 2 &&
                double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                points.Add(new Point(x, y));
            }
        }

        return points;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

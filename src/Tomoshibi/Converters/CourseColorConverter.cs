using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tomoshibi.Converters;

/// <summary>
/// Maps a course code to a stable accent colour so the same course reads the
/// same everywhere — chips, slots, tags. The hash is computed by hand (FNV-1a)
/// because <see cref="string.GetHashCode()"/> is randomised per process in
/// .NET, which would reshuffle the colours on every launch.
///
/// ConverterParameter "soft" returns a translucent fill (for slot backgrounds);
/// anything else returns the solid accent.
/// </summary>
public class CourseColorConverter : IValueConverter
{
    // Tokyo Night accents — all readable on the ink background.
    private static readonly Color[] Palette =
    {
        Color.Parse("#9ECE6A"), // matcha
        Color.Parse("#7AA2F7"), // blue
        Color.Parse("#F7768E"), // sakura
        Color.Parse("#E0AF68"), // amber
        Color.Parse("#73DACA"), // teal
        Color.Parse("#BB9AF7"), // purple
        Color.Parse("#FF9E64"), // orange
        Color.Parse("#2AC3DE"), // cyan
    };

    private static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#565F89"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;
        var soft = (parameter as string) == "soft";

        if (string.IsNullOrWhiteSpace(code))
            return soft ? new SolidColorBrush(Color.Parse("#22565F89")) : Neutral;

        var color = Palette[StableIndex(code.Trim().ToLowerInvariant()) % Palette.Length];

        if (soft)
            color = Color.FromArgb(0x33, color.R, color.G, color.B);

        return new SolidColorBrush(color);
    }

    private static int StableIndex(string s)
    {
        // FNV-1a 32-bit, masked positive.
        uint hash = 2166136261;
        foreach (var c in s)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return (int)(hash & 0x7FFFFFFF);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tomoshibi.Converters;

/// <summary>Renders any value lowercase — used so enum names (Mon, High)
/// match the app's all-lowercase voice in pickers.</summary>
public class LowercaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString()?.ToLowerInvariant();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

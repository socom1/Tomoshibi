using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Tomoshibi.Models;

namespace Tomoshibi.Converters;

/// <summary>
/// Maps the current Pomodoro phase to its accent brush from the theme:
/// matcha for focus, blue for a short break, sakura for a long break.
/// </summary>
public class PhaseToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            PomodoroPhase.Focus => "MatchaBrush",
            PomodoroPhase.ShortBreak => "BlueBrush",
            PomodoroPhase.LongBreak => "SakuraBrush",
            _ => "MatchaBrush"
        };

        if (Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var brush))
        {
            return brush;
        }

        return Brushes.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

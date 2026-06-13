using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tomoshibi.Services;

/// <summary>
/// Swaps the palette at runtime between the default dark "Tokyo Night" and a
/// light "day" variant. The whole UI references the brush keys through
/// DynamicResource, so overriding those keys at the application level
/// re-themes every view live — no per-view work.
///
/// The accents darken in the light theme so they stay readable on white, and
/// AccentText flips so the accent button's label keeps its contrast.
/// </summary>
public static class ThemeService
{
    private static readonly Dictionary<string, string> Dark = new()
    {
        ["InkBrush"] = "#16161E",
        ["SurfaceBrush"] = "#1F2335",
        ["SurfaceHoverBrush"] = "#2A2E42",
        ["BorderBrush"] = "#2A2E42",
        ["MatchaBrush"] = "#9ECE6A",
        ["MatchaDeepBrush"] = "#8DBD59",
        ["SakuraBrush"] = "#F7768E",
        ["AmberBrush"] = "#E0AF68",
        ["BlueBrush"] = "#7AA2F7",
        ["TextBrush"] = "#C0CAF5",
        ["MutedTextBrush"] = "#565F89",
        ["AccentTextBrush"] = "#16161E",
    };

    private static readonly Dictionary<string, string> Light = new()
    {
        ["InkBrush"] = "#E3E4ED",       // soft paper background
        ["SurfaceBrush"] = "#FFFFFF",   // cards lift off the bg
        ["SurfaceHoverBrush"] = "#EBECF3",
        ["BorderBrush"] = "#CBCDDA",
        ["MatchaBrush"] = "#5F8F34",    // accents darkened for white
        ["MatchaDeepBrush"] = "#4E7A28",
        ["SakuraBrush"] = "#D6536C",
        ["AmberBrush"] = "#B5832F",
        ["BlueBrush"] = "#3E68C9",
        ["TextBrush"] = "#272A3F",
        ["MutedTextBrush"] = "#7E84A3",
        ["AccentTextBrush"] = "#FFFFFF",
    };

    public static void Apply(bool light)
    {
        var app = Application.Current;
        if (app is null)
            return;

        // Keep Fluent's own light/dark in step so its built-in control parts
        // (calendar internals, scrollbars) match.
        app.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;

        var palette = light ? Light : Dark;
        foreach (var (key, hex) in palette)
            app.Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }
}

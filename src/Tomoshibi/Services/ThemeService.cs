using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tomoshibi.Services;

/// <summary>A named palette the shop can sell and the app can apply.</summary>
public record AppTheme(string Id, string Jp, string En, int Price, bool IsLight,
                       IReadOnlyDictionary<string, string> Brushes)
{
    public Color Preview(string key) => Color.Parse(Brushes[key]);
}

/// <summary>
/// The theme catalogue plus the live swap. Every view references the brush
/// keys through DynamicResource, so overriding those keys at the application
/// level re-themes the whole UI instantly — no per-view work. Themes are
/// cosmetic palettes; a couple are free, the rest are bought with embers.
/// </summary>
public static class ThemeService
{
    private static Dictionary<string, string> Map(
        string ink, string surface, string hover, string border, string chip,
        string matcha, string matchaDeep, string sakura, string amber, string blue,
        string text, string muted, string accentText) => new()
    {
        ["InkBrush"] = ink,
        ["SurfaceBrush"] = surface,
        ["SurfaceHoverBrush"] = hover,
        ["BorderBrush"] = border,
        // Chip/tag fill — a step off the hover surface so tags keep their
        // shape on a hovered card. Must be themed: it's lighter than the
        // surface in the dark palettes, darker than it in the light one.
        ["ChipBrush"] = chip,
        ["MatchaBrush"] = matcha,
        ["MatchaDeepBrush"] = matchaDeep,
        ["SakuraBrush"] = sakura,
        ["AmberBrush"] = amber,
        ["BlueBrush"] = blue,
        ["TextBrush"] = text,
        ["MutedTextBrush"] = muted,
        ["AccentTextBrush"] = accentText,
    };

    public static readonly IReadOnlyList<AppTheme> All = new[]
    {
        new AppTheme("dark", "夜", "tokyo night", 0, false, Map(
            "#16161E", "#1F2335", "#2A2E42", "#2A2E42", "#3B4261",
            "#9ECE6A", "#8DBD59", "#F7768E", "#E0AF68", "#7AA2F7",
            "#C0CAF5", "#565F89", "#16161E")),

        new AppTheme("light", "昼", "day", 0, true, Map(
            "#E3E4ED", "#FFFFFF", "#EBECF3", "#CBCDDA", "#D8DAE9",
            "#5F8F34", "#4E7A28", "#D6536C", "#B5832F", "#3E68C9",
            "#272A3F", "#7E84A3", "#FFFFFF")),

        new AppTheme("matcha", "抹茶", "matcha", 300, false, Map(
            "#121A14", "#1A271C", "#26382A", "#26382A", "#334A38",
            "#9ECE6A", "#8DBD59", "#F7768E", "#E0AF68", "#7AA2F7",
            "#D4E8C8", "#6A7E63", "#121A14")),

        new AppTheme("sakura", "桜", "sakura", 400, false, Map(
            "#1B1620", "#271E2E", "#352843", "#352840", "#47355A",
            "#F58FB0", "#E06A95", "#FF6E7F", "#E8B07A", "#9E8CF0",
            "#F2D9E6", "#8C7790", "#1B1620")),

        new AppTheme("sunset", "夕焼け", "sunset", 500, false, Map(
            "#1E1714", "#2B201A", "#3A2C22", "#3A2C22", "#4C3A2C",
            "#FF9E64", "#F08A4B", "#F7768E", "#E0AF68", "#7AA2F7",
            "#F2E0D0", "#8A7765", "#1E1714")),

        new AppTheme("sumi", "墨", "sumi", 250, false, Map(
            "#161618", "#202023", "#2C2C30", "#2C2C30", "#3C3C42",
            "#C8C8CE", "#B0B0B6", "#D88A8A", "#C9B98A", "#8AA0C0",
            "#D8D8DE", "#6A6A72", "#161618")),
    };

    public static AppTheme Find(string? id) =>
        All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public static void Apply(string? id)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var theme = Find(id);
        app.RequestedThemeVariant = theme.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;

        foreach (var (key, hex) in theme.Brushes)
            app.Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }
}

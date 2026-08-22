using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibVLCSharp.Avalonia;
using Material.Icons;
using Material.Icons.Avalonia;
using Tomoshibi.Services;
using Tomoshibi;
using Xunit;

namespace Tomoshibi.Tests;

public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>Guards the theme against the failure that doesn't announce itself.
///
/// <para><c>Controls.axaml</c> reaches into Fluent's own control templates —
/// <c>TextBox /template/ Border#PART_BorderElement</c> and about fifty more. A
/// selector that no longer matches anything is not an error and not a warning:
/// the build stays green, the tests stay green, and the control just quietly
/// renders in Fluent's default blue. The only way to catch it is to build the
/// real template and look at the part that was supposed to be restyled.</para>
///
/// <para>These also cover the second half of that problem — a package whose
/// compiled XAML was built against the previous Avalonia major. That one
/// doesn't even fail at startup; it throws the first time a template is built,
/// which in a running app means on navigation. Loading the real style stack
/// here is what makes it show up at test time instead.</para></summary>
public class ThemeTemplateTests
{
    // One Avalonia per test run. Controls can only be touched on its UI
    // thread, which is what Run marshals onto.
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));

    private static void Run(Action body) =>
        Session.Dispatch(body, default).GetAwaiter().GetResult();

    /// <summary>Put the control in a window and run a layout pass, which is
    /// what actually applies the template and resolves the styles.</summary>
    private static T Shown<T>(T control) where T : Control
    {
        Host(control);
        return control;
    }

    /// <summary>Same, but hands back the window — a popup's contents hang off
    /// the top level rather than off the control that owns it.</summary>
    private static Window Host(Control control)
    {
        var window = new Window { Width = 500, Height = 400, Content = control };
        window.Show();

        // Show() alone queues the layout; templates aren't built until it runs.
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(500, 400));
        window.Arrange(new Rect(0, 0, 500, 400));

        return window;
    }

    /// <summary>The named element inside a built template — the thing a
    /// <c>/template/</c> selector targets.</summary>
    private static TPart Part<TPart>(Visual root, string name) where TPart : StyledElement =>
        root.GetVisualDescendants()
            .OfType<TPart>()
            .First(x => x.Name == name);

    /// <summary>Resolve a palette brush the same way the styles do, so these
    /// assertions follow the palette instead of pinning hex codes.</summary>
    private static IBrush Palette(string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value),
                    $"palette key '{key}' is gone");
        return Assert.IsAssignableFrom<IBrush>(value);
    }

    [Fact]
    public void A_rejected_field_still_gets_its_sakura_outline() => Run(() =>
    {
        var box = Shown(new TextBox { Classes = { "invalid" } });

        var border = Part<Border>(box, "PART_BorderElement");

        // 1.5 is ours; Fluent's own default is a plain 1.
        Assert.Equal(new Thickness(1.5), border.BorderThickness);
        Assert.Equal(Palette("SakuraBrush"), border.BorderBrush);
    });

    [Fact]
    public void A_ticked_checkbox_is_matcha_rather_than_fluent_blue() => Run(() =>
    {
        var check = Shown(new CheckBox { IsChecked = true });

        var box = Part<Border>(check, "NormalRectangle");

        Assert.Equal(Palette("MatchaBrush"), box.Background);
        Assert.Equal(Palette("MatchaBrush"), box.BorderBrush);
    });

    [Fact]
    public void The_tick_itself_is_ink_so_it_reads_against_the_matcha() => Run(() =>
    {
        var check = Shown(new CheckBox { IsChecked = true });

        var glyph = Part<Path>(check, "CheckGlyph");

        Assert.Equal(Palette("InkBrush"), glyph.Fill);
    });

    [Fact]
    public void The_number_fields_inner_textbox_keeps_its_chrome_stripped() => Run(() =>
    {
        var spinner = Shown(new NumericUpDown { Value = 120 });

        var inner = Part<TextBox>(spinner, "PART_TextBox");

        // Left alone, this draws a second border on top of NumericUpDown's own.
        // Only the properties Fluent's template doesn't set on the element
        // itself are checkable: its Padding and Foreground are TemplateBindings,
        // which outrank a style setter, so the matching setters next to these
        // two in Controls.axaml have never applied on any Avalonia version.
        Assert.Equal(new Thickness(0), inner.BorderThickness);
        Assert.Equal(0, inner.MinHeight);
    });

    [Fact]
    public void The_dropdowns_own_surface_is_still_ours() => Run(() =>
    {
        var combo = Shown(new ComboBox { ItemsSource = new[] { "us 4.0", "uk" } });

        // The closed-state surface the :pointerover and :focus styles target.
        // Fluent calls it "Background" with no PART_ prefix, so it carries no
        // compatibility promise across a theme revision — hence checking it.
        var surface = Part<Border>(combo, "Background");

        Assert.NotNull(surface);
    });

    [Fact]
    public void The_open_dropdowns_panel_is_ours_too() => Run(() =>
    {
        var combo = new ComboBox { ItemsSource = new[] { "us 4.0", "uk" } };
        var window = Host(combo);

        // The app installs its palette at startup; nothing else here does.
        ThemeService.Apply("dark");

        // PopupBorder only exists once the dropdown is up, and it hangs off the
        // top level rather than the ComboBox — which is why this one went
        // unasserted when the theme tests first landed.
        combo.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();

        var popup = Part<Border>(window, "PopupBorder");

        // Fluent sets these on the element, so they can only be reached by
        // redefining the keys it looks up. Before that, this panel rendered in
        // Fluent's own grey on every theme the app ships.
        Assert.Equal(Palette("SurfaceBrush").ToString(), popup.Background?.ToString());
        Assert.Equal(Palette("BorderBrush").ToString(), popup.BorderBrush?.ToString());
    });

    [Fact]
    public void The_date_picker_still_hands_its_inner_field_our_font() => Run(() =>
    {
        var picker = Shown(new CalendarDatePicker());

        var inner = Part<TextBox>(picker, "PART_TextBox");

        Assert.Equal(Palette("TextBrush"), inner.Foreground);
    });

    [Fact]
    public void The_icon_packs_own_template_still_builds_against_this_avalonia() => Run(() =>
    {
        // The regression that cost an afternoon. Material.Icons.Avalonia ships
        // compiled XAML, so a copy built against the previous Avalonia major
        // restores, builds and starts without a murmur, then throws
        // MissingMethodException on TemplateBinding.ProvideValue the first time
        // one of its templates is built. In the app that's on navigation, not
        // on launch, so "it opened fine" proves nothing. Building the icon's
        // own template is what makes the mismatch fail here instead.
        var icon = Shown(new MaterialIcon { Kind = MaterialIconKind.Check, Width = 24, Height = 24 });

        Assert.NotNull(icon);
    });

    [Fact]
    public void The_vlc_video_control_still_loads_against_this_avalonia() => Run(() =>
    {
        // LibVLCSharp.Avalonia has no build for Avalonia 12 — 3.10.1 still asks
        // for 11.3.13. It carries no compiled XAML, so it can't fail the way
        // Material.Icons did, but it does subclass NativeControlHost: if that
        // base changed shape, this throws TypeLoadException on construction.
        var view = new VideoView();

        Assert.NotNull(view);
    });
}

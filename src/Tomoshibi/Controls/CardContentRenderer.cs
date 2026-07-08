using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LibVLCSharp.Avalonia;
using Tomoshibi.Services;

namespace Tomoshibi.Controls;

/// <summary>
/// Renders a note field — parsed by <see cref="ContentTokenizer"/> — into a
/// stacked visual tree: wrapped text with bold/italic runs, inline images, and
/// (from Phase 4) audio/video. Cloze deletions display according to the card's
/// <see cref="ClozeOrd"/> and which <see cref="Side"/> is showing. The same
/// control backs the review session, the editor preview and the browser.
/// </summary>
public class CardContentRenderer : Decorator
{
    /// <summary>Media resolver, set once at startup — one store per app.</summary>
    public static MediaStore? Media { get; set; }

    /// <summary>Playback backend for card audio/video, set once at startup.</summary>
    public static IMediaService? Player { get; set; }

    private static readonly Dictionary<string, Bitmap> BitmapCache = new();

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<CardContentRenderer, string?>(nameof(Source));

    public static readonly StyledProperty<int> ClozeOrdProperty =
        AvaloniaProperty.Register<CardContentRenderer, int>(nameof(ClozeOrd));

    public static readonly StyledProperty<CardSide> SideProperty =
        AvaloniaProperty.Register<CardContentRenderer, CardSide>(nameof(Side));

    public static readonly StyledProperty<double> ContentFontSizeProperty =
        AvaloniaProperty.Register<CardContentRenderer, double>(nameof(ContentFontSize), 16);

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int ClozeOrd
    {
        get => GetValue(ClozeOrdProperty);
        set => SetValue(ClozeOrdProperty, value);
    }

    public CardSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public double ContentFontSize
    {
        get => GetValue(ContentFontSizeProperty);
        set => SetValue(ContentFontSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == ClozeOrdProperty ||
            change.Property == SideProperty || change.Property == ContentFontSizeProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        var root = new StackPanel { Spacing = 8 };
        var text = TryFindBrush("TextBrush", Colors.White);
        var accent = TryFindBrush("MatchaBrush", Colors.YellowGreen);
        var muted = TryFindBrush("MutedTextBrush", Colors.Gray);

        foreach (var block in ContentTokenizer.Parse(Source))
        {
            TextBlock? line = null;

            void CloseLine()
            {
                if (line is { Inlines.Count: > 0 })
                    root.Children.Add(line);
                line = null;
            }

            TextBlock OpenLine() => line ??= new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = ContentFontSize,
                Foreground = text
            };

            foreach (var seg in block.Segments)
            {
                switch (seg)
                {
                    case TextSegment t:
                        OpenLine().Inlines!.Add(new Run(t.Text)
                        {
                            FontWeight = t.Bold ? FontWeight.SemiBold : FontWeight.Normal,
                            FontStyle = t.Italic ? FontStyle.Italic : FontStyle.Normal
                        });
                        break;

                    case ClozeSegment c:
                        OpenLine().Inlines!.Add(RenderCloze(c, accent, text));
                        break;

                    case MediaSegment { Kind: MediaKind.Image } m:
                        CloseLine();
                        root.Children.Add(BuildImage(m.Name, text));
                        break;

                    case MediaSegment { Kind: MediaKind.Audio } m:
                        CloseLine();
                        root.Children.Add(BuildAudio(m, text, muted));
                        break;

                    case MediaSegment m: // video
                        CloseLine();
                        root.Children.Add(BuildVideo(m, text, muted));
                        break;
                }
            }

            CloseLine();
        }

        Child = root;
    }

    private Run RenderCloze(ClozeSegment c, IBrush accent, IBrush text)
    {
        var isTarget = c.Ord == ClozeOrd;

        if (Side == CardSide.Front && isTarget)
        {
            var shown = string.IsNullOrEmpty(c.Hint) ? "[…]" : $"[{c.Hint}]";
            return new Run(shown) { Foreground = accent, FontWeight = FontWeight.SemiBold };
        }

        // Revealed everywhere else; the just-tested deletion is highlighted on
        // the back so the eye lands on the answer.
        var highlight = Side == CardSide.Back && isTarget;
        return new Run(c.Answer)
        {
            Foreground = highlight ? accent : text,
            FontWeight = highlight ? FontWeight.SemiBold : FontWeight.Normal
        };
    }

    private Control BuildImage(string name, IBrush fallbackText)
    {
        var path = Media?.Resolve(name);
        if (path is null)
            return new TextBlock
            {
                Text = $"[img:{name}]",
                Foreground = fallbackText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = ContentFontSize
            };

        try
        {
            if (!BitmapCache.TryGetValue(path, out var bmp))
            {
                bmp = new Bitmap(path);
                BitmapCache[path] = bmp;
            }
            // Uniform + fill-width so a wide image scales down to the card
            // instead of overflowing; MaxHeight keeps a tall one in check. Never
            // upscale past the bitmap's own size (small images stay crisp).
            return new Image
            {
                Source = bmp,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                MaxHeight = 300,
                MaxWidth = bmp.PixelSize.Width,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }
        catch
        {
            return new TextBlock { Text = $"[img:{name}]", Foreground = fallbackText };
        }
    }

    private Control BuildAudio(MediaSegment m, IBrush text, IBrush muted)
    {
        var path = Media?.Resolve(m.Name);
        if (path is null)
            return Literal($"[sound:{m.Name}]", text);
        if (Player is not { IsSupported: true })
            return InertChip("🔊 audio · playback unavailable", muted);

        var button = new Button
        {
            Classes = { "seg" },
            Content = "🔊 play sound",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) => Player.PlayAudio(path);
        return button;
    }

    private Control BuildVideo(MediaSegment m, IBrush text, IBrush muted)
    {
        var path = Media?.Resolve(m.Name);
        if (path is null)
            return Literal($"[video:{m.Name}]", text);
        if (Player is not { IsSupported: true })
            return InertChip("🎬 video · playback unavailable", muted);

        var player = Player.CreatePlayer();
        if (player is null)
            return InertChip("🎬 video · playback unavailable", muted);

        var view = new VideoView { Height = 240, HorizontalAlignment = HorizontalAlignment.Stretch };
        var play = new Button
        {
            Classes = { "accent" },
            Content = "▶ play video",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Attach the player only once the native surface is in the tree, and
        // tear it down on the way out (never from a libvlc callback thread).
        view.AttachedToVisualTree += (_, _) => { try { view.MediaPlayer = player; } catch { } };
        view.DetachedFromVisualTree += (_, _) =>
        {
            try { player.Stop(); } catch { }
            try { player.Dispose(); } catch { }
        };

        play.Click += (_, _) =>
        {
            var media = Player.CreateMedia(path);
            if (media is null) return;
            try { player.Play(media); play.IsVisible = false; } catch { }
        };

        var grid = new Grid { Height = 240 };
        grid.Children.Add(view);
        grid.Children.Add(play);
        return grid;
    }

    private TextBlock Literal(string textValue, IBrush brush) => new()
    {
        Text = textValue, Foreground = brush,
        TextWrapping = TextWrapping.Wrap, FontSize = ContentFontSize
    };

    private Control InertChip(string label, IBrush muted) => new Border
    {
        Classes = { "tag" },
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = new TextBlock { Text = label, Foreground = muted, FontSize = ContentFontSize - 3 }
    };

    private IBrush TryFindBrush(string key, Color fallback)
        => this.TryFindResource(key, out var res) && res is IBrush b ? b : new SolidColorBrush(fallback);
}

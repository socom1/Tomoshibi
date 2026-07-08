using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.Controls;

/// <summary>
/// Renders an image-occlusion card: the note's image with its masks drawn over
/// it, according to <see cref="OcclusionLayout"/> for the card's ord and face.
/// The image + masks live at the bitmap's pixel size inside a Viewbox, so masks
/// stay pinned to the image at any display scale — no letterbox maths needed.
/// </summary>
public class OcclusionView : Decorator
{
    private static readonly Dictionary<string, Bitmap> BitmapCache = new();

    public static readonly StyledProperty<Note?> NoteProperty =
        AvaloniaProperty.Register<OcclusionView, Note?>(nameof(Note));

    public static readonly StyledProperty<int> OrdProperty =
        AvaloniaProperty.Register<OcclusionView, int>(nameof(Ord));

    public static readonly StyledProperty<CardSide> SideProperty =
        AvaloniaProperty.Register<OcclusionView, CardSide>(nameof(Side));

    public Note? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }
    public int Ord { get => GetValue(OrdProperty); set => SetValue(OrdProperty, value); }
    public CardSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NoteProperty || change.Property == OrdProperty || change.Property == SideProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        var note = Note;
        if (note is null || note.Fields.Count == 0)
        {
            Child = null;
            return;
        }

        var bmp = LoadBitmap(note.Fields[0]);
        if (bmp is null)
        {
            Child = new TextBlock { Text = "image missing", Foreground = Brush("MutedTextBrush", Colors.Gray) };
            return;
        }

        var w = bmp.PixelSize.Width;
        var h = bmp.PixelSize.Height;

        var canvas = new Canvas { Width = w, Height = h };
        canvas.Children.Add(new Image { Source = bmp, Width = w, Height = h });

        var maskFill = Brush("SurfaceHoverBrush", Color.FromRgb(0x2A, 0x2E, 0x42));
        var accent = Brush("MatchaBrush", Colors.YellowGreen);
        var border = Brush("BorderBrush", Colors.DimGray);

        var states = OcclusionLayout.Compute(note, Ord, Side);
        for (var i = 0; i < states.Length && i < note.Occlusions.Count; i++)
        {
            var state = states[i];
            if (!state.Masked && !state.Highlight)
                continue; // revealed, unremarkable — show the image

            var r = note.Occlusions[i];
            var rect = new Rectangle
            {
                Width = Math.Max(1, r.W * w),
                Height = Math.Max(1, r.H * h),
                Fill = state.Masked ? maskFill : Brushes.Transparent,
                Stroke = state.Highlight ? accent : border,
                StrokeThickness = state.Highlight ? 3 : 1
            };
            Canvas.SetLeft(rect, r.X * w);
            Canvas.SetTop(rect, r.Y * h);
            canvas.Children.Add(rect);
        }

        Child = new Viewbox
        {
            Child = canvas,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 380,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    internal static Bitmap? LoadBitmap(string token)
    {
        var path = CardContentRenderer.Media?.Resolve(token);
        if (path is null) return null;
        try
        {
            if (!BitmapCache.TryGetValue(path, out var bmp))
            {
                bmp = new Bitmap(path);
                BitmapCache[path] = bmp;
            }
            return bmp;
        }
        catch { return null; }
    }

    private IBrush Brush(string key, Color fallback)
        => this.TryFindResource(key, out var res) && res is IBrush b ? b : new SolidColorBrush(fallback);
}

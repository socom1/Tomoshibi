using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tomoshibi.Controls;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

/// <summary>
/// The interactive occlusion editor: shows the note's image at pixel size in a
/// Viewbox and lets you drag to add mask boxes or click one to remove it. All
/// coordinates are normalised (0–1) before they reach the view model, so masks
/// are display-size independent.
/// </summary>
public partial class OcclusionEditorView : UserControl
{
    private OcclusionEditorViewModel? _vm;
    private Canvas? _canvas;
    private double _pxW, _pxH;
    private Point _start;
    private Rectangle? _preview;
    private bool _dragging;

    public OcclusionEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind();
    }

    private void Bind()
    {
        if (_vm is not null) _vm.VisualsChanged -= Redraw;
        _vm = DataContext as OcclusionEditorViewModel;
        if (_vm is not null) _vm.VisualsChanged += Redraw;
        Redraw();
    }

    private async void OnChooseImage(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "choose an image to occlude",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            _vm.SetImage(path);
    }

    private void Redraw()
    {
        var host = this.FindControl<Border>("CanvasHost");
        if (host is null || _vm is null) return;

        if (!_vm.HasImage)
        {
            host.Child = Caption("no image chosen yet");
            return;
        }

        var bmp = OcclusionView.LoadBitmap(_vm.ImageToken);
        if (bmp is null)
        {
            host.Child = Caption("image missing");
            return;
        }

        _pxW = bmp.PixelSize.Width;
        _pxH = bmp.PixelSize.Height;

        var canvas = new Canvas { Width = _pxW, Height = _pxH, Background = Brushes.Transparent };
        canvas.Children.Add(new Image { Source = bmp, Width = _pxW, Height = _pxH });

        var accent = Brush("MatchaBrush", Colors.YellowGreen);
        var fill = new SolidColorBrush(Color.FromArgb(0x66, 0x9E, 0xCE, 0x6A));
        for (var i = 0; i < _vm.Model.Occlusions.Count; i++)
        {
            var r = _vm.Model.Occlusions[i];
            var box = new Rectangle
            {
                Width = Math.Max(1, r.W * _pxW),
                Height = Math.Max(1, r.H * _pxH),
                Fill = fill,
                Stroke = accent,
                StrokeThickness = 2
            };
            Canvas.SetLeft(box, r.X * _pxW);
            Canvas.SetTop(box, r.Y * _pxH);
            canvas.Children.Add(box);
        }

        canvas.PointerPressed += OnPointerPressed;
        canvas.PointerMoved += OnPointerMoved;
        canvas.PointerReleased += OnPointerReleased;
        _canvas = canvas;

        host.Child = new Viewbox
        {
            Child = canvas,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        var p = e.GetPosition(_canvas);

        // Click inside an existing mask → remove it (topmost first).
        for (var i = _vm.Model.Occlusions.Count - 1; i >= 0; i--)
        {
            var r = _vm.Model.Occlusions[i];
            if (p.X >= r.X * _pxW && p.X <= (r.X + r.W) * _pxW &&
                p.Y >= r.Y * _pxH && p.Y <= (r.Y + r.H) * _pxH)
            {
                _vm.RemoveAt(i);
                return;
            }
        }

        // Otherwise start drawing a new mask.
        _start = p;
        _dragging = true;
        _preview = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x55, 0x9E, 0xCE, 0x6A)),
            Stroke = Brush("MatchaBrush", Colors.YellowGreen),
            StrokeThickness = 2
        };
        Canvas.SetLeft(_preview, p.X);
        Canvas.SetTop(_preview, p.Y);
        _canvas.Children.Add(_preview);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _preview is null || _canvas is null) return;
        var p = e.GetPosition(_canvas);
        var x = Math.Min(_start.X, p.X);
        var y = Math.Min(_start.Y, p.Y);
        Canvas.SetLeft(_preview, x);
        Canvas.SetTop(_preview, y);
        _preview.Width = Math.Abs(p.X - _start.X);
        _preview.Height = Math.Abs(p.Y - _start.Y);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || _vm is null || _canvas is null) return;
        _dragging = false;

        var p = e.GetPosition(_canvas);
        var x = Math.Min(_start.X, p.X) / _pxW;
        var y = Math.Min(_start.Y, p.Y) / _pxH;
        var w = Math.Abs(p.X - _start.X) / _pxW;
        var h = Math.Abs(p.Y - _start.Y) / _pxH;

        _preview = null; // the coming redraw rebuilds the canvas
        _vm.AddRect(x, y, w, h);
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Margin = new Thickness(10),
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private IBrush Brush(string key, Color fallback)
        => this.TryFindResource(key, out var res) && res is IBrush b ? b : new SolidColorBrush(fallback);
}

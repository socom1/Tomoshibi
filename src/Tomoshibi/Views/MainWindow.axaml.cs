using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Hold the splash a beat after the window appears, then let it
    /// fade out (the opacity transition does the reveal).</summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_vm is not null)
                _vm.IsBooting = false;
        };
        timer.Start();
    }

    /// <summary>
    /// Space toggles the timer from anywhere — except while typing in a text
    /// field or while the add-task modal is up, where space means space.
    /// Controls that consume space themselves (buttons, checkboxes) mark the
    /// event handled before it bubbles here, so they're unaffected.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _vm is null)
            return;

        var cmd = e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
                  e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // Cmd/Ctrl + K opens the command palette.
        if (cmd && e.Key == Key.K)
        {
            _vm.OpenCommandPalette();
            e.Handled = true;
            return;
        }

        // Cmd/Ctrl + 1…8 jumps between destinations (Meta is Cmd on macOS).
        if (cmd && e.Key is >= Key.D1 and <= Key.D8)
        {
            _vm.NavigateByIndex(e.Key - Key.D1 + 1);
            e.Handled = true;
            return;
        }

        // Space toggles the timer from anywhere — except while typing in a
        // text field or while the add-task modal is up.
        if (e.Key != Key.Space)
            return;

        if (_vm.Today.Tasks.IsAddTaskModalOpen)
            return;

        if (FocusManager?.GetFocusedElement() is TextBox)
            return;

        _vm.Today.Pomodoro.ToggleRunCommand.Execute(null);
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as MainWindowViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            ApplyZenState();
            ApplyWindowPlacement();
        }
    }

    /// <summary>Open where the last session ended. Width 0 = first run, keep
    /// the XAML defaults and the centered startup location.</summary>
    private void ApplyWindowPlacement()
    {
        if (_vm is null) return;

        var (width, height, x, y) = _vm.WindowPlacement;
        if (width <= 0 || height <= 0)
            return;

        Width = width;
        Height = height;

        if (x is { } px && y is { } py)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(px, py);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Don't capture the fullscreen/zen dimensions as the new normal.
        if (_vm is not null && WindowState == WindowState.Normal)
            _vm.SaveWindowPlacement(Width, Height, Position.X, Position.Y);

        // The close button hides the window instead of quitting (when the
        // close-to-tray setting is on) — the timer keeps running and the
        // tray icon is the way back. With it off, closing really quits.
        // Real shutdowns (tray quit, Cmd+Q) carry a different close reason
        // and pass straight through either way.
        if (e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            if (_vm?.CloseToTray ?? true)
            {
                Hide();
            }
            else if (Avalonia.Application.Current?.ApplicationLifetime
                     is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        base.OnClosing(e);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsZenMode))
            ApplyZenState();
        else if (e.PropertyName == nameof(MainWindowViewModel.IsCommandPaletteOpen)
                 && _vm?.IsCommandPaletteOpen == true)
        {
            // Drop the cursor straight into the palette's search box.
            this.FindControl<TextBox>("PaletteBox")?.Focus();
        }
    }

    /// <summary>Arrow keys move the palette selection; Enter runs it.</summary>
    private void OnPaletteKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null)
            return;

        switch (e.Key)
        {
            case Key.Down: _vm.CommandPalette.Move(1); e.Handled = true; break;
            case Key.Up: _vm.CommandPalette.Move(-1); e.Handled = true; break;
            case Key.Enter: _vm.CommandPalette.RunSelected(); e.Handled = true; break;
        }
    }

    private void ApplyZenState()
    {
        if (_vm is null) return;
        WindowState = _vm.IsZenMode ? WindowState.FullScreen : WindowState.Normal;
    }

    /// <summary>Pick the music folder for the floating player.</summary>
    private async void OnChooseMusicFolder(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "choose your music folder",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            _vm.Music.SetFolder(path);
    }
}

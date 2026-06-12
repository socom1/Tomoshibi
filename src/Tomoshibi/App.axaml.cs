using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Tomoshibi.Services;
using Tomoshibi.ViewModels;
using Tomoshibi.Views;

namespace Tomoshibi;

public partial class App : Application
{
    private TrayIcon? _tray;
    private NativeMenuItem? _trayToggle;
    private MainWindowViewModel? _vm;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IStorageService storage = new JsonStorageService();
            _vm = new MainWindowViewModel(storage);

            desktop.MainWindow = new MainWindow { DataContext = _vm };

            // Closing the window hides it; the timer keeps running and the
            // tray is the way back. Quitting happens via the tray menu (or
            // Cmd+Q, which goes through Shutdown and is allowed past the
            // hide-on-close guard in MainWindow).
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _vm?.Music.Shutdown();

            SetupTray(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_vm is null)
            return;

        _trayToggle = new NativeMenuItem("start");
        _trayToggle.Click += (_, _) => _vm.Today.Pomodoro.ToggleRunCommand.Execute(null);

        var skip = new NativeMenuItem("skip");
        skip.Click += (_, _) => _vm.Today.Pomodoro.SkipCommand.Execute(null);

        var reset = new NativeMenuItem("reset");
        reset.Click += (_, _) => _vm.Today.Pomodoro.ResetCommand.Execute(null);

        var show = new NativeMenuItem("show tomoshibi");
        show.Click += (_, _) => ShowMainWindow(desktop);

        var quit = new NativeMenuItem("quit");
        quit.Click += (_, _) => desktop.Shutdown();

        var menu = new NativeMenu();
        menu.Items.Add(_trayToggle);
        menu.Items.Add(skip);
        menu.Items.Add(reset);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(show);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Tomoshibi/Assets/tray.png"))),
            ToolTipText = "灯火 · tomoshibi",
            Menu = menu
        };

        // Double-purpose: clicking the icon itself brings the window back.
        _tray.Clicked += (_, _) => ShowMainWindow(desktop);

        TrayIcon.SetIcons(this, new TrayIcons { _tray });

        // Keep the tooltip and the start/pause entry in step with the timer.
        _vm.Today.Pomodoro.PropertyChanged += OnPomodoroChanged;
        UpdateTray();
    }

    private void OnPomodoroChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PomodoroViewModel.TimeDisplay)
            or nameof(PomodoroViewModel.IsRunning))
        {
            UpdateTray();
        }
    }

    private void UpdateTray()
    {
        if (_tray is null || _trayToggle is null || _vm is null)
            return;

        var p = _vm.Today.Pomodoro;
        _tray.ToolTipText = p.IsRunning
            ? $"{p.TimeDisplay} · {p.PhaseShortLabel} — tomoshibi"
            : "灯火 · tomoshibi";
        _trayToggle.Header = p.StartPauseLabel;
    }

    private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is not { } window)
            return;

        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The 設定 · settings destination — one page gathering what was scattered:
/// the timer numbers and alert toggles (shared instance with the today gear,
/// so they stay in sync), startup behaviour, the grading scale, the music
/// player's knobs, and where the data lives on disk.
/// </summary>
public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    /// <summary>The same instance the today-page gear flyout binds — edits
    /// in either place are one set of values.</summary>
    public SettingsViewModel Timer { get; }

    /// <summary>The floating player's state, for folder/shuffle/volume.</summary>
    public MusicPlayerViewModel Music { get; }

    /// <summary>The subjects page, for the grading-scale picker.</summary>
    public SubjectsViewModel Subjects { get; }

    /// <summary>Where the JSON state file lives.</summary>
    public string DataLocation { get; }

    [ObservableProperty]
    private bool _showWelcome;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _lightTheme;

    public string VersionLabel => "灯火 · tomoshibi — v1.5";

    public SettingsPageViewModel(AppState state, Action save,
                                 SettingsViewModel timer,
                                 MusicPlayerViewModel music,
                                 SubjectsViewModel subjects,
                                 string dataLocation)
    {
        _state = state;
        _save = save;
        Timer = timer;
        Music = music;
        Subjects = subjects;
        DataLocation = dataLocation;

        _showWelcome = state.ShowWelcome;
        _closeToTray = state.CloseToTray;
        _lightTheme = state.LightTheme;
    }

    partial void OnShowWelcomeChanged(bool value)
    {
        _state.ShowWelcome = value;
        _save();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _state.CloseToTray = value;
        _save();
    }

    partial void OnLightThemeChanged(bool value)
    {
        _state.LightTheme = value;
        Services.ThemeService.Apply(value);
        _save();
    }
}

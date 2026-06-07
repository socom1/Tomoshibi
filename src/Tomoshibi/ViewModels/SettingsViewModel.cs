using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Editable timer lengths. Bound to NumericUpDowns (hence decimal), it writes
/// straight through to the shared <see cref="PomodoroSettings"/> and notifies
/// the rest of the app so changes save and the timer can refresh.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly PomodoroSettings _settings;
    private readonly Action _onChanged;

    [ObservableProperty]
    private decimal _focusMinutes;

    [ObservableProperty]
    private decimal _shortBreakMinutes;

    [ObservableProperty]
    private decimal _longBreakMinutes;

    [ObservableProperty]
    private decimal _roundsBeforeLongBreak;

    public SettingsViewModel(PomodoroSettings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        _focusMinutes = settings.FocusMinutes;
        _shortBreakMinutes = settings.ShortBreakMinutes;
        _longBreakMinutes = settings.LongBreakMinutes;
        _roundsBeforeLongBreak = settings.RoundsBeforeLongBreak;
    }

    partial void OnFocusMinutesChanged(decimal value)
    {
        _settings.FocusMinutes = (int)value;
        _onChanged();
    }

    partial void OnShortBreakMinutesChanged(decimal value)
    {
        _settings.ShortBreakMinutes = (int)value;
        _onChanged();
    }

    partial void OnLongBreakMinutesChanged(decimal value)
    {
        _settings.LongBreakMinutes = (int)value;
        _onChanged();
    }

    partial void OnRoundsBeforeLongBreakChanged(decimal value)
    {
        _settings.RoundsBeforeLongBreak = (int)value;
        _onChanged();
    }
}

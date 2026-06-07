using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The Pomodoro timer. A one-second tick drives a small state machine:
/// focus -> short break, with a long break after every Nth focus round,
/// then back to a fresh set.
/// </summary>
public partial class PomodoroViewModel : ViewModelBase
{
    private readonly PomodoroSettings _settings;
    private readonly DispatcherTimer _timer;

    private int _remainingSeconds;
    private int _round = 1; // current focus round within the set (1..RoundsBeforeLongBreak)

    /// <summary>Raised when a focus block finishes; carries the focused minutes.
    /// Not raised for breaks or when the user skips.</summary>
    public event Action<int>? FocusSessionCompleted;

    [ObservableProperty]
    private PomodoroPhase _phase = PomodoroPhase.Focus;

    [ObservableProperty]
    private string _timeDisplay = "25:00";

    [ObservableProperty]
    private string _phaseLabel = "集中 · focus";

    [ObservableProperty]
    private string _roundLabel = "round 1 of 4";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartPauseLabel))]
    private bool _isRunning;

    public string StartPauseLabel => IsRunning ? "pause" : "start";

    public PomodoroViewModel(PomodoroSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;

        SetPhase(PomodoroPhase.Focus, resetRound: true);
    }

    /// <summary>Parameterless ctor for the XAML designer preview only.</summary>
    public PomodoroViewModel() : this(new PomodoroSettings())
    {
    }

    [RelayCommand]
    private void ToggleRun()
    {
        if (IsRunning)
        {
            _timer.Stop();
            IsRunning = false;
        }
        else
        {
            _timer.Start();
            IsRunning = true;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _timer.Stop();
        IsRunning = false;
        _remainingSeconds = PhaseLengthSeconds(Phase);
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Skip()
    {
        // Move on without counting the current block.
        Advance(focusCounts: false);
    }

    /// <summary>
    /// Called when timer settings change. If the timer is idle, refresh the
    /// current phase so the new length shows right away; if it's running, the
    /// next phase will pick the new values up.
    /// </summary>
    public void ApplySettings()
    {
        if (!IsRunning)
            SetPhase(Phase, resetRound: false);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateTimeDisplay();
        }

        if (_remainingSeconds <= 0)
            Advance(focusCounts: true);
    }

    private void Advance(bool focusCounts)
    {
        _timer.Stop();
        IsRunning = false;

        switch (Phase)
        {
            case PomodoroPhase.Focus:
                if (focusCounts)
                    FocusSessionCompleted?.Invoke(_settings.FocusMinutes);

                var longDue = _round >= _settings.RoundsBeforeLongBreak;
                SetPhase(longDue ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak, resetRound: false);
                break;

            case PomodoroPhase.ShortBreak:
                _round++;
                SetPhase(PomodoroPhase.Focus, resetRound: false);
                break;

            case PomodoroPhase.LongBreak:
                SetPhase(PomodoroPhase.Focus, resetRound: true);
                break;
        }
    }

    private void SetPhase(PomodoroPhase phase, bool resetRound)
    {
        if (resetRound)
            _round = 1;

        Phase = phase;
        _remainingSeconds = PhaseLengthSeconds(phase);

        PhaseLabel = phase switch
        {
            PomodoroPhase.Focus => "集中 · focus",
            PomodoroPhase.ShortBreak => "休憩 · short break",
            PomodoroPhase.LongBreak => "休憩 · long break",
            _ => "集中 · focus"
        };

        RoundLabel = phase switch
        {
            PomodoroPhase.Focus => $"round {_round} of {_settings.RoundsBeforeLongBreak}",
            PomodoroPhase.LongBreak => "long break",
            _ => "short break"
        };

        UpdateTimeDisplay();
    }

    private int PhaseLengthSeconds(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => _settings.FocusMinutes * 60,
        PomodoroPhase.ShortBreak => _settings.ShortBreakMinutes * 60,
        PomodoroPhase.LongBreak => _settings.LongBreakMinutes * 60,
        _ => _settings.FocusMinutes * 60
    };

    private void UpdateTimeDisplay()
    {
        var span = TimeSpan.FromSeconds(_remainingSeconds);
        TimeDisplay = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
    }
}

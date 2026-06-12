using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The floating music player: point it at a folder of your own music and it
/// plays through it (shuffled by default), auto-advancing as tracks end.
/// Lives outside the destinations — the equaliser bubble and its panel
/// float over every page.
/// </summary>
public partial class MusicPlayerViewModel : ViewModelBase
{
    private static readonly string[] AudioExtensions =
        { ".mp3", ".m4a", ".aac", ".wav", ".aiff", ".flac", ".ogg" };

    private readonly AppState _state;
    private readonly Action _save;
    private readonly IMusicService _service;

    private List<string> _queue = new();
    private int _index = -1;

    [ObservableProperty] private bool _isPanelOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    private bool _isPlaying;

    [ObservableProperty] private bool _isPaused;

    [ObservableProperty] private string _trackLabel = "no folder chosen";
    [ObservableProperty] private string _folderLabel = string.Empty;
    [ObservableProperty] private bool _hasFolder;
    [ObservableProperty] private bool _shuffle;
    [ObservableProperty] private decimal _volume;

    public bool IsSupported => _service.IsSupported;
    public string PlayPauseGlyph => IsPlaying && !IsPaused ? "❚❚" : "▶";

    public MusicPlayerViewModel(AppState state, Action save, IMusicService service)
    {
        _state = state;
        _save = save;
        _service = service;

        _shuffle = _state.MusicShuffle;
        _volume = (decimal)Math.Clamp(_state.MusicVolume, 0, 100);

        _service.TrackEnded += () => Dispatcher.UIThread.Post(NextTrack);

        if (!string.IsNullOrEmpty(_state.MusicFolder))
            LoadFolder(_state.MusicFolder, autoplay: false);
    }

    [RelayCommand]
    private void TogglePanel() => IsPanelOpen = !IsPanelOpen;

    /// <summary>Called by the view after the folder picker returns.</summary>
    public void SetFolder(string path)
    {
        _state.MusicFolder = path;
        _save();
        LoadFolder(path, autoplay: true);
    }

    private void LoadFolder(string path, bool autoplay)
    {
        try
        {
            _queue = Directory.EnumerateFiles(path)
                .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            _queue = new List<string>();
        }

        if (Shuffle)
            ShuffleQueue();

        HasFolder = _queue.Count > 0;
        FolderLabel = $"{Path.GetFileName(path)} · {_queue.Count} tracks";
        _index = -1;

        if (!HasFolder)
        {
            TrackLabel = "no audio files in that folder";
            return;
        }

        TrackLabel = "ready";
        if (autoplay)
            NextTrack();
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (!HasFolder)
        {
            IsPanelOpen = true;
            return;
        }

        if (!IsPlaying)
        {
            NextTrack();
        }
        else if (IsPaused)
        {
            _service.Resume();
            IsPaused = false;
        }
        else
        {
            _service.Pause();
            IsPaused = true;
        }

        OnPropertyChanged(nameof(PlayPauseGlyph));
    }

    [RelayCommand]
    private void NextTrack()
    {
        if (_queue.Count == 0)
            return;

        _index = (_index + 1) % _queue.Count;
        StartCurrent();
    }

    [RelayCommand]
    private void PrevTrack()
    {
        if (_queue.Count == 0)
            return;

        _index = (_index - 1 + _queue.Count) % _queue.Count;
        StartCurrent();
    }

    [RelayCommand]
    private void StopPlayback()
    {
        _service.Stop();
        IsPlaying = false;
        IsPaused = false;
        TrackLabel = HasFolder ? "stopped" : TrackLabel;
        OnPropertyChanged(nameof(PlayPauseGlyph));
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        Shuffle = !Shuffle;
        _state.MusicShuffle = Shuffle;
        if (Shuffle)
            ShuffleQueue();
        else
            _queue = _queue.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        _save();
    }

    private void StartCurrent()
    {
        var path = _queue[_index];
        TrackLabel = Path.GetFileNameWithoutExtension(path);
        _service.Play(path, (double)Volume);
        IsPlaying = true;
        IsPaused = false;
        OnPropertyChanged(nameof(PlayPauseGlyph));
    }

    private void ShuffleQueue()
    {
        var rng = new Random();
        _queue = _queue.OrderBy(_ => rng.Next()).ToList();
    }

    partial void OnVolumeChanged(decimal value)
    {
        _state.MusicVolume = (double)value;
        _save();
    }

    /// <summary>Kill the player on app shutdown so no orphan keeps singing.</summary>
    public void Shutdown() => _service.Stop();
}

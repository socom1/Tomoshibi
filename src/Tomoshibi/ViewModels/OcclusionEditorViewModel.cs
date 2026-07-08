using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Edits an image-occlusion note: choose an image, draw mask rectangles over
/// it (one card per mask), and set the reveal mode plus optional header /
/// back-extra text. Rects are stored normalised (0–1) so they survive any
/// display size. The view drives drawing; this holds the model and keeps the
/// generated cards in sync.
/// </summary>
public partial class OcclusionEditorViewModel : ViewModelBase
{
    private readonly Note _note;
    private readonly Action _changed;
    private readonly MediaStore _media;
    private bool _loading;

    /// <summary>Raised when the image or masks change so the canvas redraws.</summary>
    public event Action? VisualsChanged;

    public Note Model => _note;

    [ObservableProperty] private bool _hasImage;
    [ObservableProperty] private int _maskCount;
    [ObservableProperty] private bool _hideAll;
    [ObservableProperty] private string _header = string.Empty;
    [ObservableProperty] private string _backExtra = string.Empty;
    [ObservableProperty] private string _hint = "pick an image, then drag to draw mask boxes";

    public OcclusionEditorViewModel(Note note, Action changed, MediaStore media)
    {
        _note = note;
        _changed = changed;
        _media = media;

        _loading = true;
        while (_note.Fields.Count < 3) _note.Fields.Add(string.Empty);
        _hideAll = _note.HideAll;
        _header = _note.Fields[1];
        _backExtra = _note.Fields[2];
        _loading = false;

        RefreshState();
    }

    public string ImageToken => _note.Fields.Count > 0 ? _note.Fields[0] : string.Empty;

    /// <summary>Import an image and hang the occlusion note off it.</summary>
    public void SetImage(string sourcePath)
    {
        try { _note.Fields[0] = _media.Import(sourcePath); }
        catch { return; }
        _note.Occlusions.Clear(); // masks were relative to the old image
        Commit();
        VisualsChanged?.Invoke();
    }

    /// <summary>Add a mask from a normalised rectangle (view supplies 0–1
    /// coords). Tiny accidental taps are ignored.</summary>
    public void AddRect(double x, double y, double w, double h)
    {
        if (w < 0.01 || h < 0.01) return;
        _note.Occlusions.Add(new OcclusionRect
        {
            X = Math.Clamp(x, 0, 1),
            Y = Math.Clamp(y, 0, 1),
            W = Math.Clamp(w, 0, 1 - x),
            H = Math.Clamp(h, 0, 1 - y)
        });
        Commit();
        VisualsChanged?.Invoke();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _note.Occlusions.Count) return;
        _note.Occlusions.RemoveAt(index);
        Commit();
        VisualsChanged?.Invoke();
    }

    [RelayCommand]
    private void RemoveLast()
    {
        if (_note.Occlusions.Count == 0) return;
        _note.Occlusions.RemoveAt(_note.Occlusions.Count - 1);
        Commit();
        VisualsChanged?.Invoke();
    }

    [RelayCommand]
    private void ClearMasks()
    {
        if (_note.Occlusions.Count == 0) return;
        _note.Occlusions.Clear();
        Commit();
        VisualsChanged?.Invoke();
    }

    partial void OnHideAllChanged(bool value) => Save(() => _note.HideAll = value);
    partial void OnHeaderChanged(string value) => Save(() => _note.Fields[1] = value);
    partial void OnBackExtraChanged(string value) => Save(() => _note.Fields[2] = value);

    private void Save(Action apply)
    {
        if (_loading) return;
        apply();
        _changed();
    }

    /// <summary>Re-sync cards to masks, refresh flags, and save.</summary>
    private void Commit()
    {
        CardGenerator.Sync(_note);
        RefreshState();
        _changed();
    }

    private void RefreshState()
    {
        HasImage = !string.IsNullOrEmpty(ImageToken);
        MaskCount = _note.Occlusions.Count;
        Hint = !HasImage
            ? "pick an image, then drag to draw mask boxes"
            : MaskCount == 0
                ? "drag on the image to draw a mask box · each box becomes one card"
                : $"{MaskCount} mask{(MaskCount == 1 ? "" : "s")} · drag to add more · click a box to remove it";
    }
}

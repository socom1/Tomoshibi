using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// One sticky on the board. Text writes through as you type; the colour
/// dots restyle it in place. A stable pseudo-random tilt (from the id) is
/// what gives the board its corkboard scatter.
/// </summary>
public partial class StickyNoteViewModel : ViewModelBase
{
    private readonly Action _save;

    public StickyNote Model { get; }

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAmber))]
    [NotifyPropertyChangedFor(nameof(IsSakura))]
    [NotifyPropertyChangedFor(nameof(IsMatcha))]
    [NotifyPropertyChangedFor(nameof(IsBlue))]
    private StickyColor _color;

    public bool IsAmber => Color == StickyColor.Amber;
    public bool IsSakura => Color == StickyColor.Sakura;
    public bool IsMatcha => Color == StickyColor.Matcha;
    public bool IsBlue => Color == StickyColor.Blue;

    /// <summary>−2.2°…+2.2°, stable per note.</summary>
    public double Tilt => (Model.Id.GetHashCode() % 23) / 5.0;

    public StickyNoteViewModel(StickyNote model, Action save)
    {
        Model = model;
        _save = save;
        _text = model.Text;
        _color = model.Color;
    }

    [RelayCommand]
    private void SetColor(string? name)
    {
        if (Enum.TryParse<StickyColor>(name, true, out var color))
            Color = color;
    }

    partial void OnTextChanged(string value)
    {
        Model.Text = value;
        _save();
    }

    partial void OnColorChanged(StickyColor value)
    {
        Model.Color = value;
        _save();
    }
}

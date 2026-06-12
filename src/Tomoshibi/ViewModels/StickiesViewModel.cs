using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The 付箋 · stickies destination — a corkboard of quick coloured notes.
/// Deliberately dumber than the todo backlog: no status, no dates, just
/// "don't forget" scraps you type straight onto the board.
/// </summary>
public partial class StickiesViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    public ObservableCollection<StickyNoteViewModel> Items { get; } = new();

    [ObservableProperty] private bool _hasItems;

    public StickiesViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;

        foreach (var note in _state.Stickies.OrderBy(n => n.CreatedAt))
            Items.Add(new StickyNoteViewModel(note, _save));

        HasItems = Items.Count > 0;
    }

    [RelayCommand]
    private void Add()
    {
        // Rotate through the colours so a fresh board isn't monochrome.
        var color = (StickyColor)(_state.Stickies.Count % 4);
        var note = new StickyNote { Color = color };

        _state.Stickies.Add(note);
        Items.Add(new StickyNoteViewModel(note, _save));
        HasItems = true;
        _save();
    }

    [RelayCommand]
    private void Remove(StickyNoteViewModel? sticky)
    {
        if (sticky is null)
            return;

        _state.Stickies.Remove(sticky.Model);
        Items.Remove(sticky);
        HasItems = Items.Count > 0;
        _save();
    }
}

using System;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>One editable flashcard row in the deck editor. Edits write
/// straight onto the model and save, like the other inline editors.</summary>
public class FlashcardViewModel : ViewModelBase
{
    private readonly Action _changed;
    public Flashcard Model { get; }

    public FlashcardViewModel(Flashcard model, Action changed)
    {
        Model = model;
        _changed = changed;
    }

    public string Front
    {
        get => Model.Front;
        set
        {
            if (Model.Front == value) return;
            Model.Front = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public string Back
    {
        get => Model.Back;
        set
        {
            if (Model.Back == value) return;
            Model.Back = value;
            OnPropertyChanged();
            _changed();
        }
    }
}

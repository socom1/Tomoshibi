using System;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>A saved study-video link row: open it in the browser, or remove it.</summary>
public partial class StudyLinkViewModel : ViewModelBase
{
    private readonly Action<StudyLinkViewModel> _open;
    private readonly Action<StudyLinkViewModel> _remove;

    public StudyLink Model { get; }
    public string Title => string.IsNullOrWhiteSpace(Model.Title) ? Model.Url : Model.Title;
    public string Url => Model.Url;

    public StudyLinkViewModel(StudyLink model, Action<StudyLinkViewModel> open, Action<StudyLinkViewModel> remove)
    {
        Model = model;
        _open = open;
        _remove = remove;
    }

    [RelayCommand] private void Open() => _open(this);
    [RelayCommand] private void Remove() => _remove(this);
}

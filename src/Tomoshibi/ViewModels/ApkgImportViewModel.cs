using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Drives an .apkg import: shows the chosen file, a keep-schedule toggle, runs
/// <see cref="ApkgImporter"/>, adds the resulting decks to the collection, and
/// reports what came in (or why it couldn't).
/// </summary>
public partial class ApkgImportViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly MediaStore _media;
    private readonly Action _save;
    private readonly Action _onImported;
    private string? _path;

    [ObservableProperty] private string _fileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _hasFile;

    [ObservableProperty] private bool _keepSchedule = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _isDone;

    [ObservableProperty] private string _status = "choose an .apkg file exported from Anki";

    public ApkgImportViewModel(AppState state, MediaStore media, Action save, Action onImported)
    {
        _state = state;
        _media = media;
        _save = save;
        _onImported = onImported;
    }

    public bool CanImport => HasFile && !IsDone;

    /// <summary>Called by the view once the file picker returns.</summary>
    public void SetFile(string path)
    {
        _path = path;
        FileName = Path.GetFileName(path);
        HasFile = true;
        IsDone = false;
        Status = "ready — import will add its decks to yours";
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void Import()
    {
        if (_path is null) return;

        var result = ApkgImporter.Import(_path, _media, KeepSchedule);
        if (!result.Ok)
        {
            Status = result.Message;
            return;
        }

        foreach (var deck in result.Decks)
            _state.Decks.Add(deck);

        _save();
        _onImported();

        Status = $"imported {result.Notes} notes · {result.Cards} cards · {result.Decks.Count} decks"
               + $" · {result.Media} media"
               + (result.Skipped > 0 ? $" · {result.Skipped} skipped" : string.Empty);
        IsDone = true;
        HasFile = false;
    }
}

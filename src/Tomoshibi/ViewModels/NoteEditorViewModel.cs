using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Edits one note: pick a type, fill its fields, tag it, drop in an image, and
/// watch a live preview of the card it produces. Every edit re-syncs the note's
/// cards (surviving cards keep their schedule) and saves.
/// </summary>
public partial class NoteEditorViewModel : ViewModelBase
{
    private readonly Note _note;
    private readonly Action _changed;
    private readonly MediaStore _media;
    private bool _loading;

    public Note Model => _note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBasic), nameof(IsReversed), nameof(IsCloze))]
    [NotifyPropertyChangedFor(nameof(Field0Label), nameof(Field1Label))]
    private NoteType _type;

    [ObservableProperty] private string _field0 = string.Empty;
    [ObservableProperty] private string _field1 = string.Empty;
    [ObservableProperty] private string _tagsText = string.Empty;

    // Live preview of the note's first card.
    [ObservableProperty] private string _previewFrontSource = string.Empty;
    [ObservableProperty] private string _previewBackSource = string.Empty;
    [ObservableProperty] private int _previewClozeOrd;

    public NoteEditorViewModel(Note note, Action changed, MediaStore media)
    {
        _note = note;
        _changed = changed;
        _media = media;

        _loading = true;
        while (_note.Fields.Count < 2) _note.Fields.Add(string.Empty);
        _type = _note.Type;
        _field0 = _note.Fields[0];
        _field1 = _note.Fields[1];
        _tagsText = string.Join(" ", _note.Tags);
        _loading = false;

        UpdatePreview();
    }

    public bool IsBasic => Type == NoteType.Basic;
    public bool IsReversed => Type == NoteType.BasicReversed;
    public bool IsCloze => Type == NoteType.Cloze;

    public string Field0Label => Type == NoteType.Cloze ? "text — wrap answers in {{c1::…}}" : "front";
    public string Field1Label => Type == NoteType.Cloze ? "back extra (optional)" : "back";

    [RelayCommand]
    private void SelectType(string? kind)
    {
        Type = kind switch
        {
            "reversed" => NoteType.BasicReversed,
            "cloze" => NoteType.Cloze,
            _ => NoteType.Basic
        };
    }

    partial void OnTypeChanged(NoteType value)
    {
        if (_loading) return;
        _note.Type = value;
        SyncAndSave();
    }

    partial void OnField0Changed(string value)
    {
        if (_loading) return;
        _note.Fields[0] = value;
        SyncAndSave();
    }

    partial void OnField1Changed(string value)
    {
        if (_loading) return;
        _note.Fields[1] = value;
        SyncAndSave();
    }

    partial void OnTagsTextChanged(string value)
    {
        if (_loading) return;
        _note.Tags.Clear();
        foreach (var tag in value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            if (!_note.Tags.Contains(tag))
                _note.Tags.Add(tag);
        _changed();
    }

    /// <summary>Import an image and drop its token into a field (0 front,
    /// 1 back). Called by the view after the file picker returns.</summary>
    public void InsertImage(int fieldIndex, string sourcePath)
    {
        string token;
        try { token = _media.Import(sourcePath); }
        catch { return; }

        var snippet = $"[img:{token}]";
        if (fieldIndex == 1)
            Field1 = string.IsNullOrEmpty(Field1) ? snippet : Field1 + " " + snippet;
        else
            Field0 = string.IsNullOrEmpty(Field0) ? snippet : Field0 + " " + snippet;
    }

    private void SyncAndSave()
    {
        CardGenerator.Sync(_note);
        UpdatePreview();
        _changed();
    }

    private void UpdatePreview()
    {
        var ord = CardGenerator.ExpectedOrds(_note).FirstOrDefault();
        PreviewClozeOrd = ord;

        var f0 = _note.Fields.Count > 0 ? _note.Fields[0] : string.Empty;
        var f1 = _note.Fields.Count > 1 ? _note.Fields[1] : string.Empty;

        (PreviewFrontSource, PreviewBackSource) = _note.Type switch
        {
            NoteType.Cloze => (f0, string.IsNullOrWhiteSpace(f1) ? f0 : f0 + "\n" + f1),
            _ => (f0, f1)
        };
    }
}

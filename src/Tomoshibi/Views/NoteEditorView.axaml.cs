using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class NoteEditorView : UserControl
{
    public NoteEditorView()
    {
        InitializeComponent();
    }

    /// <summary>The 🖼 buttons: pick an image file, hand it to the editor to
    /// import + embed. The field to drop it in comes from the button's Tag
    /// ("0" front, "1" back).</summary>
    private async void OnInsertImage(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: { } tag } || DataContext is not NoteEditorViewModel editor)
            return;
        if (!int.TryParse(tag.ToString(), out var fieldIndex))
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "choose an image",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            editor.InsertImage(fieldIndex, path);
    }
}

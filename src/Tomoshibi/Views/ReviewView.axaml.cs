using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class ReviewView : UserControl
{
    public ReviewView()
    {
        InitializeComponent();
    }

    private ReviewViewModel? Vm => DataContext as ReviewViewModel;

    private async void OnImportDeckClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "import a deck (Anki text export)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("text deck") { Patterns = new[] { "*.txt", "*.tsv" } }
            }
        });

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();

        // Same guard as the .ics import: a deck export is small; refuse
        // anything absurd rather than reading an unbounded file into memory.
        const long maxBytes = 5 * 1024 * 1024;
        if (stream.CanSeek && stream.Length > maxBytes)
        {
            vm.ImportSummary = "that file is too large to import";
            return;
        }

        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        vm.ImportDeck(Path.GetFileNameWithoutExtension(files[0].Name), text);
    }

    private async void OnExportDeckClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedDeck: { } deck } vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var safeName = string.Join("_", deck.Name.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "deck";

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "export deck",
            SuggestedFileName = $"{safeName}.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("text deck") { Patterns = new[] { "*.txt" } }
            }
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(vm.BuildDeckTsv(deck));
    }
}

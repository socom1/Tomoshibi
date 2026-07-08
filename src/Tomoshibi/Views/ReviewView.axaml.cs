using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Export the current deck's notes to a CSV file.</summary>
    private async void OnExportCsv(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DeckViewModel deck }) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "export deck to CSV",
            SuggestedFileName = Sanitize(deck.Name) + ".csv",
            DefaultExtension = "csv",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("CSV") { Patterns = new[] { "*.csv" } }
            }
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            File.WriteAllText(path, deck.ExportCsv());
    }

    /// <summary>Import notes from a CSV/TSV file into the current deck.</summary>
    private async void OnImportCsv(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DeckViewModel deck }) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "import cards from CSV / TSV",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("CSV / TSV / text") { Patterns = new[] { "*.csv", "*.tsv", "*.txt" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            deck.ImportCsv(File.ReadAllText(path));
    }

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "deck" : cleaned;
    }

    /// <summary>Pick an .apkg file, then open the import dialog for it.</summary>
    private async void OnImportApkg(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReviewViewModel review) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "import an Anki deck",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Anki deck") { Patterns = new[] { "*.apkg" } }
            }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            review.OpenApkgImport(path);
    }

    /// <summary>Import an Anki text/TSV export as a brand-new deck.</summary>
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

    /// <summary>Export the open deck as Anki-importable text.</summary>
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

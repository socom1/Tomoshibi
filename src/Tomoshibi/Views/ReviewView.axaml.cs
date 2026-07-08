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
}

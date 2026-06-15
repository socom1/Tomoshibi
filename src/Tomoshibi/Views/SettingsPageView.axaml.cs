using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView()
    {
        InitializeComponent();
    }

    private SettingsPageViewModel? Vm => DataContext as SettingsPageViewModel;

    private async void OnExportGrades(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
            await SaveText("export grades", $"tomoshibi-grades-{DateTime.Now:yyyy-MM-dd}.csv",
                "csv", "*.csv", vm.BuildGradesCsv());
    }

    private async void OnExportTimetable(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
            await SaveText("export timetable", $"tomoshibi-timetable-{DateTime.Now:yyyy-MM-dd}.ics",
                "calendar", "*.ics", vm.BuildTimetableIcs());
    }

    private async void OnBackup(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
            await SaveText("backup data", $"tomoshibi-backup-{DateTime.Now:yyyy-MM-dd}.json",
                "json", "*.json", vm.BuildBackupJson());
    }

    /// <summary>Prompt for a destination and write the text — shared by every
    /// export. A cancelled picker is a no-op.</summary>
    private async Task SaveText(string title, string suggestedName,
                                string typeName, string pattern, string content)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(typeName) { Patterns = new[] { pattern } }
            }
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }

    private async void OnChooseMusicFolder(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "choose your music folder",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            vm.Music.SetFolder(path);
    }

    /// <summary>Reveal the data folder in the OS file manager.</summary>
    private void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        var dir = Path.GetDirectoryName(vm.DataLocation);
        if (dir is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch
        {
            // No file manager — nothing useful to do.
        }
    }
}

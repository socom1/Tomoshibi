using System.Diagnostics;
using System.IO;
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

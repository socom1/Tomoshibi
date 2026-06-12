using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class SubjectsView : UserControl
{
    public SubjectsView()
    {
        InitializeComponent();
    }

    private SubjectsViewModel? Vm => DataContext as SubjectsViewModel;

    /// <summary>Clicking anywhere on a subject card opens its page. The
    /// ✎/✕ buttons handle their presses first, so they don't navigate.</summary>
    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: SubjectViewModel row } && Vm is { } vm)
        {
            vm.OpenDetail(row);
            e.Handled = true;
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SubjectViewModel row } && Vm is { } vm)
        {
            vm.BeginEdit(row);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "export transcript",
            SuggestedFileName = $"tomoshibi-transcript-{DateTime.Now:yyyy-MM-dd}.md",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("markdown") { Patterns = new[] { "*.md" } }
            }
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new System.IO.StreamWriter(stream);
        await writer.WriteAsync(vm.BuildTranscript());
    }
}

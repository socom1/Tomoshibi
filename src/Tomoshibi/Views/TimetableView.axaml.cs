using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class TimetableView : UserControl
{
    public TimetableView()
    {
        InitializeComponent();
    }

    private TimetableViewModel? Vm => DataContext as TimetableViewModel;

    // The + buttons open their flyout natively; the click handler just makes
    // sure a fresh form isn't carrying a half-finished edit.
    private void OnAddSlotOpen(object? sender, RoutedEventArgs e) => Vm?.CancelEdits();
    private void OnAddDeadlineOpen(object? sender, RoutedEventArgs e) => Vm?.CancelEdits();

    private void OnEditSlotClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ClassSlotItemViewModel item } && Vm is { } vm)
        {
            vm.BeginEditSlot(item);
            AddSlotButton.Flyout?.ShowAt(AddSlotButton);
        }
    }

    private async void OnImportIcsClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "import an .ics timetable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("iCalendar") { Patterns = new[] { "*.ics" } }
            }
        });

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();

        // A real timetable export is a few KB; refuse anything absurd rather
        // than reading an unbounded (possibly downloaded) file into memory.
        const long maxBytes = 5 * 1024 * 1024;
        if (stream.CanSeek && stream.Length > maxBytes)
        {
            vm.ImportSummary = "that .ics is too large to import";
            return;
        }

        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        vm.ImportIcs(text);
    }
}

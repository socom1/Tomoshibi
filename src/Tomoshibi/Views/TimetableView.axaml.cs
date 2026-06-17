using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
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

    // The deadline + button opens its flyout natively; the handler just clears
    // any half-finished edit so the fresh form starts empty.
    private void OnAddDeadlineOpen(object? sender, RoutedEventArgs e) => Vm?.CancelEdits();

    /// <summary>Click a class — in the list or on the week grid — to edit it.
    /// The ✕ delete button consumes its own press, so it won't open the form.</summary>
    private void OnSlotPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ClassSlotItemViewModel item } && Vm is { } vm)
        {
            vm.BeginEditSlot(item);
            e.Handled = true;
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

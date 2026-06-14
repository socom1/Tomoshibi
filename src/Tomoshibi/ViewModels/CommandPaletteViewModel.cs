using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The Cmd/Ctrl+K command palette: type to filter across pages, quick
/// actions and subjects; arrow keys move the selection, Enter runs it. The
/// candidate list is rebuilt by the shell each time the palette opens, so it
/// always reflects the current subjects.
/// </summary>
public partial class CommandPaletteViewModel : ViewModelBase
{
    private readonly Action _close;
    private List<PaletteItemViewModel> _all = new();

    public ObservableCollection<PaletteItemViewModel> Results { get; } = new();

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private int _selectedIndex;

    public CommandPaletteViewModel(Action close)
    {
        _close = close;
    }

    /// <summary>Reset with a fresh candidate set when the palette opens.</summary>
    public void Load(IEnumerable<PaletteItemViewModel> items)
    {
        _all = items.ToList();
        Query = string.Empty;
        Filter();
    }

    partial void OnQueryChanged(string value) => Filter();

    private void Filter()
    {
        var q = Query?.Trim() ?? string.Empty;

        Results.Clear();
        foreach (var item in _all)
            if (q.Length == 0 || item.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                Results.Add(item);

        SelectedIndex = Results.Count > 0 ? 0 : -1;
    }

    public void Move(int delta)
    {
        if (Results.Count == 0)
            return;
        SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, Results.Count - 1);
    }

    public void RunSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
            Run(Results[SelectedIndex]);
    }

    public void Run(PaletteItemViewModel? item)
    {
        if (item is null)
            return;
        _close();
        item.Run();
    }
}

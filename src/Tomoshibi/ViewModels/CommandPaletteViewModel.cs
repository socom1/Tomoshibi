using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The Cmd/Ctrl+K command palette: type to filter across pages, quick
/// actions, subjects and your own content — todo tickets, flashcard decks and
/// journal reflections; arrow keys move the selection, Enter runs it. The
/// candidate list is rebuilt by the shell each time the palette opens, so it
/// always reflects the current state.
/// </summary>
public partial class CommandPaletteViewModel : ViewModelBase
{
    private readonly Action _close;
    private readonly Action<PaletteItemViewModel>? _recordUse;
    private List<PaletteItemViewModel> _all = new();

    public ObservableCollection<PaletteItemViewModel> Results { get; } = new();

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private int _selectedIndex;

    public CommandPaletteViewModel(Action close,
        Action<PaletteItemViewModel>? recordUse = null)
    {
        _close = close;
        _recordUse = recordUse;
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
        Results.Clear();

        // Fuzzy, typo-tolerant scoring — see PaletteMatcher for the ranking
        // rules. Frecency breaks ties, so familiar picks float up (and, on
        // an empty query where every score is 0, order the list outright);
        // build order settles what's left, keeping pages grouped on top.
        var ranked = _all
            .Select((item, index) => (Item: item,
                Score: Services.PaletteMatcher.Score(Query, item.Title), Index: index))
            .Where(x => x.Score is not null)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.SortHint)
            .ThenBy(x => x.Index);

        foreach (var match in ranked)
            Results.Add(match.Item);

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
        _recordUse?.Invoke(item); // frecency notes the pick
        _close();
        item.Run();
    }
}

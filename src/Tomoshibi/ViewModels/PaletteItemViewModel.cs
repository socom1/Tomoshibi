using System;

namespace Tomoshibi.ViewModels;

/// <summary>One row in the command palette: a label, a kind tag for the
/// right-hand hint (page / action / subject), and what running it does.</summary>
public class PaletteItemViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Action Run { get; init; } = () => { };

    /// <summary>The stable identity frecency is tracked under — titles and
    /// kinds survive the per-open rebuild, the lambdas don't.</summary>
    public string UsageKey => $"{Kind}:{Title}";

    /// <summary>Frecency warmth, stamped by the shell at build time. Breaks
    /// ranking ties, and orders the whole list when the query is empty.</summary>
    public double SortHint { get; set; }
}

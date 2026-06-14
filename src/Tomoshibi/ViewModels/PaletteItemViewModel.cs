using System;

namespace Tomoshibi.ViewModels;

/// <summary>One row in the command palette: a label, a kind tag for the
/// right-hand hint (page / action / subject), and what running it does.</summary>
public class PaletteItemViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public Action Run { get; init; } = () => { };
}

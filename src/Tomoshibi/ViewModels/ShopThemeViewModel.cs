using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>One theme card in the shop: its swatch, price, and whether it's
/// owned / active / affordable. State is refreshed by the shop after every
/// buy, apply, or balance change.</summary>
public partial class ShopThemeViewModel : ViewModelBase
{
    public AppTheme Theme { get; }

    public string Jp => Theme.Jp;
    public string En => Theme.En;
    public string PriceLabel => Theme.Price == 0 ? "free" : $"火種 {Theme.Price:N0}";

    // Swatch colours straight from the theme's own palette.
    public IBrush SwatchBg => new SolidColorBrush(Theme.Preview("InkBrush"));
    public IBrush SwatchCard => new SolidColorBrush(Theme.Preview("SurfaceBrush"));
    public IBrush SwatchAccent => new SolidColorBrush(Theme.Preview("MatchaBrush"));
    public IBrush SwatchText => new SolidColorBrush(Theme.Preview("TextBrush"));

    [ObservableProperty] private bool _isOwned;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _canAfford;

    /// <summary>What the action button says/does given the state.</summary>
    public string ActionLabel => IsActive ? "active" : IsOwned ? "apply" : "buy";
    public bool ActionEnabled => IsActive ? false : IsOwned ? true : CanAfford;

    public ShopThemeViewModel(AppTheme theme)
    {
        Theme = theme;
    }

    public void SetState(bool owned, bool active, int balance)
    {
        IsOwned = owned;
        IsActive = active;
        CanAfford = balance >= Theme.Price;
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(ActionEnabled));
    }
}

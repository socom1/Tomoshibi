using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The ショップ · shop — spend embers on themes. Owns the catalogue rows,
/// drives buy/apply, and keeps every row's owned/active/affordable state in
/// step with the shared wallet.
/// </summary>
public partial class ShopViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    public WalletViewModel Wallet { get; }

    public ObservableCollection<ShopThemeViewModel> Themes { get; } = new();

    [ObservableProperty] private string _flash = string.Empty;

    public ShopViewModel(AppState state, Action save, WalletViewModel wallet)
    {
        _state = state;
        _save = save;
        Wallet = wallet;

        foreach (var theme in ThemeService.All)
            Themes.Add(new ShopThemeViewModel(theme));

        Wallet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WalletViewModel.Balance))
                RefreshStates();
        };

        RefreshStates();
    }

    [RelayCommand]
    private void Activate(ShopThemeViewModel? row)
    {
        if (row is null)
            return;

        if (row.IsOwned)
        {
            ApplyTheme(row.Theme.Id);
            Flash = $"{row.En} applied";
        }
        else if (Wallet.TrySpend(row.Theme.Price))
        {
            _state.OwnedThemeIds.Add(row.Theme.Id);
            ApplyTheme(row.Theme.Id);
            Flash = $"unlocked {row.En} — applied";
        }
        else
        {
            Flash = $"need {row.Theme.Price - Wallet.Balance:N0} more 火種 for {row.En}";
        }

        RefreshStates();
    }

    private void ApplyTheme(string id)
    {
        _state.ActiveThemeId = id;
        ThemeService.Apply(id);
        _save();
    }

    private void RefreshStates()
    {
        foreach (var row in Themes)
        {
            var owned = row.Theme.Price == 0 || _state.OwnedThemeIds.Contains(row.Theme.Id);
            row.SetState(owned, _state.ActiveThemeId == row.Theme.Id, Wallet.Balance);
        }
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The embers wallet — one shared instance the timer earns into, the shop
/// spends from, and the dashboard displays. Backed by AppState so the
/// balance survives restarts.
/// </summary>
public partial class WalletViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;

    [ObservableProperty]
    private int _balance;

    /// <summary>"火種 1,240" — the balance with the currency mark.</summary>
    public string BalanceLabel => $"火種 {Balance:N0}";

    public WalletViewModel(AppState state, Action save)
    {
        _state = state;
        _save = save;
        _balance = state.Embers;
    }

    public void Add(int amount)
    {
        if (amount <= 0)
            return;
        Balance += amount;
        _state.Embers = Balance;
        _save();
    }

    /// <summary>Deduct if affordable; returns whether it went through.</summary>
    public bool TrySpend(int amount)
    {
        if (amount > Balance)
            return false;
        Balance -= amount;
        _state.Embers = Balance;
        _save();
        return true;
    }

    partial void OnBalanceChanged(int value) => OnPropertyChanged(nameof(BalanceLabel));
}

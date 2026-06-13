using System;
using CommunityToolkit.Mvvm.Input;

namespace Tomoshibi.ViewModels;

/// <summary>
/// A "needs work" row on the dashboard: a subject sitting below its target
/// (or below your average, or trending down). Two actions — open the
/// subject, or drop a focus task for it onto today.
/// </summary>
public partial class WeakSpotViewModel : ViewModelBase
{
    private readonly Action _open;
    private readonly Action _sendToToday;

    public string Name { get; }
    public string? Code { get; }
    public bool HasCode => !string.IsNullOrWhiteSpace(Code);

    /// <summary>"8.0% below your 70% target", "trending down", …</summary>
    public string Message { get; }

    /// <summary>Worst → mildest, drives the tint (sakura / amber).</summary>
    public bool IsSevere { get; }

    public WeakSpotViewModel(string name, string? code, string message, bool isSevere,
                             Action open, Action sendToToday)
    {
        Name = name;
        Code = code;
        Message = message;
        IsSevere = isSevere;
        _open = open;
        _sendToToday = sendToToday;
    }

    [RelayCommand] private void Open() => _open();
    [RelayCommand] private void SendToToday() => _sendToToday();
}

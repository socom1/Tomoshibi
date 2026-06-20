using System;
using CommunityToolkit.Mvvm.Input;

namespace Tomoshibi.ViewModels;

/// <summary>One row of the first-run checklist: what to do, whether it's done,
/// the ember reward for it, and the jump that takes you there.</summary>
public class FirstStepViewModel
{
    public string Label { get; }
    public bool IsDone { get; }
    public int Reward { get; }
    public IRelayCommand GoCommand { get; }

    public string Glyph => IsDone ? "✓" : "▸";
    public string RewardLabel => IsDone ? "done" : $"+{Reward} 火種";

    public FirstStepViewModel(string label, bool done, int reward, Action go)
    {
        Label = label;
        IsDone = done;
        Reward = reward;
        GoCommand = new RelayCommand(go);
    }
}

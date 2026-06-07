using CommunityToolkit.Mvvm.ComponentModel;

namespace Tomoshibi.ViewModels;

/// <summary>
/// Shared base for all view models. ObservableObject gives us
/// INotifyPropertyChanged plus the [ObservableProperty] source generator.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}

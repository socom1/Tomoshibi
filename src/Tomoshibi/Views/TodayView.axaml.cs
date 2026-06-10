using Avalonia.Controls;
using Avalonia.Interactivity;
using Tomoshibi.Models;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class TodayView : UserControl
{
    public TodayView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The done-checkbox can't carry a Command, so the click lands here and
    /// is forwarded to the view model. The row's TaskBlock is the checkbox's
    /// DataContext; the parse that follows refreshes IsChecked.
    /// </summary>
    private void OnTaskDoneClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: TaskBlock task } &&
            DataContext is TodayViewModel vm)
        {
            vm.Tasks.ToggleDoneCommand.Execute(task);
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TodoItemViewModel row } &&
            DataContext is TodoViewModel vm)
        {
            vm.BeginEdit(row);
        }
    }
}

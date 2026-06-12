using Avalonia.Controls;
using Avalonia.Interactivity;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class SubjectsView : UserControl
{
    public SubjectsView()
    {
        InitializeComponent();
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SubjectViewModel row } &&
            DataContext is SubjectsViewModel vm)
        {
            vm.BeginEdit(row);
        }
    }
}

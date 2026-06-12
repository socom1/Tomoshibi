using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class SubjectsView : UserControl
{
    public SubjectsView()
    {
        InitializeComponent();
    }

    private SubjectsViewModel? Vm => DataContext as SubjectsViewModel;

    /// <summary>Clicking anywhere on a subject card opens its page. The
    /// ✎/✕ buttons handle their presses first, so they don't navigate.</summary>
    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: SubjectViewModel row } && Vm is { } vm)
        {
            vm.OpenDetail(row);
            e.Handled = true;
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SubjectViewModel row } && Vm is { } vm)
        {
            vm.BeginEdit(row);
        }
    }
}

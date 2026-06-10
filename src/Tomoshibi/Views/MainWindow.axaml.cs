using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Tomoshibi.ViewModels;

namespace Tomoshibi.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Space toggles the timer from anywhere — except while typing in a text
    /// field or while the add-task modal is up, where space means space.
    /// Controls that consume space themselves (buttons, checkboxes) mark the
    /// event handled before it bubbles here, so they're unaffected.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Space || _vm is null)
            return;

        if (_vm.Today.Tasks.IsAddTaskModalOpen)
            return;

        if (FocusManager?.GetFocusedElement() is TextBox)
            return;

        _vm.Today.Pomodoro.ToggleRunCommand.Execute(null);
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as MainWindowViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            ApplyZenState();
            ApplyNavState();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsZenMode))
            ApplyZenState();
        else if (e.PropertyName == nameof(MainWindowViewModel.IsNavOpen))
            ApplyNavState();
    }

    private void ApplyZenState()
    {
        if (_vm is null) return;
        WindowState = _vm.IsZenMode ? WindowState.FullScreen : WindowState.Normal;
    }

    /// <summary>
    /// Flips the three column widths so the nav rail slides in and out:
    /// open takes a small fixed slice on the left (~Auto, capped) and the
    /// main area takes the rest; closed collapses both nav and divider.
    /// </summary>
    private void ApplyNavState()
    {
        if (_vm is null) return;
        var cols = NormalLayoutGrid.ColumnDefinitions;
        if (_vm.IsNavOpen)
        {
            cols[0].Width = GridLength.Auto;
            cols[1].Width = new GridLength(1);
            cols[2].Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            cols[0].Width = new GridLength(0);
            cols[1].Width = new GridLength(0);
            cols[2].Width = new GridLength(1, GridUnitType.Star);
        }
    }
}

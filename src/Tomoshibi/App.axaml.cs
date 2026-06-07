using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tomoshibi.Services;
using Tomoshibi.ViewModels;
using Tomoshibi.Views;

namespace Tomoshibi;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IStorageService storage = new JsonStorageService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(storage)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

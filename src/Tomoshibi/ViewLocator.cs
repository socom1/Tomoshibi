using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Tomoshibi.ViewModels;

namespace Tomoshibi;

/// <summary>
/// Maps a view model to its matching view by naming convention
/// (Foo.ViewModels.BarViewModel -> Foo.Views.BarView). Used by the
/// DataTemplate in App.axaml so content view models render automatically.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}

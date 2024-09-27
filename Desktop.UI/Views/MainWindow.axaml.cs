using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Remotely.Desktop.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Remotely.Desktop.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        if (!Design.IsDesignMode)
        {
            DataContext = StaticServiceProvider.Instance?.GetService<IMainWindowViewModel>();
        }

        Position = new PixelPoint(0, 0);
        this.Opened += OnInitialized;
        InitializeComponent();
        Closed += MainWindow_Closed;
    }
    
    private void OnInitialized(object? sender, EventArgs e)
    {
        Hide();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        var dispatcher = StaticServiceProvider.Instance?.GetService<IUiDispatcher>();
        dispatcher?.Shutdown();
    }
}

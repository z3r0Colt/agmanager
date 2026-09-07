using System.Windows.Controls;
using AgApp.ViewModels;

namespace AgApp.Views;

public partial class LogPage : UserControl
{
    public LogPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LogViewModel vm)
        {
            vm.Refresh();
            vm.StartAutoRefresh();
        }
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LogViewModel vm)
            vm.StopAutoRefresh();
    }
}

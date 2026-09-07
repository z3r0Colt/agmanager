using System.Windows.Controls;
using AgApp.ViewModels;

namespace AgApp.Views;

public partial class StoragePage : UserControl
{
    public StoragePage()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as StorageViewModel)?.Refresh();
    }
}

using System.Windows.Controls;
using System.Windows;
using LauncherTF2.ViewModels;

namespace LauncherTF2.Views;

public partial class ModsView : UserControl
{
    public ModsView()
    {
        InitializeComponent();
    }

    private async void ModsScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ModsViewModel viewModel)
        {
            await viewModel.TryLoadMoreFromScrollAsync(e.VerticalOffset, e.ViewportHeight, e.ExtentHeight);
        }
    }
}

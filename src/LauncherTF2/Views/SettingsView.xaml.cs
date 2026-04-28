using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LauncherTF2.ViewModels;

namespace LauncherTF2.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        this.KeyDown += SettingsView_KeyDown;
        this.PreviewMouseDown += SettingsView_PreviewMouseDown;
    }

    /// <summary>
    /// Scrolls the content panel to the section matching the clicked category button's Tag.
    /// </summary>
    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton btn && btn.Tag is string sectionName)
        {
            var section = this.FindName(sectionName) as FrameworkElement;
            section?.BringIntoView();
        }
    }

    private void SettingsView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.HandleKeyPress(e.Key);
        }
    }

    private void SettingsView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            if (vm.HandleMousePress(e.ChangedButton))
            {
                e.Handled = true;
            }
        }
    }
}

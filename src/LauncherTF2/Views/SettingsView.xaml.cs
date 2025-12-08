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

    private void SettingsView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            // Note: HandleKeyPress is void, effectively logic is same as mouse but we don't swallow keys yet (maybe we should?)
            // For now leaving as is unless user complains about typing 'w' into a box while binding 'w'.
            vm.HandleKeyPress(e.Key);
        }
    }

    private void SettingsView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            // Pass the changed button (the one that was pressed)
            if (vm.HandleMousePress(e.ChangedButton))
            {
                e.Handled = true;
            }
        }
    }
}

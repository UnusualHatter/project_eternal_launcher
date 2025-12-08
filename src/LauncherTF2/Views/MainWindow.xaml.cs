using System.Windows;

namespace LauncherTF2.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            TrayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "");
        }
        catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is LauncherTF2.ViewModels.MainViewModel vm)
        {
            vm.Cleanup();
        }
    }
}

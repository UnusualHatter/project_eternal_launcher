using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System;
using System.IO;

namespace LauncherTF2.Views;

public partial class MainWindow : Window
{
    private readonly Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;
    private readonly System.Windows.Controls.ContentControl? _mainContentControl;

    public MainWindow()
    {
        // Carrega o XAML manualmente para evitar dependência dos campos gerados pelo designer.
        Application.LoadComponent(this, new Uri("/LauncherTF2;component/Views/MainWindow.xaml", UriKind.Relative));

        _trayIcon = FindName("TrayIcon") as Hardcodet.Wpf.TaskbarNotification.TaskbarIcon;
        _mainContentControl = FindName("MainContentControl") as System.Windows.Controls.ContentControl;

        try
        {
            // Usa o ícone associado ao executável para garantir compatibilidade com NotifyIcon.
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null && File.Exists(assembly.Location))
            {
                var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(assembly.Location);
                if (appIcon != null && _trayIcon != null)
                    _trayIcon.Icon = appIcon;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set tray icon: {ex.Message}");
        }

        // Setup Content transition animation
        if (DataContext is LauncherTF2.ViewModels.MainViewModel vm)
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.CurrentView))
                {
                    AnimateContentTransition();
                }
            };
        }
        else
        {
            this.DataContextChanged += (s, e) =>
            {
                if (e.NewValue is LauncherTF2.ViewModels.MainViewModel newVm)
                {
                    newVm.PropertyChanged += (s2, e2) =>
                    {
                        if (e2.PropertyName == nameof(newVm.CurrentView))
                        {
                            AnimateContentTransition();
                        }
                    };
                }
            };
        }
    }

    private void AnimateContentTransition()
    {
        if (_mainContentControl == null) return;
        
        var fadeOut = new DoubleAnimation(0.5, TimeSpan.FromSeconds(0.05));
        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.15));

        fadeOut.Completed += (s, e) => _mainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        _mainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizarParaBandeja();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizarParaBandeja();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is LauncherTF2.ViewModels.MainViewModel vm)
        {
            vm.Cleanup();
        }
    }

    private void MinimizarParaBandeja()
    {
        Hide();
        _trayIcon?.ShowBalloonTip("Eternal TF2", "O launcher continua em execução em segundo plano.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }
}

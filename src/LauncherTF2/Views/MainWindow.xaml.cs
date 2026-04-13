using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System;

namespace LauncherTF2.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            // Set tray icon from window icon (Window.Icon is already set to logo64.png)
            if (Icon != null)
            {
                using (var stream = new System.IO.MemoryStream())
                {
                    Icon.Save(stream);
                    stream.Position = 0;
                    TrayIcon.Icon = new System.Drawing.Icon(stream);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set tray icon: {ex.Message}");
            
            // Fallback: try to get from executable
            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                if (assembly != null)
                {
                    TrayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(assembly.Location);
                }
            }
            catch { }
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
        if (MainContentControl == null) return;
        
        var fadeOut = new DoubleAnimation(0.5, TimeSpan.FromSeconds(0.05));
        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.15));

        fadeOut.Completed += (s, e) => MainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        MainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
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
        this.WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Minimize to tray
        this.Hide();
        TrayIcon.ShowBalloonTip("Eternal TF2", "Launcher is still running in the background.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
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

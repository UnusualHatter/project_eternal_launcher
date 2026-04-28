using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System;
using System.ComponentModel;
using System.IO;

namespace LauncherTF2.Views;

/// <summary>
/// Main application window — manages the custom title bar, system tray icon,
/// and content transition animations between tabs.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;
    private readonly System.Windows.Controls.ContentControl? _mainContentControl;
    private readonly TranslateTransform? _mainContentTransform;

    public MainWindow()
    {
        Application.LoadComponent(this, new Uri("/LauncherTF2;component/Views/MainWindow.xaml", UriKind.Relative));

        _trayIcon = FindName("TrayIcon") as Hardcodet.Wpf.TaskbarNotification.TaskbarIcon;
        _mainContentControl = FindName("MainContentControl") as System.Windows.Controls.ContentControl;
        _mainContentTransform = _mainContentControl?.RenderTransform as TranslateTransform;

        // Extract the app icon from the executable for tray display
        try
        {
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

        // Use WeakEventManager to avoid leaking a strong reference from the VM back to the View
        if (DataContext is LauncherTF2.ViewModels.MainViewModel vm)
        {
            PropertyChangedEventManager.AddHandler(vm, OnCurrentViewChanged, nameof(vm.CurrentView));
        }

        DataContextChanged += OnDataContextChanged;
    }

    // Swaps the PropertyChanged subscription when DataContext changes
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            PropertyChangedEventManager.RemoveHandler(oldVm, OnCurrentViewChanged, nameof(LauncherTF2.ViewModels.MainViewModel.CurrentView));

        if (e.NewValue is LauncherTF2.ViewModels.MainViewModel newVm)
            PropertyChangedEventManager.AddHandler(newVm, OnCurrentViewChanged, nameof(newVm.CurrentView));
    }

    private void OnCurrentViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        AnimateContentTransition();
    }

    // Quick fade-out → fade-in when switching tabs
    private void AnimateContentTransition()
    {
        if (_mainContentControl == null) return;

        var fadeOut = new DoubleAnimation(0.55, TimeSpan.FromSeconds(0.06));
        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.16));
        var slideOut = new DoubleAnimation(10, TimeSpan.FromSeconds(0.06));
        var slideIn = new DoubleAnimation(0, TimeSpan.FromSeconds(0.16));

        if (_mainContentTransform != null)
        {
            _mainContentTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
            fadeOut.Completed += (_, _) =>
            {
                _mainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                _mainContentTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            };
        }
        else
        {
            fadeOut.Completed += (_, _) => _mainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        _mainContentControl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    // Custom title bar drag support
    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => HideToTray();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    /// <summary>
    /// Intercepts Alt+F4 and taskbar close — the app can only be fully
    /// closed via the tray context menu "Exit" command.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon?.ShowBalloonTip(
            "Eternal TF2",
            "O launcher continua em execução em segundo plano.",
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }
}

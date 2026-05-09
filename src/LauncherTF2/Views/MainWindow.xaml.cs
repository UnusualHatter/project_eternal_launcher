using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System;
using System.ComponentModel;
using System.IO;
using LauncherTF2.Core;

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

        // Load the packed logo64.ico for crisp multi-resolution tray rendering
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/Assets/logo64.ico", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(iconUri);
            if (streamInfo != null && _trayIcon != null)
            {
                using var stream = streamInfo.Stream;
                _trayIcon.Icon = new System.Drawing.Icon(stream);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[MainWindow] Failed to load tray icon from packed resource", ex);
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

        _mainContentControl.BeginAnimation(UIElement.OpacityProperty, null);
        _mainContentTransform?.BeginAnimation(TranslateTransform.YProperty, null);

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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (ServiceLocator.Settings.GetLauncherConfig().CloseToTray)
            HideToTray();
        else
            Application.Current.Shutdown();
    }

    /// <summary>
    /// Intercepts Alt+F4 and taskbar close. Honours the CloseToTray launcher
    /// preference — when disabled, allows a normal shutdown instead.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (ServiceLocator.Settings.GetLauncherConfig().CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();

        if (_trayIcon != null && ServiceLocator.Settings.GetLauncherConfig().ShowNotifications)
        {
            _trayIcon.ShowBalloonTip(
                "Eternal TF2",
                "O launcher continua em execução em segundo plano.",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }
}

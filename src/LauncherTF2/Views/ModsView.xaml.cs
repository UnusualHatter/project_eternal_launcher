using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;

namespace LauncherTF2.Views;

public partial class ModsView : UserControl
{
    public ModsView()
    {
        InitializeComponent();
        DataContextChanged += ModsView_DataContextChanged;
    }

    private void ModsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ViewModels.ModsViewModel vm)
        {
            vm.PropertyChanged += Vm_PropertyChanged;
            if (vm.PreloaderHwnd != IntPtr.Zero)
            {
                HostHandle(vm.PreloaderHwnd);
            }
        }
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.ModsViewModel.PreloaderHwnd))
        {
            var vm = DataContext as ViewModels.ModsViewModel;
            if (vm != null)
            {
                if (vm.PreloaderHwnd != IntPtr.Zero)
                {
                    HostHandle(vm.PreloaderHwnd);
                }
                else
                {
                    Dispatcher.Invoke(() => HostContainer.Child = null);
                }
            }
        }
    }

    private async void HostHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;

        bool success = false;
        for (int i = 0; i < 5; i++)
        {
            if (IsWindow(handle))
            {
                success = true;
                break;
            }
            await System.Threading.Tasks.Task.Delay(100);
        }

        if (success)
        {
            Dispatcher.Invoke(() =>
            {
                var host = new LauncherTF2.Core.EmbeddedWindowHost(handle);
                HostContainer.Child = host;
            });
        }
        else
        {
            LauncherTF2.Core.Logger.Log($"[ModsView] Failed to host window {handle}: Invalid HWND after timeout.");
            if (DataContext is ViewModels.ModsViewModel vm)
            {
                vm.HandleEmbeddingError();
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

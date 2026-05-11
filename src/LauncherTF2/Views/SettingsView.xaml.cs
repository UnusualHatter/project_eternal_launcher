using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LauncherTF2.Core;
using LauncherTF2.Models.Settings;
using LauncherTF2.ViewModels;

namespace LauncherTF2.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _vm;

    public SettingsView()
    {
        InitializeComponent();
        KeyDown += SettingsView_KeyDown;
        PreviewMouseDown += SettingsView_PreviewMouseDown;
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Subscribes to the VM's <c>ScrollToCategoryRequested</c> event so a
    /// sidebar click can trigger a smooth scroll without the VM holding
    /// a reference to the visual tree.
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.ScrollToCategoryRequested -= OnScrollRequested;
        _vm = DataContext as SettingsViewModel;
        if (_vm != null) _vm.ScrollToCategoryRequested += OnScrollRequested;
    }

    private void OnScrollRequested(object? sender, string anchorId)
    {
        var target = ScrollAnchor.Find(ContentScroller, anchorId);
        if (target == null) return;
        AnimatedScrollHelper.ScrollToElement(ContentScroller, target);
    }

    /// <summary>
    /// Drives the IsActive highlight in the sidebar by figuring out which
    /// anchor sits closest to (but at or above) the viewport top. This is
    /// resilient to dynamically generated categories because anchors register
    /// themselves with <see cref="ScrollAnchor"/> on Loaded.
    /// </summary>
    private void ContentScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_vm == null) return;

        var top = ContentScroller.VerticalOffset;
        // A small lead-in so the new category lights up just as it's about to enter the viewport.
        const double viewportLead = 80;

        string? bestId = null;
        double bestY = double.NegativeInfinity;

        foreach (var (id, fe) in ScrollAnchor.All(ContentScroller))
        {
            try
            {
                var content = ContentScroller.Content as Visual;
                if (content == null) continue;
                var y = fe.TransformToVisual(content).Transform(new Point(0, 0)).Y;
                if (y <= top + viewportLead && y > bestY)
                {
                    bestY = y;
                    bestId = id;
                }
            }
            catch
            {
                // Element not in the visual tree yet — skip.
            }
        }

        if (!string.IsNullOrEmpty(bestId)) _vm.SyncActiveFromScroll(bestId!);
    }

    /// <summary>Click handler for preset chips inside the PresetSetting DataTemplate.</summary>
    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.Tag is not string id) return;
        // The button's DataContext is the PresetOption; walk up to find the PresetSetting.
        var parent = fe.DataContext;
        var preset = FindPresetSetting(fe);
        preset?.ApplyById(id);
    }

    private static PresetSetting? FindPresetSetting(DependencyObject start)
    {
        var p = LogicalTreeHelper.GetParent(start);
        while (p != null)
        {
            if (p is FrameworkElement fe && fe.DataContext is PresetSetting ps) return ps;
            p = LogicalTreeHelper.GetParent(p);
        }
        // Fall back to visual tree if logical traversal missed (ItemsControl can detach logical scope).
        var v = start;
        while (v != null)
        {
            if (v is FrameworkElement fe2 && fe2.DataContext is PresetSetting ps2) return ps2;
            v = VisualTreeHelper.GetParent(v);
        }
        return null;
    }

    private void SettingsView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.HandleKeyPress(e.Key);
    }

    private void SettingsView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            if (vm.HandleMousePress(e.ChangedButton)) e.Handled = true;
        }
    }
}

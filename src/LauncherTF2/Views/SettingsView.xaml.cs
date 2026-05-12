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

    private void ContentScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_vm == null) return;

        // Skip the active-section recompute while AnimatedScrollHelper is driving
        // the offset — every animation frame fires ScrollChanged, and recomputing
        // would strobe the sidebar highlight through every section we cross.
        // The helper keeps IsAnimating true for 2 extra dispatcher frames after
        // the animation ends to drain late events, so we can rely on it solely.
        if (AnimatedScrollHelper.IsAnimating(ContentScroller)) return;

        var top = ContentScroller.VerticalOffset;
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
                if (y <= top + viewportLead && y > bestY) { bestY = y; bestId = id; }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(bestId))
            _vm.SyncActiveFromScroll(bestId);
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.Tag is not string id) return;
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
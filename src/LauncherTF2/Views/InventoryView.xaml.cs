using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Threading;
using LauncherTF2.ViewModels;

namespace LauncherTF2.Views;

public partial class InventoryView : UserControl
{
    private bool _loadoutBrowserInitialized;
    private InventoryViewModel? _viewModel;

    public InventoryView()
    {
        InitializeComponent();
        Loaded += InventoryView_Loaded;
        DataContextChanged += InventoryView_DataContextChanged;
    }

    private void InventoryView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = e.NewValue as InventoryViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private async void InventoryView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            _viewModel = DataContext as InventoryViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        UpdateBackpackCardWidth();

        await EnsureLoadoutBrowserIfNeededAsync();
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InventoryViewModel.IsLoadoutTabActive))
        {
            await EnsureLoadoutBrowserIfNeededAsync();
        }

        if (e.PropertyName == nameof(InventoryViewModel.IsDetailPanelOpen) ||
            e.PropertyName == nameof(InventoryViewModel.IsDetailPanelExpanded))
        {
            _ = Dispatcher.BeginInvoke(new Action(UpdateBackpackCardWidth), DispatcherPriority.Background);
        }
    }

    private void BackpackScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBackpackCardWidth();
    }

    private void UpdateBackpackCardWidth()
    {
        if (_viewModel == null)
            return;

        var viewport = BackpackScrollViewer.ViewportWidth;
        if (double.IsNaN(viewport) || viewport <= 0)
            viewport = BackpackScrollViewer.ActualWidth;

        if (viewport <= 0)
            return;

        // Reserve space for the vertical scrollbar so the last column isn't clipped
        const double scrollbarWidth = 18;
        const double minCardWidth = 130;
        const double maxCardWidth = 164;
        const double horizontalMargin = 8; // 4 left + 4 right from Border Margin

        var usable = viewport - scrollbarWidth;
        var columns = Math.Max(1, (int)Math.Floor(usable / (minCardWidth + horizontalMargin)));
        var computedWidth = Math.Floor((usable / columns) - horizontalMargin);
        var clamped = Math.Max(minCardWidth, Math.Min(maxCardWidth, computedWidth));

        _viewModel.InventoryCardWidth = clamped;
    }

    private async Task EnsureLoadoutBrowserIfNeededAsync()
    {
        if (_loadoutBrowserInitialized)
            return;

        if (_viewModel?.IsLoadoutTabActive != true)
            return;

        await LoadoutBrowser.EnsureCoreWebView2Async();

        var requestedUrl = _viewModel?.LoadoutUrl;
        if (!Uri.TryCreate(requestedUrl, UriKind.Absolute, out var targetUri))
        {
            targetUri = new Uri("https://loadout.tf/", UriKind.Absolute);
        }

        LoadoutBrowser.Source = targetUri;
        _loadoutBrowserInitialized = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (LoadoutBrowser.CanGoBack)
        {
            LoadoutBrowser.GoBack();
        }
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (LoadoutBrowser.CanGoForward)
        {
            LoadoutBrowser.GoForward();
        }
    }
}

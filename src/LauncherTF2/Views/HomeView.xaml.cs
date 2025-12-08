using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LauncherTF2.Views;

public partial class HomeView : UserControl
{
    private readonly DispatcherTimer _timer;
    private readonly List<string> _backgrounds = new()
    {
        "/Resources/Assets/backrnd1.png",
        "/Resources/Assets/backrnd2.png",
        "/Resources/Assets/backrnd3.png",
        "/Resources/Assets/backrnd4.png",
        "/Resources/Assets/backrnd5.png"
    };

    private int _currentIndex = 0;
    private bool _isImage1Active = true;

    public HomeView()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _timer.Tick += OnBackgroundTimerTick;

        Loaded += HomeView_Loaded;
        Unloaded += HomeView_Unloaded;
    }

    private void HomeView_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
    }

    private void HomeView_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void OnBackgroundTimerTick(object? sender, EventArgs e)
    {
        // Advance index
        _currentIndex = (_currentIndex + 1) % _backgrounds.Count;
        string nextImagePath = _backgrounds[_currentIndex];
        var imageSource = new BitmapImage(new Uri(nextImagePath, UriKind.RelativeOrAbsolute));

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));

        if (_isImage1Active)
        {
            // Transition from Image1 (Active) to Image2
            BackgroundImage2.Source = imageSource;
            BackgroundImage2.BeginAnimation(OpacityProperty, fadeIn);
            BackgroundImage1.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            // Transition from Image2 (Active) to Image1
            BackgroundImage1.Source = imageSource;
            BackgroundImage1.BeginAnimation(OpacityProperty, fadeIn);
            BackgroundImage2.BeginAnimation(OpacityProperty, fadeOut);
        }

        _isImage1Active = !_isImage1Active;
    }
}

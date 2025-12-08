using System.Windows.Controls;

namespace LauncherTF2.Views;

public partial class BlogView : UserControl
{
    public BlogView()
    {
        InitializeComponent();
    }

    private void Back_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Browser.CanGoBack)
        {
            Browser.GoBack();
        }
    }

    private void Forward_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Browser.CanGoForward)
        {
            Browser.GoForward();
        }
    }
}

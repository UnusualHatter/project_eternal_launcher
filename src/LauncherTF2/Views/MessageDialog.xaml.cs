using System.Windows;
using System.Windows.Media;

namespace LauncherTF2.Views;

public partial class MessageDialog : Window
{
    public bool Confirmed { get; private set; }

    private MessageDialog(string title, string message, bool showCancel, string okLabel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = okLabel;
        
        if (showCancel)
        {
            CancelButton.Visibility = Visibility.Visible;
        }

        // Inherit the application's theme resources so the dialog respects color scheme
        if (Application.Current?.Resources.MergedDictionaries != null)
        {
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                Resources.MergedDictionaries.Add(dict);
            }
        }

        // Allow dragging
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }

    public static void ShowError(string title, string message)
    {
        var dialog = new MessageDialog(title, message, false, "OK");
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        dialog.ShowDialog();
    }

    public static bool ShowConfirm(string title, string message, string confirmLabel = "Confirm")
    {
        var dialog = new MessageDialog(title, message, true, confirmLabel);
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        dialog.ShowDialog();
        return dialog.Confirmed;
    }
}

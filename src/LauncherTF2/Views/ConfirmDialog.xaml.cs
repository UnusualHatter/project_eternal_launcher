using System.Windows;

namespace LauncherTF2.Views;

/// <summary>
/// Styled confirmation dialog that matches the launcher's dark theme.
/// Use <see cref="Show"/> to display it and get a bool result.
/// </summary>
public partial class ConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    private ConfirmDialog(string title, string message, string confirmLabel = "Remove")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;

        // Allow dragging the borderless window
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
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

    /// <summary>
    /// Shows the dialog centered on the active window.
    /// Returns <c>true</c> if the user confirmed.
    /// </summary>
    public static bool Show(string title, string message, string confirmLabel = "Remove")
    {
        var dialog = new ConfirmDialog(title, message, confirmLabel)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        return dialog.Confirmed;
    }
}

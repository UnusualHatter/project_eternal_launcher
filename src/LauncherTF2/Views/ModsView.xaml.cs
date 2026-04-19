using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LauncherTF2.ViewModels;

namespace LauncherTF2.Views;

public partial class ModsView : UserControl
{
    public ModsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles file/folder drops on the install panel.
    /// </summary>
    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        ResetDropVisual();

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            DataContext is ModsViewModel viewModel)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths != null && paths.Length > 0)
            {
                await viewModel.HandleDropAsync(paths);
            }
        }
    }

    /// <summary>
    /// Visual feedback when dragging files over the drop zone.
    /// </summary>
    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && sender is Border border)
        {
            border.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
            border.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0x6B, 0x00)); // AccentColor with low alpha
        }
    }

    /// <summary>
    /// Explicitly tells Windows we accept the dragged files.
    /// Without this, WPF sometimes defaults to DragDropEffects.None (showing a 🚫 cursor).
    /// </summary>
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Reset visual when dragging leaves the drop zone.
    /// </summary>
    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropVisual();
    }

    private void ResetDropVisual()
    {
        if (DropZone != null)
        {
            DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            DropZone.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0D));
        }
    }
}

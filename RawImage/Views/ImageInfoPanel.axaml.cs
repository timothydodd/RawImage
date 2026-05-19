using Avalonia.Controls;
using Avalonia.Interactivity;
using RawImage.ViewModels;

namespace RawImage.Views;

public partial class ImageInfoPanel : UserControl
{
    public ImageInfoPanel()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if ((VisualRoot as Window)?.DataContext is MainWindowViewModel vm)
        {
            vm.ToggleInfoPanel();
        }
    }

    private void OnRemoveHistoryItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImageInfoPanelViewModel vm && sender is Button btn && btn.DataContext is ColorHistoryItem item)
        {
            vm.RemoveHistoryItem(item);
        }
    }
}

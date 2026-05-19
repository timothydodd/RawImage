using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RawImage.Models;
using RawImage.ViewModels;

namespace RawImage.Views;

public partial class FileBrowser : UserControl
{
    public FileBrowser()
    {
        InitializeComponent();
    }

    private void OnItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is FileBrowserViewModel vm && sender is Border border && border.DataContext is FileNode node)
        {
            vm.SetSelection(node);
        }
    }

    private void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if ((VisualRoot as Window)?.DataContext is MainWindowViewModel vm)
        {
            vm.OpenBrowseAtCurrentFolder();
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RawImage.ViewModels;

namespace RawImage.Views;

public partial class BrowseWindow : Window
{
    public BrowseWindow()
    {
        InitializeComponent();
    }

    public string? SelectedImagePath { get; private set; }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is BrowseWindowViewModel vm)
        {
            vm.ImageSelected += OnImageSelected;
        }
    }

    private void OnImageSelected(string path)
    {
        SelectedImagePath = path;
        Close(path);
    }

    private void OnImageTilePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ImageTile tile)
        {
            SelectedImagePath = tile.Path;
            Close(tile.Path);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}

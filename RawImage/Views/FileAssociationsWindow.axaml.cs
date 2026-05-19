using Avalonia.Controls;
using Avalonia.Interactivity;
using RawImage.ViewModels;

namespace RawImage.Views;

public partial class FileAssociationsWindow : Window
{
    public FileAssociationsWindow()
    {
        InitializeComponent();
        DataContext = new FileAssociationsViewModel();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

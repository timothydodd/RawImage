using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace RawImage.ViewModels;

public partial class ColorHistoryItem : ObservableObject
{
    [ObservableProperty]
    private Color _color;

    [ObservableProperty]
    private string _hex = string.Empty;

    [ObservableProperty]
    private int _x;

    [ObservableProperty]
    private int _y;

    [ObservableProperty]
    private int _red;

    [ObservableProperty]
    private int _green;

    [ObservableProperty]
    private int _blue;
}

public partial class ImageInfoPanelViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _red;

    [ObservableProperty]
    private int _green;

    [ObservableProperty]
    private int _blue;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private Point _position;

    public ObservableCollection<ColorHistoryItem> ColorHistory { get; } = new();

    private const int MaxHistoryItems = 10;

    public Color CurrentColor => Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);

    public string PositionText => $"X: {(int)Position.X}, Y: {(int)Position.Y}";

    public string DimensionsText => $"{ImageWidth} x {ImageHeight}";

    public string ColorHex => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public void AddToHistory()
    {
        var item = new ColorHistoryItem
        {
            Color = CurrentColor,
            Hex = ColorHex,
            X = (int)Position.X,
            Y = (int)Position.Y,
            Red = Red,
            Green = Green,
            Blue = Blue
        };

        // Insert at the beginning
        ColorHistory.Insert(0, item);

        // Limit history size
        while (ColorHistory.Count > MaxHistoryItems)
        {
            ColorHistory.RemoveAt(ColorHistory.Count - 1);
        }
    }

    [RelayCommand]
    public void ClearHistory()
    {
        ColorHistory.Clear();
    }

    [RelayCommand]
    public void RemoveHistoryItem(ColorHistoryItem? item)
    {
        if (item != null)
        {
            ColorHistory.Remove(item);
        }
    }

    partial void OnRedChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(ColorHex));
    }

    partial void OnGreenChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(ColorHex));
    }

    partial void OnBlueChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(ColorHex));
    }

    partial void OnPositionChanged(Point value) => OnPropertyChanged(nameof(PositionText));
    partial void OnImageWidthChanged(int value) => OnPropertyChanged(nameof(DimensionsText));
    partial void OnImageHeightChanged(int value) => OnPropertyChanged(nameof(DimensionsText));
}

using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RawImage.Models;

/// <summary>
/// Holds information about the current image and pixel under cursor.
/// </summary>
public partial class ImageInfo : ObservableObject
{
    [ObservableProperty]
    private int _red;

    [ObservableProperty]
    private int _green;

    [ObservableProperty]
    private int _blue;

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    [ObservableProperty]
    private Point _position;

    [ObservableProperty]
    private bool _infoOnClick;

    public Color Color => Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);
}

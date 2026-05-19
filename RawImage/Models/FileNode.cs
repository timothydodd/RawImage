using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RawImage.Models;

/// <summary>
/// Represents a file or folder node in the file browser tree.
/// </summary>
public partial class FileNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private bool _isFolder;

    [ObservableProperty]
    private bool _hasChanged;

    [ObservableProperty]
    private bool _hasThumbnail;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isThumbnailLoading;

    private int _thumbnailLoadStarted;

    public FileNode? Parent { get; set; }

    public ObservableCollection<FileNode> Children { get; } = new();

    public IBrush Foreground => HasChanged ? Brushes.OrangeRed : Brushes.White;

    public FileNode(FileNode? parent = null)
    {
        Parent = parent;
        IsThumbnailLoading = true;
    }

    public Task LoadThumbnailAsync()
    {
        if (IsFolder || !HasThumbnail || Thumbnail != null)
            return Task.CompletedTask;

        if (Interlocked.Exchange(ref _thumbnailLoadStarted, 1) == 1)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            Bitmap? bmp = null;
            try
            {
                if (File.Exists(Path))
                {
                    using var stream = File.OpenRead(Path);
                    bmp = Bitmap.DecodeToWidth(stream, 100);
                }
            }
            catch
            {
                // Ignore thumbnail load errors
            }

            Dispatcher.UIThread.Post(() =>
            {
                Thumbnail = bmp;
                IsThumbnailLoading = false;
            });
        });
    }

    partial void OnHasChangedChanged(bool value)
    {
        OnPropertyChanged(nameof(Foreground));
    }

    public override string ToString() => Name;
}

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RawImage.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RawImage.ViewModels;

public partial class FolderItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<FolderItem> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;
}

public partial class ImageTile : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isLoading = true;
}

public partial class BrowseWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private FolderItem? _selectedFolder;

    [ObservableProperty]
    private ImageTile? _selectedImage;

    [ObservableProperty]
    private bool _showHistory;

    public ObservableCollection<FolderItem> Folders { get; } = new();
    public ObservableCollection<ImageTile> Images { get; } = new();
    public ObservableCollection<ImageTile> HistoryImages { get; } = new();

    public event Action<string>? ImageSelected;

    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".psd", ".xnb" };

    private bool _drivesLoaded;

    public BrowseWindowViewModel()
    {
        // Defer drive enumeration — it can hit slow/network drives and stalls
        // app launch when this view model is constructed eagerly.
    }

    public void EnsureDrivesLoaded()
    {
        if (_drivesLoaded) return;
        _drivesLoaded = true;
        _ = Task.Run(LoadDrivesAsync);
    }

    private async Task LoadDrivesAsync()
    {
        var entries = new List<FolderItem>();

        void AddSpecial(Environment.SpecialFolder folder, string label)
        {
            try
            {
                var path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    var item = new FolderItem { Name = label, Path = path };
                    LoadSubFolders(item);
                    entries.Add(item);
                }
            }
            catch { }
        }

        AddSpecial(Environment.SpecialFolder.UserProfile, "Home");
        AddSpecial(Environment.SpecialFolder.MyPictures, "Pictures");
        AddSpecial(Environment.SpecialFolder.Desktop, "Desktop");

        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                try
                {
                    var item = new FolderItem
                    {
                        Name = string.IsNullOrEmpty(drive.VolumeLabel)
                            ? $"{drive.Name.TrimEnd('\\')}"
                            : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})",
                        Path = drive.RootDirectory.FullName
                    };
                    LoadSubFolders(item);
                    entries.Add(item);
                }
                catch { }
            }
        }
        catch { }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Folders.Clear();
            foreach (var e in entries) Folders.Add(e);
        });
    }

    public void NavigateToFolder(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            CurrentPath = folderPath;
            _ = LoadImagesAsync(folderPath);
        }
    }

    private void LoadSubFolders(FolderItem parent)
    {
        parent.Children.Clear();

        try
        {
            var dirs = Directory.GetDirectories(parent.Path)
                .Where(d => !new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(d => Path.GetFileName(d))
                .Take(100); // Limit for performance

            foreach (var dir in dirs)
            {
                var child = new FolderItem
                {
                    Name = Path.GetFileName(dir),
                    Path = dir
                };

                // Check if has subdirectories (for expand arrow)
                try
                {
                    if (Directory.GetDirectories(dir).Any())
                    {
                        child.Children.Add(new FolderItem { Name = "Loading..." });
                    }
                }
                catch { }

                parent.Children.Add(child);
            }
        }
        catch { }
    }

    partial void OnSelectedFolderChanged(FolderItem? value)
    {
        if (value != null)
        {
            // Load subfolders if needed
            if (value.Children.Count == 1 && value.Children[0].Name == "Loading...")
            {
                LoadSubFolders(value);
            }

            CurrentPath = value.Path;
            _ = LoadImagesAsync(value.Path);
        }
    }

    private async Task LoadImagesAsync(string folderPath)
    {
        Images.Clear();

        try
        {
            var files = Directory.GetFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f))
                .Take(200); // Limit for performance

            foreach (var file in files)
            {
                var tile = new ImageTile
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    IsLoading = true
                };
                Images.Add(tile);
            }

            // Load thumbnails in background
            foreach (var tile in Images.ToList())
            {
                _ = LoadThumbnailAsync(tile);
            }
        }
        catch { }
    }

    private async Task LoadThumbnailAsync(ImageTile tile)
    {
        try
        {
            await Task.Run(async () =>
            {
                try
                {
                    using var stream = File.OpenRead(tile.Path);
                    var bitmap = new Bitmap(stream);

                    // Create thumbnail (max 120x120)
                    var maxSize = 120.0;
                    var scale = Math.Min(maxSize / bitmap.PixelSize.Width, maxSize / bitmap.PixelSize.Height);
                    scale = Math.Min(scale, 1.0); // Don't upscale

                    var thumbWidth = (int)(bitmap.PixelSize.Width * scale);
                    var thumbHeight = (int)(bitmap.PixelSize.Height * scale);

                    var thumbnail = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(thumbWidth, thumbHeight));

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        tile.Thumbnail = thumbnail;
                        tile.IsLoading = false;
                    });
                }
                catch
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        tile.IsLoading = false;
                    });
                }
            });
        }
        catch { }
    }

    [RelayCommand]
    public void OpenSelected(ImageTile? tile)
    {
        var target = tile ?? SelectedImage;
        if (target != null)
        {
            ImageSelected?.Invoke(target.Path);
        }
    }

    public void SelectImage(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            ImageSelected?.Invoke(path);
        }
    }

    [RelayCommand]
    public void ToggleHistory()
    {
        ShowHistory = !ShowHistory;
        if (ShowHistory)
        {
            LoadHistory();
        }
    }

    private void LoadHistory()
    {
        HistoryImages.Clear();

        foreach (var path in Services.SettingsService.Settings.History)
        {
            if (File.Exists(path))
            {
                var tile = new ImageTile
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    IsLoading = true
                };
                HistoryImages.Add(tile);
            }
        }

        // Load thumbnails
        foreach (var tile in HistoryImages.ToList())
        {
            _ = LoadThumbnailAsync(tile);
        }
    }

    [RelayCommand]
    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath))
            return;

        var parent = Directory.GetParent(CurrentPath);
        if (parent != null)
        {
            CurrentPath = parent.FullName;
            _ = LoadImagesAsync(CurrentPath);
        }
    }
}

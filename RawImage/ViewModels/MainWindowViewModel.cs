using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RawImage.Models;
using RawImage.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RawImage.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public event Action? ImageLoaded;
    [ObservableProperty]
    private Bitmap? _currentImage;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private bool _fitToWindow = true;

    [ObservableProperty]
    private bool _showInfoPanel = true;

    [ObservableProperty]
    private bool _showFileBrowser = true;

    [ObservableProperty]
    private bool _isFullScreen;

    [ObservableProperty]
    private bool _showBrowseOverlay;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private FilterMode _filterMode = FilterMode.Auto;

    [ObservableProperty]
    private BitmapInterpolationMode _interpolationMode = BitmapInterpolationMode.MediumQuality;

    [ObservableProperty]
    private bool _isImageLoading;

    [ObservableProperty]
    private bool _isGif;

    [ObservableProperty]
    private string? _gifPath;

    // Sub view models
    public FileBrowserViewModel FileBrowser { get; } = new();
    public ImageInfoPanelViewModel ImageInfo { get; } = new();
    public BrowseWindowViewModel BrowsePanel { get; }

    public MainWindowViewModel()
    {
        // Load settings
        ShowInfoPanel = SettingsService.Settings.ShowViewInfo;
        ShowFileBrowser = SettingsService.Settings.ShowDirectory;
        FilterMode = SettingsService.Settings.FilterMode;
        UpdateInterpolationMode();

        // Wire up file browser selection
        FileBrowser.SelectionChanged += OnFileSelected;

        // Initialize browse panel
        BrowsePanel = new BrowseWindowViewModel();
        BrowsePanel.ImageSelected += OnBrowseImageSelected;
    }

    private void OnBrowseImageSelected(string path)
    {
        ShowBrowseOverlay = false;
        LoadFromPath(path);
    }

    [RelayCommand]
    public void OpenBrowse()
    {
        BrowsePanel.EnsureDrivesLoaded();
        ShowBrowseOverlay = true;
    }

    [RelayCommand]
    public void OpenBrowseAtCurrentFolder()
    {
        BrowsePanel.EnsureDrivesLoaded();
        if (!string.IsNullOrEmpty(FileBrowser.CurrentPath))
        {
            BrowsePanel.NavigateToFolder(FileBrowser.CurrentPath);
        }
        ShowBrowseOverlay = true;
    }

    [RelayCommand]
    public void CloseBrowse()
    {
        ShowBrowseOverlay = false;
    }

    private void OnFileSelected(FileNode node)
    {
        if (!node.IsFolder)
        {
            _ = LoadImageAsync(node.Path);
        }
    }

    public void LoadFromPath(string path)
    {
        if (File.Exists(path))
        {
            // Kick off main image load first so it isn't queued behind file-browser work.
            _ = LoadImageAsync(path);
            // Defer browser init to the next dispatcher tick so the image decode starts
            // before we begin enumerating the directory.
            Dispatcher.UIThread.Post(() => FileBrowser.Initialize(path), DispatcherPriority.Background);
        }
        else if (Directory.Exists(path))
        {
            FileBrowser.Initialize(path);
        }
    }

    private async Task LoadImageAsync(string path)
    {
        if (path == CurrentFilePath)
            return;

        CurrentFilePath = path;
        CurrentFileName = Path.GetFileName(path);
        IsImageLoading = true;

        try
        {
            var result = await ImageLoaderService.LoadAsync(path);

            if (result.Error != null)
            {
                // Handle error - could show dialog
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsGif = result.IsGif;
                GifPath = result.GifPath;

                if (!result.IsGif)
                {
                    CurrentImage = result.Bitmap;
                }

                ImageWidth = result.Width;
                ImageHeight = result.Height;
                ImageInfo.ImageWidth = result.Width;
                ImageInfo.ImageHeight = result.Height;

                // Update interpolation based on image type
                if (FilterMode == FilterMode.Auto)
                {
                    InterpolationMode = result.UseNearestNeighbor
                        ? BitmapInterpolationMode.None
                        : BitmapInterpolationMode.HighQuality;
                }

                // Update history
                UpdateHistory(path);

                // Notify view to center/fit image
                ImageLoaded?.Invoke();
                IsImageLoading = false;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading image: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => IsImageLoading = false);
        }
    }

    private void UpdateHistory(string path)
    {
        var history = SettingsService.Settings.History;
        var index = history.IndexOf(path);
        if (index >= 0)
        {
            history.RemoveAt(index);
        }
        history.Insert(0, path);

        if (history.Count > 30)
        {
            history.RemoveRange(30, history.Count - 30);
        }
    }

    partial void OnShowBrowseOverlayChanged(bool value)
    {
        if (value) BrowsePanel.EnsureDrivesLoaded();
    }

    partial void OnShowInfoPanelChanged(bool value)
    {
        SettingsService.Settings.ShowViewInfo = value;
    }

    partial void OnShowFileBrowserChanged(bool value)
    {
        SettingsService.Settings.ShowDirectory = value;
    }

    partial void OnFilterModeChanged(FilterMode value)
    {
        SettingsService.Settings.FilterMode = value;
        UpdateInterpolationMode();
        OnPropertyChanged(nameof(IsFilterAuto));
        OnPropertyChanged(nameof(IsFilterHigh));
        OnPropertyChanged(nameof(IsFilterNear));
    }

    public bool IsFilterAuto => FilterMode == FilterMode.Auto;
    public bool IsFilterHigh => FilterMode == FilterMode.High;
    public bool IsFilterNear => FilterMode == FilterMode.Near;

    private void UpdateInterpolationMode()
    {
        InterpolationMode = FilterMode switch
        {
            FilterMode.High => BitmapInterpolationMode.HighQuality,
            FilterMode.Near => BitmapInterpolationMode.None,
            _ => BitmapInterpolationMode.MediumQuality
        };
    }

    [RelayCommand]
    public void ToggleFitToWindow()
    {
        FitToWindow = !FitToWindow;
    }

    [RelayCommand]
    public void ToggleInfoPanel()
    {
        ShowInfoPanel = !ShowInfoPanel;
    }

    [RelayCommand]
    public void ToggleFileBrowser()
    {
        ShowFileBrowser = !ShowFileBrowser;
    }

    [RelayCommand]
    public void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
    }

    [RelayCommand]
    public void SetFilterAuto()
    {
        FilterMode = FilterMode.Auto;
    }

    [RelayCommand]
    public void SetFilterHigh()
    {
        FilterMode = FilterMode.High;
    }

    [RelayCommand]
    public void SetFilterNear()
    {
        FilterMode = FilterMode.Near;
    }

    [RelayCommand]
    public void OpenInExplorer()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", $"/select, \"{CurrentFilePath}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                var dir = Path.GetDirectoryName(CurrentFilePath);
                Process.Start("xdg-open", dir ?? ".");
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"-R \"{CurrentFilePath}\"");
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    [RelayCommand]
    public void CopyToClipboard()
    {
        // Clipboard functionality would be implemented in the view
        // as it requires platform-specific handling
    }

    public bool IsWindows => OperatingSystem.IsWindows();

    public event Action? FileAssociationsRequested;

    [RelayCommand]
    public void OpenFileAssociations()
    {
        if (!OperatingSystem.IsWindows()) return;
        FileAssociationsRequested?.Invoke();
    }

    public void UpdatePixelInfo(int x, int y, byte r, byte g, byte b)
    {
        ImageInfo.Position = new Point(x, y);
        ImageInfo.Red = r;
        ImageInfo.Green = g;
        ImageInfo.Blue = b;
    }

    public void RequestFitToWindow(double viewportWidth, double viewportHeight, bool isNewImage = false)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0)
            return;

        // If fit to window is off and this is a new image, always use 100%
        if (!FitToWindow && isNewImage)
        {
            Zoom = 1.0;
            return;
        }

        // Only fit if FitToWindow is enabled
        if (FitToWindow && (viewportWidth < ImageWidth || viewportHeight < ImageHeight))
        {
            var scaleX = viewportWidth / ImageWidth;
            var scaleY = viewportHeight / ImageHeight;
            Zoom = Math.Min(scaleX, scaleY);
        }
        else if (isNewImage)
        {
            Zoom = 1.0;
        }
    }
}

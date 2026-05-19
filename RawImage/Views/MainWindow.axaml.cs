using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RawImage.Services;
using RawImage.ViewModels;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls.Primitives;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace RawImage.Views;

public partial class MainWindow : Window
{
    private Point? _lastDragPoint;
    private TranslateTransform? _imageTranslate;

    // Smooth zoom animation
    private double _targetZoom;
    private double _currentPanX;
    private double _currentPanY;
    private DispatcherTimer? _animationTimer;
    private const double AnimationSpeed = 0.25; // Interpolation factor (0-1, higher = faster)

    // Zoom anchor point (keeps this point stable during zoom)
    private Point _zoomAnchorScreen;  // Mouse position in viewport
    private Point _zoomAnchorImage;   // Corresponding point in image coordinates

    private TranslateTransform ImageTranslate => _imageTranslate ??=
        ((TransformGroup)ImageContainer.RenderTransform!).Children.OfType<TranslateTransform>().First();

    public MainWindow()
    {
        InitializeComponent();

        // Set up drag-to-pan and zoom
        ImageViewport.PointerPressed += OnPointerPressed;
        ImageViewport.PointerReleased += OnPointerReleased;
        ImageViewport.PointerMoved += OnPointerMoved;
        ImageViewport.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

        // Set up drag-drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        // Load saved window state
        Loaded += OnLoaded;
        Closing += OnClosing;

        // Make title bar draggable
        TitleBar.PointerPressed += OnTitleBarPointerPressed;

        // Update zoom on size change
        SizeChanged += OnSizeChanged;

        // Subscribe to image loaded event when DataContext is set
        DataContextChanged += OnDataContextChanged;

        // Update maximize/restore icon when window state changes
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            var state = (WindowState)e.NewValue!;
            MaximizeIcon.IsVisible = state != WindowState.Maximized;
            RestoreIcon.IsVisible = state == WindowState.Maximized;
            ToolTip.SetTip(MaximizeButton, state == WindowState.Maximized ? "Restore" : "Maximize");
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ImageLoaded += OnImageLoaded;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.FileAssociationsRequested += OnFileAssociationsRequested;
            UpdateMenuCheckmarks(vm);
            UpdateImageInterpolation(vm);
            UpdateFileBrowserColumnWidth(vm.ShowFileBrowser);
        }
    }

    private async void OnFileAssociationsRequested()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dialog = new FileAssociationsWindow();
        await dialog.ShowDialog(this);
    }

    private double _lastFileBrowserWidth = 200;

    private WindowState _previousWindowState = WindowState.Normal;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.PropertyName is nameof(vm.IsFilterAuto) or nameof(vm.IsFilterHigh) or nameof(vm.IsFilterNear))
            {
                UpdateMenuCheckmarks(vm);
            }

            if (e.PropertyName is nameof(vm.InterpolationMode))
            {
                UpdateImageInterpolation(vm);
            }

            if (e.PropertyName is nameof(vm.ShowFileBrowser))
            {
                UpdateFileBrowserColumnWidth(vm.ShowFileBrowser);
            }

            if (e.PropertyName is nameof(vm.IsFullScreen))
            {
                if (vm.IsFullScreen)
                {
                    _previousWindowState = WindowState;
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = _previousWindowState;
                }
            }
        }
    }

    private void UpdateFileBrowserColumnWidth(bool show)
    {
        var column = MainContent.ColumnDefinitions[0];
        if (show)
        {
            column.Width = new GridLength(_lastFileBrowserWidth);
            column.MinWidth = 100;
        }
        else
        {
            if (column.Width.Value > 0)
                _lastFileBrowserWidth = column.Width.Value;
            column.Width = new GridLength(0);
            column.MinWidth = 0;
        }
    }

    private void UpdateImageInterpolation(MainWindowViewModel vm)
    {
        RenderOptions.SetBitmapInterpolationMode(MainImage, vm.InterpolationMode);
        MainImage.InvalidateVisual();
    }

    private void UpdateMenuCheckmarks(MainWindowViewModel vm)
    {
        // Checkmark icon
        var checkIcon = Geometry.Parse("M9,16.17 L4.83,12 L3.41,13.41 L9,19 L21,7 L19.59,5.59 Z");
        var checkBrush = new SolidColorBrush(Color.Parse("#50fa7b"));

        // Get menu items from the flyout
        if (LogoButton.Flyout is MenuFlyout flyout)
        {
            foreach (var item in flyout.Items)
            {
                if (item is MenuItem menuItem)
                {
                    PathIcon? icon = null;
                    bool showCheck = menuItem.Name switch
                    {
                        "MenuFullScreen" => vm.IsFullScreen,
                        "MenuFilterAuto" => vm.IsFilterAuto,
                        "MenuFilterHigh" => vm.IsFilterHigh,
                        "MenuFilterNear" => vm.IsFilterNear,
                        _ => false
                    };

                    if (showCheck && menuItem.Name?.StartsWith("Menu") == true)
                    {
                        icon = new PathIcon { Data = checkIcon, Width = 12, Height = 12, Foreground = checkBrush };
                    }

                    menuItem.Icon = icon;
                }
            }
        }
    }

    private void OnImageLoaded()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // Always reset pan to center the image
        ResetPan();

        // Fit to window based on settings
        var viewportWidth = ImageViewport.Bounds.Width;
        var viewportHeight = ImageViewport.Bounds.Height;

        if (viewportWidth > 0 && viewportHeight > 0)
        {
            vm.RequestFitToWindow(viewportWidth, viewportHeight, isNewImage: true);
        }

        // Reset animation target to match
        _targetZoom = vm.Zoom;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Update checkmarks on load
        if (DataContext is MainWindowViewModel vm)
        {
            UpdateMenuCheckmarks(vm);
        }

        var settings = SettingsService.Settings;

        if (settings.WindowWidth > 100 && settings.WindowHeight > 100)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;

            // Check if position is on screen
            var screens = Screens;
            bool onScreen = false;
            foreach (var screen in screens.All)
            {
                if (screen.Bounds.Contains(new PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop)))
                {
                    onScreen = true;
                    break;
                }
            }

            if (onScreen)
            {
                Position = new PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
            }

            WindowState = settings.WindowState;
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        var settings = SettingsService.Settings;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        settings.WindowLeft = Position.X;
        settings.WindowTop = Position.Y;
        settings.WindowState = WindowState;

        // Save settings immediately on close
        SettingsService.Save();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't start drag if clicking on menu or window buttons
        if (e.Source is Control source)
        {
            Control? parent = source;
            while (parent != null)
            {
                if (parent is Menu or Button or MenuItem)
                    return;
                parent = parent.Parent as Control;
            }
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Double-click to maximize/restore
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                BeginMoveDrag(e);
            }
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RequestFitToWindow(ImageViewport.Bounds.Width, ImageViewport.Bounds.Height);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(ImageViewport);
        if (point.Properties.IsLeftButtonPressed)
        {
            _lastDragPoint = point.Position;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(ImageViewport);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Cursor = Cursor.Default;
        _lastDragPoint = null;
        e.Pointer.Capture(null);

        // Add to history on Ctrl+Click
        if (DataContext is MainWindowViewModel vm && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var imagePos = GetImageCoordinates(e.GetPosition(ImageViewport), vm.Zoom);
            UpdatePixelInfo(imagePos);
            vm.ImageInfo.AddToHistory();
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_lastDragPoint.HasValue)
        {
            var currentPoint = e.GetPosition(ImageViewport);
            var delta = currentPoint - _lastDragPoint.Value;
            _lastDragPoint = currentPoint;

            // Update pan directly (no animation for dragging)
            _currentPanX += delta.X;
            _currentPanY += delta.Y;

            ImageTranslate.X = _currentPanX;
            ImageTranslate.Y = _currentPanY;
        }
        else if (DataContext is MainWindowViewModel vm)
        {
            // Always update pixel info on hover
            var imagePos = GetImageCoordinates(e.GetPosition(ImageViewport), vm.Zoom);
            UpdatePixelInfo(imagePos);
        }
    }

    private Point GetImageCoordinates(Point viewportPos, double zoom)
    {
        // Convert viewport position to image coordinates
        // Account for: viewport center, pan offset, and zoom
        var viewportCenterX = ImageViewport.Bounds.Width / 2;
        var viewportCenterY = ImageViewport.Bounds.Height / 2;

        var imageX = (viewportPos.X - viewportCenterX - _currentPanX) / zoom;
        var imageY = (viewportPos.Y - viewportCenterY - _currentPanY) / zoom;

        // The image is centered, so we need to add half the image dimensions
        if (DataContext is MainWindowViewModel vm)
        {
            imageX += vm.ImageWidth / 2.0;
            imageY += vm.ImageHeight / 2.0;
        }

        return new Point(imageX, imageY);
    }

    private void UpdatePixelInfo(Point position)
    {
        if (DataContext is not MainWindowViewModel vm || vm.CurrentImage == null)
            return;

        int x = (int)position.X;
        int y = (int)position.Y;

        if (x < 0 || y < 0 || x >= vm.ImageWidth || y >= vm.ImageHeight)
            return;

        // Get pixel color from bitmap
        try
        {
            if (vm.CurrentImage is WriteableBitmap wb)
            {
                using var fb = wb.Lock();
                unsafe
                {
                    var ptr = (byte*)fb.Address;
                    int stride = fb.RowBytes;
                    int offset = y * stride + x * 4;

                    byte b = ptr[offset];
                    byte g = ptr[offset + 1];
                    byte r = ptr[offset + 2];

                    vm.UpdatePixelInfo(x, y, r, g, b);
                }
            }
        }
        catch
        {
            // Ignore pixel read errors
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // Get mouse position relative to the viewport
        var mousePos = e.GetPosition(ImageViewport);

        // Get viewport center
        var viewportCenterX = ImageViewport.Bounds.Width / 2;
        var viewportCenterY = ImageViewport.Bounds.Height / 2;

        // Use current zoom (not target) to calculate image point
        double currentZoom = vm.Zoom;

        // Calculate the point under the mouse in image coordinates
        // The image is centered, then scaled, then translated
        var imagePointX = (mousePos.X - viewportCenterX - _currentPanX) / currentZoom;
        var imagePointY = (mousePos.Y - viewportCenterY - _currentPanY) / currentZoom;

        // Store anchor point for animation
        _zoomAnchorScreen = mousePos;
        _zoomAnchorImage = new Point(imagePointX, imagePointY);

        // Calculate zoom change
        double mod = currentZoom > 0 ? currentZoom / 5.0 : 0.1;

        if (e.Delta.Y > 0)
        {
            _targetZoom = (_animationTimer?.IsEnabled == true ? _targetZoom : currentZoom) + mod;
        }
        else if (e.Delta.Y < 0)
        {
            _targetZoom = Math.Max(0.1, (_animationTimer?.IsEnabled == true ? _targetZoom : currentZoom) - mod);
        }

        // Start or continue animation
        StartAnimation(vm);

        e.Handled = true;
    }

    private void StartAnimation(MainWindowViewModel vm)
    {
        if (_animationTimer == null)
        {
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _animationTimer.Tick += (s, e) => UpdateAnimation();
        }

        if (!_animationTimer.IsEnabled)
        {
            _animationTimer.Start();
        }
    }

    private void UpdateAnimation()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            _animationTimer?.Stop();
            return;
        }

        // Get viewport center
        var viewportCenterX = ImageViewport.Bounds.Width / 2;
        var viewportCenterY = ImageViewport.Bounds.Height / 2;

        // Interpolate zoom only
        double zoomDiff = _targetZoom - vm.Zoom;

        // Check if close enough to target
        if (Math.Abs(zoomDiff) < 0.001)
        {
            vm.Zoom = _targetZoom;
            // Calculate final pan to keep anchor point under cursor
            _currentPanX = _zoomAnchorScreen.X - viewportCenterX - _zoomAnchorImage.X * vm.Zoom;
            _currentPanY = _zoomAnchorScreen.Y - viewportCenterY - _zoomAnchorImage.Y * vm.Zoom;
            ImageTranslate.X = _currentPanX;
            ImageTranslate.Y = _currentPanY;
            _animationTimer?.Stop();
            return;
        }

        // Smooth zoom interpolation
        vm.Zoom += zoomDiff * AnimationSpeed;

        // Calculate pan to keep anchor point under cursor at current zoom
        _currentPanX = _zoomAnchorScreen.X - viewportCenterX - _zoomAnchorImage.X * vm.Zoom;
        _currentPanY = _zoomAnchorScreen.Y - viewportCenterY - _zoomAnchorImage.Y * vm.Zoom;

        ImageTranslate.X = _currentPanX;
        ImageTranslate.Y = _currentPanY;
    }

    public void ResetPan()
    {
        _currentPanX = 0;
        _currentPanY = 0;
        ImageTranslate.X = 0;
        ImageTranslate.Y = 0;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer?.Contains(DataFormat.File) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || e.DataTransfer == null)
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files == null)
            return;

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            if (!string.IsNullOrEmpty(path))
            {
                vm.LoadFromPath(path);
                break;
            }
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow();
        await dialog.ShowDialog(this);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var browseWindow = new BrowseWindow
        {
            DataContext = new ViewModels.BrowseWindowViewModel()
        };

        var result = await browseWindow.ShowDialog<string?>(this);

        if (!string.IsNullOrEmpty(result))
        {
            vm.LoadFromPath(result);
        }
    }

    private void OnZoom1xClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _targetZoom = 1.0;
            vm.Zoom = 1.0;
            ResetPan();
        }
        ZoomDropdownButton.Flyout?.Hide();
    }

    private void OnZoom2xClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _targetZoom = 2.0;
            vm.Zoom = 2.0;
            ResetPan();
        }
        ZoomDropdownButton.Flyout?.Hide();
    }

    private void OnZoom4xClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _targetZoom = 4.0;
            vm.Zoom = 4.0;
            ResetPan();
        }
        ZoomDropdownButton.Flyout?.Hide();
    }

    // Menu click handlers
    private void OnToggleInfoPanelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ToggleInfoPanel();
    }

    private void OnToggleFileBrowserClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ToggleFileBrowser();
    }

    private void OnToggleFullScreenClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ToggleFullScreen();
    }

    private void OnFilterAutoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SetFilterAuto();
    }

    private void OnFilterHighClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SetFilterHigh();
    }

    private void OnFilterNearClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SetFilterNear();
    }

    private void OnBrowseTilePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // Get the ImageTile from the sender's DataContext
        if (sender is Border border && border.DataContext is ViewModels.ImageTile tile)
        {
            vm.BrowsePanel.SelectImage(tile.Path);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.CurrentImage is not WriteableBitmap bitmap)
            return;

        var fileTypes = new List<FilePickerFileType>
        {
            new("PNG Image") { Patterns = new[] { "*.png" } },
            new("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
            new("BMP Image") { Patterns = new[] { "*.bmp" } },
            new("GIF Image") { Patterns = new[] { "*.gif" } },
            new("WebP Image") { Patterns = new[] { "*.webp" } }
        };

        var suggestedName = Path.GetFileNameWithoutExtension(vm.CurrentFilePath);

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Image",
            SuggestedFileName = suggestedName,
            FileTypeChoices = fileTypes,
            DefaultExtension = "png"
        });

        if (file == null)
            return;

        try
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path).ToLowerInvariant();

            // Convert WriteableBitmap to ImageSharp Image
            using var fb = bitmap.Lock();
            SixLabors.ImageSharp.Image image;
            unsafe
            {
                image = ImageSharpImage.LoadPixelData<Bgra32>(
                    new ReadOnlySpan<byte>((byte*)fb.Address, fb.RowBytes * bitmap.PixelSize.Height),
                    bitmap.PixelSize.Width,
                    bitmap.PixelSize.Height);
            }

            // Save in the appropriate format
            await using var stream = await file.OpenWriteAsync();

            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    await image.SaveAsync(stream, new JpegEncoder { Quality = 95 });
                    break;
                case ".bmp":
                    await image.SaveAsync(stream, new BmpEncoder());
                    break;
                case ".gif":
                    await image.SaveAsync(stream, new GifEncoder());
                    break;
                case ".webp":
                    await image.SaveAsync(stream, new WebpEncoder { Quality = 95 });
                    break;
                case ".png":
                default:
                    await image.SaveAsync(stream, new PngEncoder());
                    break;
            }

            image.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
        }
    }
}

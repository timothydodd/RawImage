using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RawImage.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RawImage.ViewModels;

public partial class FileBrowserViewModel : ViewModelBase
{
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".psd", ".xnb" };

    [ObservableProperty]
    private string _currentPath = string.Empty;

    private int _lastIndex = -1;
    private FileNode? _currentNode;
    private DispatcherTimer? _searchTimer;
    private bool _isSearching;

    [ObservableProperty]
    private ObservableCollection<FileNode> _items = new();

    [ObservableProperty]
    private FileNode? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showHistory;

    // All file nodes (for search)
    private readonly ObservableCollection<FileNode> _allNodes = new();

    // Cached original items for restoring after search
    private ObservableCollection<FileNode>? _originalItems;

    public event Action<FileNode>? SelectionChanged;

    public FileBrowserViewModel()
    {
        _searchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _searchTimer.Tick += OnSearchTimerTick;
    }

    public void Initialize(string path)
    {
        var attr = File.GetAttributes(path);
        string dir = (attr & FileAttributes.Directory) == FileAttributes.Directory
            ? path
            : Path.GetDirectoryName(path) ?? string.Empty;

        if (!Directory.Exists(dir))
            return;

        CurrentPath = dir;
        _lastIndex = -1;
        Items.Clear();
        _allNodes.Clear();

        Task.Run(() => LoadDirectory(dir, path));
    }

    private void LoadDirectory(string dir, string originalPath)
    {
        var files = new List<string>();

        try
        {
            foreach (var ext in SupportedExtensions)
            {
                files.AddRange(Directory.GetFiles(dir, "*" + ext));
            }
        }
        catch
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            // Find initial selection
            string? fileName = null;
            if (File.Exists(originalPath))
            {
                fileName = Path.GetFileName(originalPath);
            }

            FileNode? selectedNode = null;

            // Add files only
            foreach (var f in files.OrderBy(x => x))
            {
                var node = new FileNode
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    IsFolder = false,
                    HasThumbnail = true
                };
                Items.Add(node);
                _allNodes.Add(node);

                if (fileName != null && string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedNode = node;
                }
            }

            if (selectedNode != null)
            {
                SetSelection(selectedNode);
            }

            // Load thumbnails on background — prioritize the currently-viewed file,
            // then nearby items, then the rest. Don't block the UI.
            _ = LoadThumbnailsAsync(selectedNode);
        });
    }

    private async Task LoadThumbnailsAsync(FileNode? priority)
    {
        // Yield so the UI can render the placeholders before we start hammering the disk.
        await Task.Delay(50);

        if (priority != null)
        {
            await priority.LoadThumbnailAsync();
        }

        var snapshot = Items.ToList();
        int pivot = priority != null ? snapshot.IndexOf(priority) : 0;
        if (pivot < 0) pivot = 0;

        // Expand outwards from the pivot so nearby items load first.
        int max = Math.Max(pivot, snapshot.Count - pivot - 1);
        for (int offset = 1; offset <= max; offset++)
        {
            int before = pivot - offset;
            int after = pivot + offset;
            if (after < snapshot.Count)
                await snapshot[after].LoadThumbnailAsync();
            if (before >= 0)
                await snapshot[before].LoadThumbnailAsync();
        }
    }

    public void SetSelection(FileNode node)
    {
        // Clear previous selection
        if (_currentNode != null)
        {
            _currentNode.IsSelected = false;
        }

        _currentNode = node;
        node.IsSelected = true;
        SelectedItem = node;
        _lastIndex = Items.IndexOf(node);
        SelectionChanged?.Invoke(node);
    }

    [RelayCommand]
    public void SelectNext()
    {
        if (_currentNode == null || Items.Count < 2)
            return;

        int startIndex = _lastIndex >= 0 ? _lastIndex : Items.IndexOf(_currentNode);
        int index = startIndex;

        do
        {
            index++;
            if (index >= Items.Count)
            {
                index = 0;
            }

            var node = Items[index];
            if (!node.IsFolder)
            {
                _lastIndex = index;
                SetSelection(node);
                return;
            }
        } while (index != startIndex);
    }

    [RelayCommand]
    public void SelectPrevious()
    {
        if (_currentNode == null || Items.Count < 2)
            return;

        int startIndex = _lastIndex >= 0 ? _lastIndex : Items.IndexOf(_currentNode);
        int index = startIndex;

        do
        {
            index--;
            if (index < 0)
            {
                index = Items.Count - 1;
            }

            var node = Items[index];
            if (!node.IsFolder)
            {
                _lastIndex = index;
                SetSelection(node);
                return;
            }
        } while (index != startIndex);
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchTimer?.Stop();

        if (string.IsNullOrWhiteSpace(value))
        {
            // Restore original items
            if (_originalItems != null)
            {
                Items = _originalItems;
                _originalItems = null;
            }
            return;
        }

        _searchTimer?.Start();
    }

    private void OnSearchTimerTick(object? sender, EventArgs e)
    {
        _searchTimer?.Stop();
        PerformSearch(SearchText);
    }

    private void PerformSearch(string searchText)
    {
        if (_isSearching)
            return;

        _isSearching = true;

        // Save original items
        if (_originalItems == null)
        {
            _originalItems = Items;
        }

        var keywords = searchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = _allNodes
            .Where(n => !n.IsFolder && keywords.All(k => n.Name.ToLower().Contains(k)))
            .OrderBy(n => n.Name)
            .ToList();

        Items = new ObservableCollection<FileNode>(results);
        _isSearching = false;
    }

}

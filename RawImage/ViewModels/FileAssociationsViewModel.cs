using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RawImage.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace RawImage.ViewModels;

public partial class ExtensionEntry : ObservableObject
{
    public string Extension { get; }

    [ObservableProperty]
    private bool _isAssociated;

    private bool _suppressApply;

    public ExtensionEntry(string extension, bool isAssociated)
    {
        Extension = extension;
        _suppressApply = true;
        IsAssociated = isAssociated;
        _suppressApply = false;
    }

    partial void OnIsAssociatedChanged(bool value)
    {
        if (_suppressApply) return;
        if (!OperatingSystem.IsWindows()) return;

        if (value)
            FileAssociationService.Register(Extension);
        else
            FileAssociationService.Unregister(Extension);
    }
}

public partial class FileAssociationsViewModel : ViewModelBase
{
    public ObservableCollection<ExtensionEntry> Extensions { get; } = new();

    public FileAssociationsViewModel()
    {
        if (!OperatingSystem.IsWindows()) return;

        foreach (var ext in FileAssociationService.SupportedExtensions)
        {
            Extensions.Add(new ExtensionEntry(ext, FileAssociationService.IsRegistered(ext)));
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var entry in Extensions)
            entry.IsAssociated = true;
    }

    [RelayCommand]
    public void ClearAll()
    {
        foreach (var entry in Extensions)
            entry.IsAssociated = false;
    }

    [RelayCommand]
    public void OpenDefaultAppsSettings()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore
        }
    }
}

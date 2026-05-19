using Avalonia.Controls;
using System.Collections.Generic;

namespace RawImage.Models;

/// <summary>
/// Filter mode for image rendering.
/// </summary>
public enum FilterMode
{
    Auto,
    High,
    Near
}

/// <summary>
/// Application settings that are persisted to JSON.
/// </summary>
public class EditorSettings
{
    public FilterMode FilterMode { get; set; } = FilterMode.Auto;
    public double WindowWidth { get; set; } = 1024;
    public double WindowHeight { get; set; } = 768;
    public double WindowTop { get; set; } = 100;
    public double WindowLeft { get; set; } = 100;
    public WindowState WindowState { get; set; } = WindowState.Normal;
    public bool ShowViewInfo { get; set; } = true;
    public bool ShowDirectory { get; set; } = true;
    public bool Lock { get; set; }
    public double ZoomValue { get; set; } = 1.0;
    public List<string> History { get; set; } = new();
    public List<string> Editors { get; set; } = new();
}

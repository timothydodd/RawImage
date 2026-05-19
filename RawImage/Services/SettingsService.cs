using RawImage.Models;
using System;
using System.IO;
using System.Text.Json;

namespace RawImage.Services;

/// <summary>
/// Handles loading and saving application settings to JSON.
/// </summary>
public static class SettingsService
{
    private const string SettingsFileName = "RawImage_settings.json";

    public static EditorSettings Settings { get; private set; } = new();

    private static string GetSettingsPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "RawImage");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        return Path.Combine(appFolder, SettingsFileName);
    }

    public static void Load()
    {
        var path = GetSettingsPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.EditorSettings);
                if (settings != null)
                {
                    Settings = settings;
                }
            }
            catch
            {
                // Use defaults on error
                Settings = new EditorSettings();
            }
        }
        else
        {
            Settings = new EditorSettings();
        }
    }

    public static void Save()
    {
        var path = GetSettingsPath();
        try
        {
            var json = JsonSerializer.Serialize(Settings, AppJsonContext.Default.EditorSettings);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RawImage.Services;

/// <summary>
/// Registers RawImage as a candidate handler for image files on Windows
/// without replacing the extension's default ProgID, so Explorer's
/// built-in thumbnail providers continue to render preview icons.
/// All writes go to HKCU (per-user) — no elevation required.
/// </summary>
public static partial class FileAssociationService
{
    private const string ProgId = "RawImage.ImageFile";
    private const string FriendlyName = "RawImage Viewer";

    // IThumbnailProvider shell extension interface GUID.
    private const string ThumbnailProviderGuid = "{e357fccd-a995-4576-b01f-234630154e96}";

    // Windows' built-in Photo Thumbnail Provider (WIC-backed) — renders
    // standard image formats (jpg/jpeg/png/bmp/gif/webp/tiff) from bytes,
    // so Explorer keeps showing live previews even when RawImage is the
    // default ProgID for an extension.
    private const string PhotoThumbnailProviderClsid = "{C7657C4A-9F68-40fa-A4DF-96BC08EB3551}";

    public static IReadOnlyList<string> SupportedExtensions { get; } = new[]
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".psd", ".xnb"
    };

    [SupportedOSPlatform("windows")]
    public static bool IsRegistered(string extension)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!IsKnownExtension(extension)) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{extension}\OpenWithProgids");
            return key?.GetValue(ProgId) != null;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static bool Register(string extension)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!IsKnownExtension(extension)) return false;
        if (!EnsureAppRegistration()) return false;

        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes")!;
            using var extKey = classes.CreateSubKey(extension);
            using var openWith = extKey.CreateSubKey("OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            NotifyShell();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FileAssociationService.Register({extension}) failed: {ex.Message}");
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static bool Unregister(string extension)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!IsKnownExtension(extension)) return false;

        try
        {
            using var openWith = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{extension}\OpenWithProgids", writable: true);
            if (openWith?.GetValue(ProgId) != null)
                openWith.DeleteValue(ProgId, throwOnMissingValue: false);
            NotifyShell();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FileAssociationService.Unregister({extension}) failed: {ex.Message}");
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static bool RegisterAll()
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!EnsureAppRegistration()) return false;

        var ok = true;
        foreach (var ext in SupportedExtensions)
            ok &= Register(ext);
        return ok;
    }

    [SupportedOSPlatform("windows")]
    public static bool UnregisterAll()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            if (classes == null) return true;

            classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);

            foreach (var ext in SupportedExtensions)
            {
                using var openWith = classes.OpenSubKey($@"{ext}\OpenWithProgids", writable: true);
                if (openWith?.GetValue(ProgId) != null)
                    openWith.DeleteValue(ProgId, throwOnMissingValue: false);
            }

            var exePath = GetExecutablePath();
            if (!string.IsNullOrEmpty(exePath))
            {
                classes.DeleteSubKeyTree(
                    $@"Applications\{Path.GetFileName(exePath)}",
                    throwOnMissingSubKey: false);
            }

            NotifyShell();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FileAssociationService.UnregisterAll failed: {ex.Message}");
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var cmd = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{ProgId}\shell\open\command");
            var command = cmd?.GetValue("") as string;
            if (string.IsNullOrEmpty(command)) return false;

            var currentExe = GetExecutablePath();
            return !string.IsNullOrEmpty(currentExe)
                && command.Contains(currentExe, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool EnsureAppRegistration()
    {
        var exePath = GetExecutablePath();
        if (string.IsNullOrEmpty(exePath)) return false;

        var exeFileName = Path.GetFileName(exePath);
        var openCommand = $"\"{exePath}\" \"%1\"";

        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes")!;

            using (var progKey = classes.CreateSubKey(ProgId))
            {
                progKey.SetValue("", FriendlyName);
                progKey.SetValue("FriendlyTypeName", FriendlyName);

                // Remove any DefaultIcon left by older builds so Explorer falls
                // back to the system's built-in image icons.
                progKey.DeleteSubKeyTree("DefaultIcon", throwOnMissingSubKey: false);

                // Bind the Windows WIC photo thumbnail provider so Explorer
                // renders live image previews when RawImage is the default
                // app for an extension.
                using (var thumbKey = progKey.CreateSubKey($@"ShellEx\{ThumbnailProviderGuid}"))
                {
                    thumbKey.SetValue("", PhotoThumbnailProviderClsid);
                }

                using var cmd = progKey.CreateSubKey(@"shell\open\command");
                cmd.SetValue("", openCommand);
            }

            using (var appKey = classes.CreateSubKey($@"Applications\{exeFileName}"))
            {
                appKey.SetValue("FriendlyAppName", "RawImage");
                using var cmd = appKey.CreateSubKey(@"shell\open\command");
                cmd.SetValue("", openCommand);
                using var supported = appKey.CreateSubKey("SupportedTypes");
                foreach (var ext in SupportedExtensions)
                    supported.SetValue(ext, string.Empty);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FileAssociationService.EnsureAppRegistration failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsKnownExtension(string ext) =>
        SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

    private static string? GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)) return path;
        return Process.GetCurrentProcess().MainModule?.FileName;
    }

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [LibraryImport("shell32.dll")]
    private static partial void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [SupportedOSPlatform("windows")]
    private static void NotifyShell()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }
}

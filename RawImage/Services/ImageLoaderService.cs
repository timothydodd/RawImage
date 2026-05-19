using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RawImage.Decoders.PsdDecoder;
using RawImage.Decoders.XnbDecoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RawImage.Services;

/// <summary>
/// Result of loading an image.
/// </summary>
public class ImageLoadResult
{
    public Bitmap? Bitmap { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsGif { get; set; }
    public string? GifPath { get; set; }
    public bool UseNearestNeighbor { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Service for loading various image formats.
/// </summary>
public static class ImageLoaderService
{
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".psd", ".xnb" };

    public static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(SupportedExtensions, ext) >= 0;
    }

    public static Task<ImageLoadResult> LoadAsync(string path)
    {
        // All decoders here are synchronous CPU/IO work. Run on the thread pool
        // so the caller's thread (usually the UI thread) stays free.
        return Task.Run(() =>
        {
            var result = new ImageLoadResult();

            if (!File.Exists(path))
            {
                result.Error = "File not found";
                return result;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                return ext switch
                {
                    ".xnb" => LoadXnb(path),
                    ".psd" => LoadPsd(path),
                    ".gif" => LoadGif(path),
                    _ => LoadStandardImage(path, ext),
                };
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        });
    }

    private static ImageLoadResult LoadXnb(string path)
    {
        var result = new ImageLoadResult { UseNearestNeighbor = true };

        using var stream = File.OpenRead(path);
        var imageData = XnbLoader.LoadXnb(stream);

        if (imageData == null)
        {
            result.Error = "Failed to load XNB file";
            return result;
        }

        result.Width = imageData.Width;
        result.Height = imageData.Height;
        result.Bitmap = CreateBitmapFromRgba(imageData.Pixels, imageData.Width, imageData.Height);

        return result;
    }

    private static ImageLoadResult LoadPsd(string path)
    {
        var result = new ImageLoadResult { UseNearestNeighbor = false };

        var psd = new PsdDecoder();
        var errorCode = psd.Load(path);

        if (errorCode != 0)
        {
            result.Error = $"Failed to load PSD file (error: {errorCode})";
            return result;
        }

        result.Width = psd.Width;
        result.Height = psd.Height;
        result.Bitmap = psd.GetBitmap();

        return result;
    }

    private static ImageLoadResult LoadGif(string path)
    {
        // Load GIF as standard image (first frame for animated GIFs)
        var result = new ImageLoadResult { UseNearestNeighbor = true };

        using var stream = File.OpenRead(path);
        result.Bitmap = LoadAsWriteableBitmap(stream);
        result.Width = result.Bitmap!.PixelSize.Width;
        result.Height = result.Bitmap.PixelSize.Height;

        return result;
    }

    private static ImageLoadResult LoadStandardImage(string path, string ext)
    {
        var result = new ImageLoadResult
        {
            UseNearestNeighbor = ext == ".png"
        };

        using var stream = File.OpenRead(path);
        result.Bitmap = LoadAsWriteableBitmap(stream);
        result.Width = result.Bitmap!.PixelSize.Width;
        result.Height = result.Bitmap.PixelSize.Height;

        return result;
    }

    private static unsafe WriteableBitmap LoadAsWriteableBitmap(Stream stream)
    {
        // Load image with ImageSharp to get pixel data
        using var image = Image.Load<Bgra32>(stream);

        var bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(image.Width, image.Height),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Unpremul);

        using var fb = bitmap.Lock();
        image.CopyPixelDataTo(new Span<byte>((void*)fb.Address, fb.RowBytes * image.Height));

        return bitmap;
    }

    private static Bitmap CreateBitmapFromRgba(byte[] pixels, int width, int height)
    {
        // Convert RGBA to BGRA for Avalonia WriteableBitmap
        var bgra = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // Pre-multiply alpha
            byte r = pixels[i];
            byte g = pixels[i + 1];
            byte b = pixels[i + 2];
            byte a = pixels[i + 3];

            bgra[i] = (byte)(b * a / 255);     // B
            bgra[i + 1] = (byte)(g * a / 255); // G
            bgra[i + 2] = (byte)(r * a / 255); // R
            bgra[i + 3] = a;                   // A
        }

        var bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul);

        using var fb = bitmap.Lock();
        unsafe
        {
            fixed (byte* src = bgra)
            {
                Buffer.MemoryCopy(src, (void*)fb.Address, bgra.Length, bgra.Length);
            }
        }

        return bitmap;
    }
}

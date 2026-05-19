using Avalonia;
using RawImage.Services;
using System;

namespace RawImage;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for single instance
        var singleInstance = new SingleInstanceService("RawImage");
        if (!singleInstance.TryAcquire(args))
        {
            // Another instance is running, args sent via named pipe
            return;
        }

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            singleInstance.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

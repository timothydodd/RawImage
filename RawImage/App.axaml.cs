using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RawImage.Services;
using RawImage.ViewModels;
using RawImage.Views;
using System;
using System.Collections.Generic;

namespace RawImage;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static MainWindowViewModel? MainViewModel { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Load settings
            SettingsService.Load();

            // Create main view model
            MainViewModel = new MainWindowViewModel();

            // Create and show main window
            MainWindow = new MainWindow
            {
                DataContext = MainViewModel
            };
            desktop.MainWindow = MainWindow;

            // Handle command line args or show browse overlay
            if (desktop.Args != null && desktop.Args.Length > 0)
            {
                MainViewModel.LoadFromPath(desktop.Args[0]);
            }
            else
            {
                // No image provided, show browse overlay after window opens
                MainWindow.Opened += OnMainWindowOpened;
            }

            // Subscribe to single instance events
            if (SingleInstanceService.Instance != null)
            {
                SingleInstanceService.Instance.ArgsReceived += OnSecondInstanceArgs;
            }

            desktop.ShutdownRequested += OnShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        if (MainWindow == null || MainViewModel == null)
            return;

        // Unsubscribe to prevent multiple calls
        MainWindow.Opened -= OnMainWindowOpened;

        // Show browse overlay
        MainViewModel.ShowBrowseOverlay = true;
    }

    private void OnSecondInstanceArgs(IList<string> args)
    {
        if (args.Count > 0 && MainViewModel != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                MainViewModel.LoadFromPath(args[0]);
                MainWindow?.Activate();
            });
        }
    }

    private void OnShutdown(object? sender, ShutdownRequestedEventArgs e)
    {
        // Save settings on exit
        SettingsService.Save();
    }
}

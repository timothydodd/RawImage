# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RawImage is a cross-platform desktop image viewer built with C# (.NET 10.0) and Avalonia UI. It supports standard image formats plus specialized decoders for Photoshop (.psd) and Xbox texture (.xnb) files.

## Build Commands

```bash
dotnet build                          # Build the project
dotnet build --configuration Release  # Release build
dotnet run                            # Run the application
dotnet publish -c Release             # Publish for distribution
```

## Architecture

**MVVM Pattern** using CommunityToolkit.Mvvm with Avalonia:
- `ViewModels/` - ObservableObject-based view models with `[ObservableProperty]` attributes
- `Views/` - Avalonia XAML views (.axaml) with code-behind
- `Services/` - Static services for image loading, settings persistence, and single-instance management
- `Decoders/` - Custom format decoders (PSD, XNB) that integrate with ImageLoaderService
- `Models/` - Data models for settings, file nodes, and image metadata

**Key Services:**
- `ImageLoaderService` - Async image loading with format detection and custom decoder routing
- `SettingsService` - JSON-based settings in `%APPDATA%\RawImage\`
- `SingleInstanceService` - Mutex + named pipes for single-instance enforcement with IPC

**Main Entry Flow:** `Program.cs` → single-instance check → `App.axaml.cs` → `MainWindow`

## Code Conventions

- Uses unsafe blocks for direct bitmap pixel manipulation (AllowUnsafeBlocks enabled)
- Partial method handlers for property changes: `partial void On<PropertyName>Changed()`
- Platform detection via `OperatingSystem.IsWindows()`, `.IsLinux()`, `.IsMacOS()`
- UI thread marshaling through Avalonia's Dispatcher for cross-thread operations

## Supported Image Formats

Standard: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.webp`
Custom decoders: `.psd` (Photoshop), `.xnb` (Xbox XNB texture)

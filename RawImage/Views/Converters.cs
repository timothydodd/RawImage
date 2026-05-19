using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace RawImage.Views;

public static class Converters
{
    public static readonly IValueConverter FileIconConverter = new FileIconValueConverter();
    public static readonly IValueConverter IconColorConverter = new IconColorValueConverter();
    public static readonly IValueConverter LockColorConverter = new LockColorValueConverter();
    public static readonly IValueConverter ToggleColorConverter = new ToggleColorValueConverter();
    public static readonly IValueConverter SelectionBackgroundConverter = new SelectionBackgroundValueConverter();
    public static readonly IValueConverter BoolToVisibilityConverter = new BoolToVisibilityValueConverter();
    public static readonly IValueConverter InverseBoolConverter = new InverseBoolValueConverter();
}

public class FileIconValueConverter : IValueConverter
{
    // Folder icon
    private static readonly Geometry FolderIcon = Geometry.Parse("M10,4 L4,4 C2.89,4 2,4.89 2,6 L2,18 C2,19.1 2.9,20 4,20 L20,20 C21.1,20 22,19.1 22,18 L22,8 C22,6.9 21.1,6 20,6 L12,6 Z");
    // Image file icon
    private static readonly Geometry FileIcon = Geometry.Parse("M21,19 L21,5 C21,3.9 20.1,3 19,3 L5,3 C3.9,3 3,3.9 3,5 L3,19 C3,20.1 3.9,21 5,21 L19,21 C20.1,21 21,20.1 21,19 Z M8.5,13.5 L11,16.5 L14.5,12 L19,18 L5,18 Z");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFolder)
        {
            return isFolder ? FolderIcon : FileIcon;
        }
        return FileIcon;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}

public class IconColorValueConverter : IValueConverter
{
    private static readonly SolidColorBrush FolderColor = new(Color.Parse("#ffb86c")); // Dracula Orange
    private static readonly SolidColorBrush FileColor = new(Color.Parse("#8be9fd"));   // Dracula Cyan

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFolder)
        {
            return isFolder ? FolderColor : FileColor;
        }
        return FileColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LockColorValueConverter : IValueConverter
{
    private static readonly SolidColorBrush LockedColor = new(Color.Parse("#bd93f9"));   // Dracula Purple
    private static readonly SolidColorBrush UnlockedColor = new(Color.Parse("#6272a4")); // Dracula Comment

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLocked)
        {
            return isLocked ? LockedColor : UnlockedColor;
        }
        return UnlockedColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ToggleColorValueConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveColor = new(Color.Parse("#50fa7b"));   // Dracula Green
    private static readonly SolidColorBrush InactiveColor = new(Color.Parse("#6272a4")); // Dracula Comment

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? ActiveColor : InactiveColor;
        }
        return InactiveColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SelectionBackgroundValueConverter : IValueConverter
{
    private static readonly SolidColorBrush SelectedColor = new(Color.Parse("#44475a"));   // Dracula Selection
    private static readonly SolidColorBrush UnselectedColor = new(Color.Parse("#00000000")); // Transparent

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            return isSelected ? SelectedColor : UnselectedColor;
        }
        return UnselectedColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

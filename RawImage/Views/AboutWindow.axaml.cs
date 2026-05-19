using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Reflection;

namespace RawImage.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetVersion()}";
    }

    private static string GetVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            // Strip build-metadata suffix (e.g. "1.0.0+abc1234").
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "unknown";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

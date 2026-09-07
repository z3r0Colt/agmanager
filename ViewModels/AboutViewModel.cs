using System.Diagnostics;
using System.Reflection;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string AppName    => "Davey Jones' Locker";
    public string AppVersion => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";
    public string AppTagline => "Pirate Game Manager";
    public string AppDescription =>
        "A free, open-source pirate game manager built with WPF and .NET 8. " +
        "Browse, download, manage, and track your library all in one place.";

    [RelayCommand]
    public void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/your-repo/davey-jones-locker")
                { UseShellExecute = true });
        }
        catch (Exception ex) { AppLogger.Warn($"[About] OpenGitHub failed: {ex.Message}"); }
    }

    [RelayCommand]
    public void OpenLicense()
    {
        try
        {
            var licPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSE");
            if (System.IO.File.Exists(licPath))
                Process.Start(new ProcessStartInfo(licPath) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo("https://opensource.org/licenses/MIT")
                    { UseShellExecute = true });
        }
        catch (Exception ex) { AppLogger.Warn($"[About] OpenLicense failed: {ex.Message}"); }
    }
}

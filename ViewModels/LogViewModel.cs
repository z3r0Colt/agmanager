using System.IO;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class LogViewModel : ObservableObject
{
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _autoRefresh = true;

    private System.Windows.Threading.DispatcherTimer? _timer;

    public void StartAutoRefresh()
    {
        _timer?.Stop();
        _timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => { if (AutoRefresh) Refresh(); };
        _timer.Start();
    }

    public void StopAutoRefresh() => _timer?.Stop();

    [RelayCommand]
    public void Refresh()
    {
        try
        {
            if (File.Exists(AppLogger.LogFilePath))
            {
                using var fs = new FileStream(AppLogger.LogFilePath,
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                LogText = sr.ReadToEnd();
            }
            else
            {
                LogText = "(No log file yet)";
            }
        }
        catch (Exception ex)
        {
            LogText = $"Error reading log: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearLog()
    {
        try
        {
            if (File.Exists(AppLogger.LogFilePath))
                File.WriteAllText(AppLogger.LogFilePath, "");
            LogText = "";
        }
        catch (Exception ex)
        {
            AppLogger.Error("[LogViewModel] ClearLog failed", ex);
        }
    }

    [RelayCommand]
    public void OpenLogFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppLogger.LogFilePath);
            if (dir != null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir)
                    { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[LogViewModel] OpenLogFolder failed: {ex.Message}");
        }
    }
}

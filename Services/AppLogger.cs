using System.IO;

namespace AgApp.Services;

public static class AppLogger
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgApp", "app.log");

    public static string LogFilePath => _logPath;

    public static void Info(string msg)  => Write("INFO",  msg);
    public static void Warn(string msg, Exception? ex = null)  => Write("WARN",  msg, ex);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

    private static void Write(string level, string msg, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}" +
                       (ex != null ? $" | {ex.GetType().Name}: {ex.Message}" : "");
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch { /* never throw from logger */ }
    }
}

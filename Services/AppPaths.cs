using System.IO;

namespace AgApp.Services;

public static class AppPaths
{
    // A separate data directory lets developers test without touching their real library.
    public static string DataDirectory =>
        Environment.GetEnvironmentVariable("AGMANAGER_DATA_DIRECTORY") is { Length: > 0 } directory
            ? Path.GetFullPath(directory)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgApp");
}

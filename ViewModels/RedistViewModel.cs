using System.Diagnostics;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public record RedistSection(string Title, string Glyph, RedistItem[] Items);

public partial class RedistItem : ObservableObject
{
    public required string Name        { get; init; }
    public required string Description { get; init; }
    public required string Url         { get; init; }
    public bool IsDirectDownload       { get; init; }

    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _actionLabel = "Install";
}

public partial class RedistViewModel : ObservableObject
{
    public RedistSection[] Sections { get; } = BuildSections();

    [RelayCommand]
    private async Task InstallAsync(RedistItem item)
    {
        if (item.IsBusy) return;

        if (!item.IsDirectDownload)
        {
            Process.Start(new ProcessStartInfo { FileName = item.Url, UseShellExecute = true });
            return;
        }

        item.IsBusy      = true;
        item.Progress    = 0;
        item.ActionLabel = "Downloading…";

        try
        {
            var ext  = Path.GetExtension(new Uri(item.Url).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".exe";
            var dest = Path.Combine(Path.GetTempPath(), $"AgApp_{Guid.NewGuid():N}{ext}");

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            using var resp = await http.GetAsync(item.Url,
                HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(dest);

            var buf = new byte[81920];
            long got = 0;
            int  n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                got += n;
                if (total > 0) item.Progress = got * 100.0 / total;
            }

            await dst.FlushAsync();
            dst.Close();

            item.ActionLabel = "Running installer…";
            Process.Start(new ProcessStartInfo { FileName = dest, UseShellExecute = true });
            await Task.Delay(2000);
            item.ActionLabel = "Install";
            item.Progress    = 0;
        }
        catch
        {
            item.ActionLabel = "Failed — retry";
            item.Progress    = 0;
            await Task.Delay(3000);
            item.ActionLabel = "Install";
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private static RedistSection[] BuildSections() =>
    [
        new("Visual C++ Redistributables", "",
        [
            new() { Name = "VC++ 2015–2022  x64",
                    Description = "Required by most modern games",
                    Url = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                    IsDirectDownload = true },
            new() { Name = "VC++ 2015–2022  x86",
                    Description = "32-bit games and components",
                    Url = "https://aka.ms/vs/17/release/vc_redist.x86.exe",
                    IsDirectDownload = true },
            new() { Name = "VC++ 2013  x64",
                    Description = "Older AAA titles",
                    Url = "https://aka.ms/highdpimfc2013x64enu",
                    IsDirectDownload = true },
            new() { Name = "VC++ 2013  x86",
                    Description = "Older 32-bit games",
                    Url = "https://aka.ms/highdpimfc2013x86enu",
                    IsDirectDownload = true },
            new() { Name = "VC++ 2012  x64",
                    Description = "Legacy game support",
                    Url = "https://go.microsoft.com/fwlink/?LinkId=271990",
                    IsDirectDownload = false },
            new() { Name = "VC++ 2010  x64",
                    Description = "Legacy game support",
                    Url = "https://go.microsoft.com/fwlink/?LinkId=201023",
                    IsDirectDownload = false },
        ]),

        new("DirectX", "",
        [
            new() { Name = "DirectX End-User Runtime",
                    Description = "D3D9, D3DX, XInput — needed by most pre-2015 games",
                    Url = "https://www.microsoft.com/en-us/download/details.aspx?id=35",
                    IsDirectDownload = false },
        ]),

        new("Microsoft .NET", "",
        [
            new() { Name = ".NET 8 Desktop Runtime  x64",
                    Description = "Required by newer game launchers",
                    Url = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe",
                    IsDirectDownload = true },
            new() { Name = ".NET 6 Desktop Runtime  x64",
                    Description = "2022–2023 era game tools",
                    Url = "https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe",
                    IsDirectDownload = true },
            new() { Name = ".NET Framework 4.8",
                    Description = "Classic .NET requirement",
                    Url = "https://go.microsoft.com/fwlink/?linkid=2088631",
                    IsDirectDownload = false },
            new() { Name = ".NET Framework 3.5",
                    Description = "Enable via Windows Optional Features",
                    Url = "ms-settings:optionalfeatures",
                    IsDirectDownload = false },
        ]),

        new("Java", "",
        [
            new() { Name = "Java 21  (JRE)",
                    Description = "Latest Java — modern Minecraft and mods",
                    Url = "https://adoptium.net/temurin/releases/",
                    IsDirectDownload = false },
            new() { Name = "Java 8  (JRE)",
                    Description = "Older Minecraft versions and Java games",
                    Url = "https://www.java.com/download/",
                    IsDirectDownload = false },
        ]),

        new("Other Runtimes", "",
        [
            new() { Name = "XNA Framework 4.0",
                    Description = "Required by XNA-based indie games",
                    Url = "https://www.microsoft.com/en-us/download/details.aspx?id=20914",
                    IsDirectDownload = false },
            new() { Name = "OpenAL",
                    Description = "3D spatial audio for older titles",
                    Url = "https://www.openal.org/downloads/",
                    IsDirectDownload = false },
            new() { Name = "PhysX System Software",
                    Description = "Hardware-accelerated physics (NVIDIA)",
                    Url = "https://www.nvidia.com/en-us/drivers/physx-system-software/",
                    IsDirectDownload = false },
        ]),
    ];
}

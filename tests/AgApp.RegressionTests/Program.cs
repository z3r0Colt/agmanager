using System.IO;
using System.IO.Compression;
using System.Text.Json;
using AgApp;
using AgApp.Models;
using AgApp.Services;
using AgApp.ViewModels;
using AgApp.Views;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "AgManagerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("AGMANAGER_DATA_DIRECTORY", Path.Combine(root, "AppData"));
        var app = new App();
        app.InitializeComponent();
        int passed = 0, failed = 0;
        void Test(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine($"PASS {name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex}"); }
        }
        SettingsService Service(string name) => new(Path.Combine(root, name, "settings.json"));
        SettingsViewModel ViewModel(SettingsService service) => new(service, new LibraryService(service))
        {
            DownloadFolder = Path.Combine(root, "Games with spaces"),
            SaveBackupRoot = Path.Combine(root, "Backups")
        };
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
        Test("Reproduce original first-save serialization failure", () =>
        {
            try { JsonSerializer.Serialize(new AppSettings()); }
            catch (ArgumentException) { return; }
            throw new Exception("Expected original NaN sentinel to fail with default serializer options");
        });
        Test("First save and reload preserve unset window position", () =>
        {
            var s = Service("first-save");
            s.Save();
            s.Load();
            Check(double.IsNaN(s.Current.WindowLeft) && double.IsNaN(s.Current.WindowTop), "Lost unset position");
        });
        Test("Existing numeric window coordinates remain compatible", () =>
        {
            var s = Service("existing");
            s.Current.WindowLeft = 52;
            s.Current.WindowTop = 100;
            s.Save();
            s.Load();
            Check(s.Current.WindowLeft == 52 && s.Current.WindowTop == 100, "Lost position");
        });
        Test("Choose new install folder, save and reload", () =>
        {
            var s = Service("valid-folder");
            var vm = ViewModel(s);
            vm.SaveCommand.Execute(null);
            Check(!vm.HasError && vm.StatusMessage.StartsWith("Settings saved"), vm.StatusMessage);
            var reload = Service("valid-folder");
            reload.Load();
            Check(reload.Current.DownloadFolder == vm.DownloadFolder, "Folder did not persist");
            Check(Directory.Exists(vm.DownloadFolder), "Folder not created");
        });
        foreach (var bad in new[] { "", "   ", "relative\\games", "C:\\bad*folder", "C:\\bad?folder", "C:\\bad\"folder" })
        {
            Test($"Reject invalid folder '{bad}' without replacing settings", () =>
            {
                var s = Service("invalid-" + Guid.NewGuid().ToString("N"));
                var old = s.Current;
                var vm = ViewModel(s);
                vm.DownloadFolder = bad;
                vm.SaveCommand.Execute(null);
                Check(vm.HasError && vm.StatusMessage.Length > 0, "Missing error feedback");
                Check(ReferenceEquals(old, s.Current), "Active settings changed after failed save");
            });
        }
        Test("A file cannot be used as an installation folder", () =>
        {
            var file = Path.Combine(root, "not-a-folder");
            File.WriteAllText(file, "preserve");
            var s = Service("file-folder");
            var vm = ViewModel(s);
            vm.DownloadFolder = file;
            vm.SaveCommand.Execute(null);
            Check(vm.HasError, "Existing file accepted as folder");
            Check(File.ReadAllText(file) == "preserve", "Existing file changed");
        });
        Test("Failed settings write preserves active and persisted settings", () =>
        {
            var path = Path.Combine(root, "locked", "settings.json");
            var s = new SettingsService(path);
            s.Save();
            var original = File.ReadAllText(path);
            var old = s.Current;
            using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var vm = ViewModel(s);
                vm.SaveCommand.Execute(null);
                Check(vm.HasError, "Write to locked file should fail");
                Check(ReferenceEquals(old, s.Current), "Active settings changed after write failure");
            }
            Check(File.ReadAllText(path) == original, "Persisted settings were damaged");
            Check(!File.Exists(path + ".tmp"), "Temporary settings file leaked");
        });
        Test("Invalid backup folder prevents partial install-folder save", () =>
        {
            var s = Service("backup-invalid");
            var old = s.Current.DownloadFolder;
            var vm = ViewModel(s);
            vm.SaveBackupRoot = "relative";
            vm.SaveCommand.Execute(null);
            Check(vm.HasError && s.Current.DownloadFolder == old, "Partially applied invalid settings");
        });
        Test("Writable folder probe leaves no temporary files", () =>
        {
            var path = SettingsService.PrepareWritableFolder(Path.Combine(root, "probe"));
            Check(Directory.GetFiles(path).Length == 0, "Probe file leaked");
        });
        Test("All page resource dictionaries load on the STA thread", () =>
        {
            foreach (var asset in new[] { "appico.ico", "Assets/Brand/logo-mark.png" })
            {
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                    new Uri($"pack://application:,,,/DaveyJonesLocker;component/{asset}"));
                Check(frame.PixelWidth > 0 && frame.PixelHeight > 0, $"Embedded brand asset did not decode: {asset}");
            }
            _ = new SettingsPage();
            _ = new LibraryPage();
            _ = new DownloadsPage();
            _ = new BrowsePage();
            _ = new AboutPage();
            _ = new RedistPage();
        });
        Test("Unavailable download folder becomes a failed job, not an exception", () =>
        {
            var s = Service("download-invalid");
            s.Current.DownloadFolder = "relative";
            var library = new LibraryService(s);
            var manager = new DownloadManager(s, new ExtractionService(), library, new MetadataService(s, library));
            var job = new DownloadJob { GameTitle = "Folder regression" };
            manager.StartDownloadAsync(job).GetAwaiter().GetResult();
            Check(job.Status == DownloadStatus.Failed && job.ErrorMessage.Length > 0, "Missing failed-job feedback");
            Check(manager.Jobs.Contains(job), "Failed job not visible in queue");
        });
        Test("Cancelled queued job is handled without releasing an unacquired gate", () =>
        {
            var s = Service("download-cancel");
            s.Current.DownloadFolder = Path.Combine(root, "cancelled-downloads");
            var library = new LibraryService(s);
            var manager = new DownloadManager(s, new ExtractionService(), library, new MetadataService(s, library));
            var job = new DownloadJob { GameTitle = "Cancellation regression" };
            job.CancellationSource.Cancel();
            manager.StartDownloadAsync(job).GetAwaiter().GetResult();
            Check(job.Status == DownloadStatus.Cancelled, "Queued cancellation not handled");
        });
        foreach (var delete in new[] { false, true })
        {
            Test($"Extraction respects delete-archive preference ({delete})", () =>
            {
                var archivePath = Path.Combine(root, $"archive-{delete}.zip");
                using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("content/readme.txt").Open()))
                    writer.Write("regression content");
                var folder = new ExtractionService().ExtractAndDeleteAsync(archivePath,
                    Path.Combine(root, "extracted"), deleteArchive: delete).GetAwaiter().GetResult();
                Check(File.ReadAllText(Path.Combine(folder, "content", "readme.txt")) == "regression content", "Extraction failed");
                Check(File.Exists(archivePath) != delete, "Archive preference was ignored");
            });
        }
        Test("Main window loads with the branded taskbar icon", () =>
        {
            var s = Service("window-resources");
            var library = new LibraryService(s);
            var metadata = new MetadataService(s, library);
            var manager = new DownloadManager(s, new ExtractionService(), library, metadata);
            var session = new GameSessionService(library);
            var backup = new SaveBackupService(library, s);
            var updates = new UpdateCheckService(library, s);
            var vm = new MainViewModel(new BrowseViewModel(), new DownloadsViewModel(manager),
                new LibraryViewModel(library, metadata, session, backup, updates), ViewModel(s),
                new RedistViewModel(), new AboutViewModel(), session);
            var window = new MainWindow(vm, s, manager);
            Check(window.Icon != null && window.Icon.Width > 0, "Missing window icon");
        });
        Console.WriteLine($"{passed} passed; {failed} failed. Test files: {root}");
        return failed == 0 ? 0 : 1;
    }
}

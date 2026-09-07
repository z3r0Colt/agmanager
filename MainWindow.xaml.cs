using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using AgApp.Helpers;
using AgApp.Models;
using AgApp.Services;
using AgApp.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;

namespace AgApp;

public partial class MainWindow : Window
{
    private TaskbarIcon? _trayIcon;
    private readonly SettingsService _settings;

    // ── Taskbar-aware maximize ────────────────────────────────────────────────
    // WPF's WindowStyle=None windows ignore the working area when maximized and
    // cover the taskbar. Handling WM_GETMINMAXINFO fixes it for every monitor.

    private const int WM_GETMINMAXINFO      = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT  { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT   { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition,
                     ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
    [DllImport("user32.dll")] private static extern bool   GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi  = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmon != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                GetMonitorInfo(hmon, ref mi);
                // ptMaxPosition is relative to the monitor origin
                mmi.ptMaxPosition.X = mi.rcWork.left   - mi.rcMonitor.left;
                mmi.ptMaxPosition.Y = mi.rcWork.top    - mi.rcMonitor.top;
                mmi.ptMaxSize.X     = mi.rcWork.right  - mi.rcWork.left;
                mmi.ptMaxSize.Y     = mi.rcWork.bottom - mi.rcWork.top;
            }
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ─────────────────────────────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DwmHelper.Apply(this);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    public MainWindow(MainViewModel viewModel, SettingsService settings, DownloadManager downloadManager)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;

        RestoreWindowState(settings.Current);

        Loaded += (_, _) => SetupTrayIcon(settings);
        Closed += (_, _) =>
        {
            _trayIcon?.Dispose();
            SaveWindowState(settings);
        };

        downloadManager.DownloadStarted += job =>
            Dispatcher.Invoke(() => ShowDownloadToast(job.GameTitle));

        downloadManager.ExtractionCompleted += (job, folder) =>
        {
            if (_settings.Current.NotifyOnExtractionComplete)
                Dispatcher.Invoke(() => ShowBalloonTip("Extraction Complete", $"{job.GameTitle} is ready to play."));
        };

        // Keyboard shortcuts
        PreviewKeyDown += OnWindowKeyDown;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowBalloonTip("Davey Jones' Locker",
                "Still running in the system tray. Double-click to restore.", BalloonIcon.Info);
            return;
        }
        base.OnClosing(e);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // Ctrl+, → Settings
            if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
            {
                vm.Navigate("Settings");
                e.Handled = true;
                return;
            }

            // Ctrl+F → focus library search
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (vm.CurrentPage == "Library")
                {
                    var searchBox = FindName("SearchBox") as System.Windows.Controls.TextBox
                        ?? FindVisualChild<System.Windows.Controls.TextBox>(this, "SearchBox");
                    if (searchBox != null) { searchBox.Focus(); searchBox.SelectAll(); }
                    e.Handled = true;
                }
                return;
            }

            // Escape → deselect game in Library
            if (e.Key == Key.Escape && vm.CurrentPage == "Library")
            {
                vm.Library.SelectedGame = null;
                e.Handled = true;
            }
        }
        catch (Exception ex) { AppLogger.Warn("[MainWindow] KeyDown handler failed", ex); }
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent, string? name = null)
        where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t && (name == null || (child is System.Windows.FrameworkElement fe && fe.Name == name)))
                return t;
            var result = FindVisualChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void RestoreWindowState(AppSettings s)
    {
        if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
        {
            Left = s.WindowLeft;
            Top  = s.WindowTop;
        }
        Width  = s.WindowWidth;
        Height = s.WindowHeight;
        if (s.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SaveWindowState(SettingsService settings)
    {
        var s = settings.Current;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowLeft   = Left;
            s.WindowTop    = Top;
            s.WindowWidth  = Width;
            s.WindowHeight = Height;
        }
        settings.Save();
    }

    private void SetupTrayIcon(SettingsService settings)
    {
        _trayIcon = new TaskbarIcon { ToolTipText = "Davey Jones' Locker — Pirate Game Manager" };

        var menu     = new System.Windows.Controls.ContextMenu();
        var showItem = new System.Windows.Controls.MenuItem { Header = "Show" };
        showItem.Click += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => { _trayIcon?.Dispose(); Application.Current.Shutdown(); };
        menu.Items.Add(showItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exitItem);
        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
    }

    public void ShowBalloonTip(string title, string message) =>
        _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);

    private void ShowDownloadToast(string gameTitle)
    {
        var icon = new System.Windows.Controls.TextBlock
        {
            Text = "⬇", FontSize = 36, TextAlignment = TextAlignment.Center,
            Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10)
        };
        var header = new System.Windows.Controls.TextBlock
        {
            Text = "Download & Install Started!", Foreground = Brushes.White,
            FontSize = 20, FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8)
        };
        var sub = new System.Windows.Controls.TextBlock
        {
            Text = gameTitle,
            Foreground = new SolidColorBrush(Color.FromRgb(0xc7, 0xd2, 0xfe)),
            FontSize = 14, TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340
        };
        var hint = new System.Windows.Controls.TextBlock
        {
            Text = "Check the Downloads tab for progress",
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x9b, 0xd6)),
            FontSize = 11, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0)
        };

        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(icon);
        stack.Children.Add(header);
        stack.Children.Add(sub);
        stack.Children.Add(hint);

        var bg = new LinearGradientBrush();
        bg.StartPoint = new Point(0, 0);
        bg.EndPoint   = new Point(0, 1);
        bg.GradientStops.Add(new GradientStop(Color.FromRgb(0x5b, 0x58, 0xf5), 0));
        bg.GradientStops.Add(new GradientStop(Color.FromRgb(0x29, 0x25, 0x7a), 1));

        var border = new System.Windows.Controls.Border
        {
            Background = bg, CornerRadius = new CornerRadius(18),
            Padding = new Thickness(44, 28, 44, 28),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x81, 0x87, 0xf9)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x63, 0x66, 0xf1),
                BlurRadius = 70, Opacity = 0.65, ShadowDepth = 0
            },
            Child = stack
        };

        var toast = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent, IsHitTestVisible = false,
            ShowInTaskbar = false, Content = border,
            SizeToContent = SizeToContent.WidthAndHeight,
            Owner = this, Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Opacity = 0
        };

        toast.Loaded += (_, _) =>
        {
            var center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
            toast.Left = center.X - toast.ActualWidth  / 2;
            toast.Top  = center.Y - toast.ActualHeight / 2;
            toast.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        };

        toast.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280));
            fadeOut.Completed += (_, _) => toast.Close();
            toast.BeginAnimation(OpacityProperty, fadeOut);
        };
        timer.Start();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Current.MinimizeToTray)
        {
            Hide();
            _trayIcon?.ShowBalloonTip("Davey Jones' Locker", "Running in tray. Double-click to restore.", BalloonIcon.Info);
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void MaxBtn_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        // Title bar X button always quits, regardless of CloseToTray
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }
}

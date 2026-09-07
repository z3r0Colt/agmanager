using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AgApp.Helpers;

internal static class DwmHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // Windows 11 22H2+

    // DWMSBT values
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static Version Win11_22H2 { get; } = new(10, 0, 22621);
    private static Version Win11_21H2 { get; } = new(10, 0, 22000);

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        // Immersive dark mode — darkens the system window border on Win10/11
        int dark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE20H1, ref dark, sizeof(int));

        // Mica backdrop on Windows 11 22H2+
        // Note: with WindowChrome + opaque WPF background the mica tints only the
        // DWM-owned non-client border; the effect is subtle but keeps the border
        // consistent with Shell chrome and avoids the white flash on window open.
        if (Environment.OSVersion.Version >= Win11_22H2)
        {
            int backdrop = DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
    }
}

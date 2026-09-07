// GamepadService stub
// Uses XInput via P/Invoke to poll the first connected controller.
// Currently only detects connect/disconnect and reads Start button.
// Extend by reading XInputGetState's XINPUT_GAMEPAD fields for thumbsticks/buttons.
//
// To enable gamepad navigation, wire the events to your ViewModel/Window.
//
// Dependency: Windows XInput (xinput1_4.dll) — ships with Windows 8+.

// Suppress warnings from intentionally-commented stub code
#pragma warning disable CS0067, CS0169, CS0649

using System.Runtime.InteropServices;

namespace AgApp.Services;

public class GamepadService : IDisposable
{
    // XInput structures
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION { public ushort wLeftMotorSpeed; public ushort wRightMotorSpeed; }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte   bLeftTrigger, bRightTrigger;
        public short  sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }

    // Button masks
    private const ushort XINPUT_GAMEPAD_START = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK  = 0x0020;
    private const int    ERROR_SUCCESS         = 0;

    /* --- Uncomment to activate XInput polling ---
    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int dwUserIndex, ref XINPUT_STATE pState);
    */

    public event Action? ControllerConnected;
    public event Action? ControllerDisconnected;
    /// <summary>Raised when the Start button is pressed (single-press, not held).</summary>
    public event Action? StartPressed;

    private System.Threading.Timer? _pollTimer;
    private bool _wasConnected;
    private ushort _lastButtons;
    private bool _disposed;

    /// <summary>Starts polling the first XInput controller at the given interval (default 100 ms).</summary>
    public void Start(int intervalMs = 100)
    {
        // Stub: polling is commented out — wire XInputGetState above to enable.
        AppLogger.Info("[Gamepad] Polling stub started (XInput calls disabled).");

        /* --- Uncomment to activate ---
        _pollTimer = new System.Threading.Timer(_ => Poll(), null, 0, intervalMs);
        */
    }

    private void Poll()
    {
        /* --- Uncomment to activate ---
        var state = new XINPUT_STATE();
        int result = XInputGetState(0, ref state);
        bool connected = result == ERROR_SUCCESS;

        if (connected != _wasConnected)
        {
            _wasConnected = connected;
            if (connected) ControllerConnected?.Invoke();
            else           ControllerDisconnected?.Invoke();
        }

        if (!connected) return;

        var buttons = state.Gamepad.wButtons;
        var pressed = (ushort)(buttons & ~_lastButtons); // newly pressed this tick
        _lastButtons = buttons;

        if ((pressed & XINPUT_GAMEPAD_START) != 0) StartPressed?.Invoke();
        */
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer?.Dispose();
        AppLogger.Info("[Gamepad] Service disposed.");
    }
}

#pragma warning restore CS0067, CS0169, CS0649

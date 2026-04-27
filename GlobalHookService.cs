using System.Runtime.InteropServices;

namespace Agent1;

internal sealed class GlobalHookService : IDisposable
{
    private NativeMethods.HookProc? _mouseProc;
    private NativeMethods.HookProc? _keyboardProc;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private bool _disposed;

    public event Action<ScreenPoint>? PrimaryMouseDown;
    public event Action? EscapePressed;

    public void Start()
    {
        if (_mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero)
        {
            return;
        }

        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;

        var moduleHandle = NativeMethods.GetModuleHandle(null);

        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);

        if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
        {
            Dispose();
            throw new InvalidOperationException("Не удалось установить глобальные Win32 hooks.");
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsPrimaryMouseDownMessage(wParam))
        {
            try
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                PrimaryMouseDown?.Invoke(new ScreenPoint(data.pt.x, data.pt.y));
            }
            catch
            {
                // Hook callbacks must not throw across the unmanaged boundary.
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            try
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (data.vkCode == NativeMethods.VK_ESCAPE)
                {
                    EscapePressed?.Invoke();
                }
            }
            catch
            {
                // Hook callbacks must not throw across the unmanaged boundary.
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private static bool IsPrimaryMouseDownMessage(IntPtr message)
    {
        if (message == NativeMethods.WM_LBUTTONDOWN)
        {
            return true;
        }

        return NativeMethods.GetSystemMetrics(NativeMethods.SM_SWAPBUTTON) != 0
            && message == NativeMethods.WM_RBUTTONDOWN;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _disposed = true;
    }
}

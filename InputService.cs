using System.Runtime.InteropServices;

namespace Agent1;

internal sealed class InputService
{
    public void SendF5()
    {
        var inputs = new[]
        {
            NativeMethods.INPUT.Keyboard(NativeMethods.VK_F5, 0),
            NativeMethods.INPUT.Keyboard(NativeMethods.VK_F5, NativeMethods.KEYEVENTF_KEYUP)
        };

        Send(inputs);
    }

    public void Click(ScreenPoint point)
    {
        var (normalizedX, normalizedY) = NormalizeToVirtualDesktop(point);
        var inputs = new[]
        {
            NativeMethods.INPUT.MouseMove(normalizedX, normalizedY),
            NativeMethods.INPUT.MouseButton(NativeMethods.MOUSEEVENTF_LEFTDOWN),
            NativeMethods.INPUT.MouseButton(NativeMethods.MOUSEEVENTF_LEFTUP)
        };

        Send(inputs);
    }

    private static (int X, int Y) NormalizeToVirtualDesktop(ScreenPoint point)
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        var x = (int)Math.Round((point.X - left) * 65535.0 / Math.Max(1, width - 1));
        var y = (int)Math.Round((point.Y - top) * 65535.0 / Math.Max(1, height - 1));
        return (x, y);
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != (uint)inputs.Length)
        {
            throw new InvalidOperationException("SendInput не отправил все события ввода.");
        }
    }
}

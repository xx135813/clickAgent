using System.Runtime.InteropServices;

namespace Agent1;

internal sealed class ScreenCaptureService
{
    public byte[] CaptureRgb24(ScreenRectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "ROI должен иметь положительные размеры.");
        }

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("GetDC вернул пустой handle.");
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr oldObject = IntPtr.Zero;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateCompatibleDC вернул пустой handle.");
            }

            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateCompatibleBitmap вернул пустой handle.");
            }

            oldObject = NativeMethods.SelectObject(memoryDc, bitmap);
            if (oldObject == IntPtr.Zero)
            {
                throw new InvalidOperationException("SelectObject не смог выбрать bitmap в memory DC.");
            }

            if (!NativeMethods.BitBlt(
                    memoryDc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDc,
                    region.X,
                    region.Y,
                    NativeMethods.SRCCOPY))
            {
                throw new InvalidOperationException("BitBlt не смог снять ROI.");
            }

            NativeMethods.SelectObject(memoryDc, oldObject);
            oldObject = IntPtr.Zero;

            var pixels32 = new byte[region.Width * region.Height * 4];
            var info = NativeMethods.BITMAPINFO.CreateTopDown32(region.Width, region.Height);
            var copied = NativeMethods.GetDIBits(
                memoryDc,
                bitmap,
                0,
                (uint)region.Height,
                pixels32,
                ref info,
                NativeMethods.DIB_RGB_COLORS);

            if (copied != region.Height)
            {
                throw new InvalidOperationException("GetDIBits не вернул полный набор строк ROI.");
            }

            var rgb = new byte[region.Width * region.Height * 3];
            for (var src = 0; src < pixels32.Length; src += 4)
            {
                var dst = src / 4 * 3;
                rgb[dst] = pixels32[src + 2];
                rgb[dst + 1] = pixels32[src + 1];
                rgb[dst + 2] = pixels32[src];
            }

            return rgb;
        }
        finally
        {
            if (oldObject != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, oldObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}

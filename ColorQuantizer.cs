namespace Agent1;

internal static class ColorQuantizer
{
    public static HashSet<int> GetUniqueQuantizedColors(byte[] rgb24, int step, CancellationToken token)
    {
        if (step <= 0 || step > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        if (rgb24.Length % 3 != 0)
        {
            throw new ArgumentException("RGB24 buffer length must be divisible by 3.", nameof(rgb24));
        }

        var colors = new HashSet<int>();

        for (var i = 0; i < rgb24.Length; i += 3)
        {
            if ((i & 0x3FFF) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var r = Quantize(rgb24[i], step);
            var g = Quantize(rgb24[i + 1], step);
            var b = Quantize(rgb24[i + 2], step);
            colors.Add((r << 16) | (g << 8) | b);
        }

        return colors;
    }

    private static int Quantize(byte value, int step)
    {
        return value / step * step;
    }
}

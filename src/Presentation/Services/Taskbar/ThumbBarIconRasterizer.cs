namespace Rok.Services.Taskbar;

/// <summary>Draws the thumbnail toolbar glyphs into premultiplied BGRA pixel buffers.</summary>
public static class ThumbBarIconRasterizer
{
    private const int Samples = 4;
    private const double Margin = 0.15;
    private const double BarWidth = 0.18;
    private const double SkipBarWidth = 0.15;
    private const double SkipGap = 0.05;

    /// <summary>Renders a glyph as a top-down buffer of <c>size * size</c> premultiplied 0xAARRGGBB pixels.</summary>
    public static uint[] Render(ThumbBarGlyph glyph, int size, uint argb)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var pixels = new uint[size * size];
        var alpha = argb >> 24;
        var red = (argb >> 16) & 0xFF;
        var green = (argb >> 8) & 0xFF;
        var blue = argb & 0xFF;
        var span = 1.0 - (2 * Margin);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var covered = 0;

                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var px = (x + ((sx + 0.5) / Samples)) / size;
                        var py = (y + ((sy + 0.5) / Samples)) / size;
                        var u = (px - Margin) / span;
                        var v = (py - Margin) / span;

                        if (Contains(glyph, u, v))
                            covered++;
                    }
                }

                if (covered == 0)
                    continue;

                var a = (uint)Math.Round(alpha * covered / (double)(Samples * Samples));
                pixels[(y * size) + x] = (a << 24)
                    | (Premultiply(red, a) << 16)
                    | (Premultiply(green, a) << 8)
                    | Premultiply(blue, a);
            }
        }

        return pixels;
    }

    private static uint Premultiply(uint channel, uint alpha) => (uint)Math.Round(channel * alpha / 255.0);

    private static bool Contains(ThumbBarGlyph glyph, double u, double v)
    {
        if (u < 0 || u > 1 || v < 0 || v > 1)
            return false;

        return glyph switch
        {
            ThumbBarGlyph.Play => InRightTriangle(u, v, 0, 1),
            ThumbBarGlyph.Pause => u <= BarWidth || u >= 1 - BarWidth,
            ThumbBarGlyph.Previous => u <= SkipBarWidth || InLeftTriangle(u, v, SkipBarWidth + SkipGap, 1),
            ThumbBarGlyph.Next => u >= 1 - SkipBarWidth || InRightTriangle(u, v, 0, 1 - SkipBarWidth - SkipGap),
            _ => false
        };
    }

    private static bool InRightTriangle(double u, double v, double left, double right)
    {
        if (u < left || u > right)
            return false;

        var halfHeight = 0.5 * (right - u) / (right - left);

        return Math.Abs(v - 0.5) <= halfHeight;
    }

    private static bool InLeftTriangle(double u, double v, double left, double right)
    {
        if (u < left || u > right)
            return false;

        var halfHeight = 0.5 * (u - left) / (right - left);

        return Math.Abs(v - 0.5) <= halfHeight;
    }
}
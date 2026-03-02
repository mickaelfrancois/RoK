using Windows.UI;

namespace Rok.Commons;

public static class ColorHelper
{
    /// <summary>
    /// Converts a 32-bit ARGB value to a Color struct. 
    /// The input long should be in the format 0xAARRGGBB, where AA is the alpha component, RR is red, GG is green, and BB is blue.
    /// </summary>
    /// <param name="argb"></param>
    /// <returns></returns>
    public static Color FromArgb(long argb)
    {
        byte alpha = (byte)(argb >> 24);
        byte red = (byte)(argb >> 16);
        byte green = (byte)(argb >> 8);
        byte blue = (byte)argb;

        return Color.FromArgb(alpha, red, green, blue);
    }

    /// <summary>
    /// Converts a Color struct to a 32-bit ARGB value represented as a long.
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public static long ToArgb(Color color)
    {
        return ((long)color.A << 24) | ((long)color.R << 16) | ((long)color.G << 8) | color.B;
    }
}

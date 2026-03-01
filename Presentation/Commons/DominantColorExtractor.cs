using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Rok.Commons;

public static class DominantColorExtractor
{
    private const uint SampleSize = 30;

    public static async Task<Color> ExtractAsync(string filePath)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            BitmapTransform transform = new()
            {
                ScaledWidth = SampleSize,
                ScaledHeight = SampleSize,
                InterpolationMode = BitmapInterpolationMode.NearestNeighbor
            };

            PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return FindVibrantColor(pixelData.DetachPixelData());
        }
        catch
        {
            return default;
        }
    }

    private static Color FindVibrantColor(byte[] pixels)
    {
        int count = pixels.Length / 4;
        if (count == 0)
            return default;

        double bestScore = -1;
        double bestR = 0, bestG = 0, bestB = 0;
        long sumR = 0, sumG = 0, sumB = 0;

        for (int i = 0; i < count; i++)
        {
            int offset = i * 4;
            double b = pixels[offset] / 255.0;
            double g = pixels[offset + 1] / 255.0;
            double r = pixels[offset + 2] / 255.0;

            sumR += pixels[offset + 2];
            sumG += pixels[offset + 1];
            sumB += pixels[offset];

            RgbToHsv(r, g, b, out _, out double s, out double v);

            double score = s * (1.0 - Math.Abs((2.0 * v) - 1.0));
            if (score > bestScore)
            {
                bestScore = score;
                bestR = r;
                bestG = g;
                bestB = b;
            }
        }

        if (bestScore < 0.1)
        {
            double avgR = sumR / (double)count / 255.0;
            double avgG = sumG / (double)count / 255.0;
            double avgB = sumB / (double)count / 255.0;
            double darken = AdaptiveDarken(avgR, avgG, avgB);
            return Color.FromArgb(255, (byte)(avgR * 255 * darken), (byte)(avgG * 255 * darken), (byte)(avgB * 255 * darken));
        }

        double factor = AdaptiveDarken(bestR, bestG, bestB);
        return Color.FromArgb(255, (byte)(bestR * 255 * factor), (byte)(bestG * 255 * factor), (byte)(bestB * 255 * factor));
    }

    private static double AdaptiveDarken(double r, double g, double b)
    {
        double luminance = RelativeLuminance(r, g, b);

        return luminance switch
        {
            > 0.5 => 0.20,
            > 0.2 => 0.35,
            > 0.05 => 0.55,
            _ => 0.75
        };
    }

    private static double RelativeLuminance(double r, double g, double b)
    {
        static double Linearize(double c) =>
            c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        return (0.2126 * Linearize(r)) + (0.7152 * Linearize(g)) + (0.0722 * Linearize(b));
    }

    private static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            h = 0;
            return;
        }

        if (max == r)
            h = (g - b) / delta % 6;
        else if (max == g)
            h = ((b - r) / delta) + 2;
        else
            h = ((r - g) / delta) + 4;

        h *= 60;
        if (h < 0) h += 360;
    }
}
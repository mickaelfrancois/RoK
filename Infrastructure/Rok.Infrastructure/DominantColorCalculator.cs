using Rok.Application.Interfaces;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Rok.Infrastructure;


public class DominantColorCalculator : IDominantColorCalculator
{
    private const uint SampleSize = 30;
    private const double DesaturationAmount = 0.7;


    public async Task<long?> CalculateAsync(string imagePath)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);
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

            (byte red, byte green, byte blue) = FindVibrantColor(pixelData.DetachPixelData());
            return ((long)255 << 24) | ((long)red << 16) | ((long)green << 8) | blue;
        }
        catch
        {
            return default;
        }
    }

    private static (byte red, byte green, byte blue) FindVibrantColor(byte[] pixels)
    {
        int count = pixels.Length / 4;
        if (count == 0)
            return default;

        double bestScore = -1;
        double bestRed = 0, bestGreen = 0, bestBlue = 0;
        long sumRed = 0, sumGreen = 0, sumBlue = 0;

        for (int i = 0; i < count; i++)
        {
            int offset = i * 4;
            double blue = pixels[offset] / 255.0;
            double green = pixels[offset + 1] / 255.0;
            double red = pixels[offset + 2] / 255.0;

            sumRed += pixels[offset + 2];
            sumGreen += pixels[offset + 1];
            sumBlue += pixels[offset];

            RgbToHsv(red, green, blue, out _, out double s, out double v);

            double score = s * (1.0 - Math.Abs((2.0 * v) - 1.0));
            if (score > bestScore)
            {
                bestScore = score;
                bestRed = red;
                bestGreen = green;
                bestBlue = blue;
            }
        }

        if (bestScore < 0.1)
        {
            double avgRed = sumRed / (double)count / 255.0;
            double avgGreen = sumGreen / (double)count / 255.0;
            double avgBlue = sumBlue / (double)count / 255.0;
            (avgRed, avgGreen, avgBlue) = Desaturate(avgRed, avgGreen, avgBlue, DesaturationAmount);
            double darken = AdaptiveDarken(avgRed, avgGreen, avgBlue);
            return ((byte)(avgRed * 255 * darken), (byte)(avgGreen * 255 * darken), (byte)(avgBlue * 255 * darken));
        }

        (bestRed, bestGreen, bestBlue) = Desaturate(bestRed, bestGreen, bestBlue, DesaturationAmount);
        double factor = AdaptiveDarken(bestRed, bestGreen, bestBlue);

        return ((byte)(bestRed * 255 * factor), (byte)(bestGreen * 255 * factor), (byte)(bestBlue * 255 * factor));
    }

    private static (double r, double g, double b) Desaturate(double red, double green, double blue, double amount)
    {
        double gray = (red * 0.299) + (green * 0.587) + (blue * 0.114);
        return (
            red + ((gray - red) * amount),
            green + ((gray - green) * amount),
            blue + ((gray - blue) * amount)
        );
    }

    private static double AdaptiveDarken(double red, double green, double blue)
    {
        double luminance = RelativeLuminance(red, green, blue);

        return luminance switch
        {
            > 0.5 => 0.15,
            > 0.2 => 0.28,
            > 0.05 => 0.45,
            _ => 0.65
        };
    }

    private static double RelativeLuminance(double r, double g, double b)
    {
        static double Linearize(double c) =>
            c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        return (0.2126 * Linearize(r)) + (0.7152 * Linearize(g)) + (0.0722 * Linearize(b));
    }

    private static void RgbToHsv(double red, double green, double blue, out double h, out double s, out double v)
    {
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            h = 0;
            return;
        }

        if (max == red)
            h = (green - blue) / delta % 6;
        else if (max == green)
            h = ((blue - red) / delta) + 2;
        else
            h = ((red - green) / delta) + 4;

        h *= 60;
        if (h < 0) h += 360;
    }
}
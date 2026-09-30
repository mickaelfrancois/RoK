using Microsoft.Extensions.Logging;

namespace Rok.Infrastructure.Player.Output;

/// <summary>Reads the native sample depth from the file header with TagLib.</summary>
public sealed class TagLibAudioFormatProbe(ILogger<TagLibAudioFormatProbe> logger) : IAudioFormatProbe
{
    public int GetBitsPerSample(string path)
    {
        try
        {
            using TagLib.File file = TagLib.File.Create(path, TagLib.ReadStyle.Average);

            return Math.Max(0, file.Properties?.BitsPerSample ?? 0);
        }
        catch (Exception ex) when (ex is TagLib.UnsupportedFormatException or TagLib.CorruptFileException or IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Unable to read the sample depth of {File}", path);
            return 0;
        }
    }
}
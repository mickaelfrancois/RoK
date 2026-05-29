namespace Rok.Commons;

public static class RadioDisplay
{
    public static string FormatTechnical(string? codec, int? bitrate)
    {
        bool hasCodec = !string.IsNullOrWhiteSpace(codec);
        bool hasBitrate = bitrate is > 0;

        if (!hasCodec && !hasBitrate)
            return string.Empty;

        if (!hasCodec)
            return $"{bitrate} kbps";

        if (!hasBitrate)
            return codec!;

        return $"{codec} · {bitrate} kbps";
    }
}

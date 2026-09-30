namespace Rok.Infrastructure.Player.Output;

/// <summary>Reads the native sample depth of an audio file.</summary>
public interface IAudioFormatProbe
{
    /// <summary>Bits per sample stored in the file; 0 when unknown (lossy formats) or unreadable.</summary>
    int GetBitsPerSample(string path);
}
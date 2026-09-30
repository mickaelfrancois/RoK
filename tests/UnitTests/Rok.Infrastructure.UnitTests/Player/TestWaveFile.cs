using NAudio.Wave;

namespace Rok.Infrastructure.UnitTests.Player;

/// <summary>Temporary WAV file written for a test and deleted on dispose.</summary>
internal sealed class TestWaveFile : IDisposable
{
    private TestWaveFile(string path, byte[] data)
    {
        Path = path;
        Data = data;
    }

    public string Path { get; }

    /// <summary>PCM bytes stored in the data chunk.</summary>
    public byte[] Data { get; }

    public static TestWaveFile Create(int sampleRate, int bitsPerSample, int channels, byte[] data)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rok-test-{Guid.NewGuid():N}.wav");

        using (WaveFileWriter writer = new(path, new WaveFormat(sampleRate, bitsPerSample, channels)))
        {
            writer.Write(data, 0, data.Length);
        }

        return new TestWaveFile(path, data);
    }

    /// <summary>Silent 16-bit stereo file of <paramref name="seconds"/> at 44.1 kHz.</summary>
    public static TestWaveFile CreateSilence(double seconds = 1) =>
        Create(44100, 16, 2, new byte[(int)(44100 * seconds) * 4]);

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // Still open by a test that failed; the temp folder is cleaned eventually.
        }
    }
}
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rok.Infrastructure.UnitTests.TestData;

public enum AudioFixtureFormat
{
    Wav,
    Aiff,
    Aif,
    M4a,
    Wma
}

internal static class AudioFixtureFactory
{
    public static readonly TimeSpan FixtureDuration = TimeSpan.FromSeconds(2);

    public const string FixtureTitle = "Rok fixture";

    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int EncoderBitRate = 128000;

    public static string Create(string folder, AudioFixtureFormat format)
    {
        string wavPath = Path.Combine(folder, $"source-{format}.wav");
        WriteSineWav(wavPath);

        string path = format switch
        {
            AudioFixtureFormat.Wav => wavPath,
            AudioFixtureFormat.Aiff => ConvertToAiff(wavPath, Path.Combine(folder, "fixture.aiff")),
            AudioFixtureFormat.Aif => ConvertToAiff(wavPath, Path.Combine(folder, "fixture.aif")),
            AudioFixtureFormat.M4a => Encode(wavPath, Path.Combine(folder, "fixture.m4a"), MediaFoundationEncoder.EncodeToAac),
            AudioFixtureFormat.Wma => Encode(wavPath, Path.Combine(folder, "fixture.wma"), MediaFoundationEncoder.EncodeToWma),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        WriteTitle(path);

        return path;
    }

    public static bool IsEncoderAvailable(AudioFixtureFormat format)
    {
        Guid? subtype = format switch
        {
            AudioFixtureFormat.M4a => AudioSubtypes.MFAudioFormat_AAC,
            AudioFixtureFormat.Wma => AudioSubtypes.MFAudioFormat_WMAudioV8,
            _ => null
        };

        if (subtype is null)
            return true;

        try
        {
            MediaFoundationApi.Startup();

            return MediaFoundationEncoder.SelectMediaType(subtype.Value, new WaveFormat(SampleRate, 16, Channels), EncoderBitRate) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void WriteSineWav(string path)
    {
        SignalGenerator generator = new(SampleRate, Channels)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = 440,
            Gain = 0.2
        };

        WaveFileWriter.CreateWaveFile16(path, generator.Take(FixtureDuration));
    }

    private static string ConvertToAiff(string wavPath, string aiffPath)
    {
        using WaveFileReader reader = new(wavPath);
        AiffFileWriter.CreateAiffFile(aiffPath, reader);

        return aiffPath;
    }

    private static string Encode(string wavPath, string outputPath, Action<IWaveProvider, string, int> encode)
    {
        MediaFoundationApi.Startup();

        using WaveFileReader reader = new(wavPath);
        encode(reader, outputPath, EncoderBitRate);

        return outputPath;
    }

    private static void WriteTitle(string path)
    {
        using TagLib.File file = TagLib.File.Create(path);
        file.Tag.Title = FixtureTitle;
        file.Save();
    }
}
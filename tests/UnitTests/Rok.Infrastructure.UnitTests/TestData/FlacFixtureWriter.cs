using System.Security.Cryptography;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rok.Infrastructure.UnitTests.TestData;

/// <summary>A FLAC metadata block, as written between STREAMINFO and the first audio frame.</summary>
internal sealed record FlacBlock(byte Type, byte[] Data)
{
    public const byte StreamInfoType = 0;
    public const byte PaddingType = 1;
    public const byte ApplicationType = 2;

    /// <summary>An APPLICATION block: a 4-character registered id followed by the payload.</summary>
    public static FlacBlock Application(string id, byte[] payload) => new(ApplicationType, [.. System.Text.Encoding.ASCII.GetBytes(id), .. payload]);

    /// <summary>A PADDING block of the given size.</summary>
    public static FlacBlock Padding(int size) => new(PaddingType, new byte[size]);
}

/// <summary>
/// Minimal hand-written FLAC encoder for tests. Frames use VERBATIM subframes, so the stream is lossless and
/// decodable by any conformant decoder while staying a few dozen lines long.
/// </summary>
internal static class FlacFixtureWriter
{
    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int BitsPerSample = 16;
    private const int BlockSize = 4096;

    /// <summary>Writes a FLAC holding the same 2 s sine as <see cref="AudioFixtureFactory"/>, with optional blocks after STREAMINFO.</summary>
    public static void WriteSine(string path, IReadOnlyList<FlacBlock>? blocks = null)
    {
        Write(path, ReadSinePcm(), blocks);
    }

    /// <summary>Writes a FLAC from interleaved 16-bit little-endian stereo PCM.</summary>
    public static void Write(string path, byte[] pcm, IReadOnlyList<FlacBlock>? blocks = null)
    {
        int totalSamples = pcm.Length / (Channels * BitsPerSample / 8);
        List<byte[]> frames = EncodeFrames(pcm, totalSamples);

        int minFrame = frames.Min(frame => frame.Length);
        int maxFrame = frames.Max(frame => frame.Length);

        List<FlacBlock> metadata = [new(FlacBlock.StreamInfoType, StreamInfo(totalSamples, minFrame, maxFrame, MD5.HashData(pcm)))];
        metadata.AddRange(blocks ?? []);

        using FileStream stream = File.Create(path);
        stream.Write("fLaC"u8);

        for (int i = 0; i < metadata.Count; i++)
        {
            FlacBlock block = metadata[i];
            byte last = i == metadata.Count - 1 ? (byte)0x80 : (byte)0;

            stream.WriteByte((byte)(last | block.Type));
            stream.WriteByte((byte)(block.Data.Length >> 16));
            stream.WriteByte((byte)(block.Data.Length >> 8));
            stream.WriteByte((byte)block.Data.Length);
            stream.Write(block.Data);
        }

        foreach (byte[] frame in frames)
            stream.Write(frame);
    }

    private static byte[] ReadSinePcm()
    {
        string wavPath = Path.Combine(Path.GetTempPath(), "FlacFixtureWriter_" + Guid.NewGuid().ToString("N") + ".wav");

        try
        {
            SignalGenerator generator = new(SampleRate, Channels)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = 440,
                Gain = 0.2
            };

            WaveFileWriter.CreateWaveFile16(wavPath, generator.Take(AudioFixtureFactory.FixtureDuration));

            using WaveFileReader reader = new(wavPath);
            byte[] pcm = new byte[reader.Length];
            reader.ReadExactly(pcm);

            return pcm;
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    private static byte[] StreamInfo(int totalSamples, int minFrame, int maxFrame, byte[] md5)
    {
        byte[] data = new byte[34];

        WriteBigEndian(data, 0, BlockSize, 2);
        WriteBigEndian(data, 2, BlockSize, 2);
        WriteBigEndian(data, 4, minFrame, 3);
        WriteBigEndian(data, 7, maxFrame, 3);

        ulong packed = ((ulong)SampleRate << 44)
                       | ((ulong)(Channels - 1) << 41)
                       | ((ulong)(BitsPerSample - 1) << 36)
                       | (uint)totalSamples;

        for (int i = 0; i < 8; i++)
            data[10 + i] = (byte)(packed >> (56 - 8 * i));

        md5.CopyTo(data, 18);

        return data;
    }

    private static List<byte[]> EncodeFrames(byte[] pcm, int totalSamples)
    {
        List<byte[]> frames = [];
        int bytesPerSample = BitsPerSample / 8;

        for (int start = 0, number = 0; start < totalSamples; start += BlockSize, number++)
        {
            int count = Math.Min(BlockSize, totalSamples - start);
            List<byte> frame = [0xFF, 0xF8];

            frame.Add((byte)((count == BlockSize ? 12 : 7) << 4 | 9));
            frame.Add(0x18);
            frame.AddRange(Utf8Number(number));

            if (count != BlockSize)
            {
                frame.Add((byte)((count - 1) >> 8));
                frame.Add((byte)(count - 1));
            }

            frame.Add(Crc8(frame));

            for (int channel = 0; channel < Channels; channel++)
            {
                frame.Add(0x02);

                for (int i = 0; i < count; i++)
                {
                    int offset = ((start + i) * Channels + channel) * bytesPerSample;
                    short sample = (short)(pcm[offset] | pcm[offset + 1] << 8);

                    frame.Add((byte)(sample >> 8));
                    frame.Add((byte)sample);
                }
            }

            ushort crc = Crc16(frame);
            frame.Add((byte)(crc >> 8));
            frame.Add((byte)crc);

            frames.Add([.. frame]);
        }

        return frames;
    }

    private static List<byte> Utf8Number(int value)
    {
        if (value < 0x80)
            return [(byte)value];

        List<byte> tail = [(byte)(0x80 | (value & 0x3F))];
        int remaining = value >> 6;
        int leadLimit = 0x20;
        int leadMask = 0xC0;

        while (remaining >= leadLimit)
        {
            tail.Insert(0, (byte)(0x80 | (remaining & 0x3F)));
            remaining >>= 6;
            leadLimit >>= 1;
            leadMask = (leadMask >> 1) | 0x80;
        }

        tail.Insert(0, (byte)(leadMask | remaining));

        return tail;
    }

    private static byte Crc8(IEnumerable<byte> bytes)
    {
        byte crc = 0;

        foreach (byte value in bytes)
        {
            crc ^= value;

            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }

        return crc;
    }

    private static ushort Crc16(IEnumerable<byte> bytes)
    {
        ushort crc = 0;

        foreach (byte value in bytes)
        {
            crc ^= (ushort)(value << 8);

            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x8005) : (ushort)(crc << 1);
        }

        return crc;
    }

    private static void WriteBigEndian(byte[] target, int offset, int value, int length)
    {
        for (int i = 0; i < length; i++)
            target[offset + i] = (byte)(value >> (8 * (length - 1 - i)));
    }
}
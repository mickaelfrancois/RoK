using System.Buffers.Binary;
using NAudio.Wave;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player.Output;

public class FloatToPcmWaveProviderTests
{
    private static WaveFormatExtensible Pcm(int containerBits, int validBits) =>
        new(44100, containerBits, 1, useIeeeFloat: false, validBits, 0x4);

    private static byte[] Convert(float[] samples, WaveFormat target)
    {
        FloatToPcmWaveProvider sut = new(new ArraySampleProvider(samples), target);
        byte[] buffer = new byte[samples.Length * (target.BitsPerSample / 8)];

        int read = sut.Read(buffer);

        Assert.Equal(buffer.Length, read);
        return buffer;
    }

    [Fact(DisplayName = "float_to_pcm_16_round_trip_is_exact")]
    public void Read_RoundTripsSixteenBitSamples_Exactly()
    {
        // Arrange
        short[] expected = [short.MinValue, -1, 0, 1, short.MaxValue, 12345, -23456];
        float[] samples = [.. expected.Select(value => value / 32768f)];

        // Act
        byte[] bytes = Convert(samples, Pcm(16, 16));

        // Assert
        short[] actual = [.. Enumerable.Range(0, expected.Length).Select(i => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2)))];
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "float_to_pcm_24_packed_round_trip_is_exact")]
    public void Read_RoundTripsPackedTwentyFourBitSamples_Exactly()
    {
        // Arrange
        int[] expected = [-8388608, -1, 0, 1, 8388607, 4194305, -7654321];
        float[] samples = [.. expected.Select(value => value / 8388608f)];

        // Act
        byte[] bytes = Convert(samples, Pcm(24, 24));

        // Assert
        int[] actual = [.. Enumerable.Range(0, expected.Length).Select(i => (bytes[(i * 3) + 2] << 24 | bytes[(i * 3) + 1] << 16 | bytes[i * 3] << 8) >> 8)];
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "float_to_pcm_24_in_32_is_left_justified_and_exact")]
    public void Read_WritesTwentyFourBitsInThirtyTwo_LeftJustified()
    {
        // Arrange
        int[] expected = [-8388608, -1, 0, 1, 8388607];
        float[] samples = [.. expected.Select(value => value / 8388608f)];

        // Act
        byte[] bytes = Convert(samples, Pcm(32, 24));

        // Assert
        int[] containers = [.. Enumerable.Range(0, expected.Length).Select(i => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4)))];
        Assert.All(containers, container => Assert.Equal(0, container & 0xFF));
        Assert.Equal(expected, containers.Select(container => container >> 8));
    }

    [Fact(DisplayName = "float_to_pcm_clamps_above_full_scale")]
    public void ToInteger_ClampsAboveFullScale()
    {
        // Act
        int high = FloatToPcmWaveProvider.ToInteger(1.5f, 16);
        int low = FloatToPcmWaveProvider.ToInteger(-2f, 16);

        // Assert
        Assert.Equal(short.MaxValue, high);
        Assert.Equal(short.MinValue, low);
    }

    [Fact(DisplayName = "float_to_pcm_rounds_to_nearest")]
    public void ToInteger_RoundsToNearest()
    {
        // Act
        int up = FloatToPcmWaveProvider.ToInteger(100.7f / 32768f, 16);
        int down = FloatToPcmWaveProvider.ToInteger(-100.7f / 32768f, 16);

        // Assert
        Assert.Equal(101, up);
        Assert.Equal(-101, down);
    }

    [Fact(DisplayName = "float_to_pcm_keeps_float_samples_unchanged")]
    public void Read_KeepsFloatSamplesUnchanged()
    {
        // Arrange
        float[] samples = [-1f, -0.123456f, 0f, 0.5f, 1f];
        WaveFormatExtensible target = new(44100, 32, 1, useIeeeFloat: true, 32, 0x4);

        // Act
        byte[] bytes = Convert(samples, target);

        // Assert
        float[] actual = [.. Enumerable.Range(0, samples.Length).Select(i => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4)))];
        Assert.Equal(samples, actual);
    }

    [Fact(DisplayName = "float_to_pcm_rejects_a_target_with_another_rate")]
    public void Constructor_Throws_WhenRateDiffers()
    {
        // Arrange
        WaveFormatExtensible target = new(48000, 16, 1, useIeeeFloat: false, 16, 0x4);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FloatToPcmWaveProvider(new ArraySampleProvider([0f]), target));
    }

    private sealed class ArraySampleProvider(float[] samples) : ISampleProvider
    {
        private int _position;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        public int Read(Span<float> buffer)
        {
            int count = Math.Min(buffer.Length, samples.Length - _position);
            samples.AsSpan(_position, count).CopyTo(buffer);
            _position += count;

            return count;
        }
    }
}
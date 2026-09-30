using System.Buffers.Binary;
using NAudio.Wave;

namespace Rok.Infrastructure.Player.Output;

/// <summary>
/// Converts float samples to the negotiated exclusive format. Integer formats scale by 2^(n-1), round to nearest and
/// clamp, so a sample decoded from an n-bit file comes back bit for bit (NAudio's converters scale by 2^(n-1)-1 and
/// truncate, which is not exact). 24 valid bits in a 32-bit container are left-justified.
/// </summary>
internal sealed class FloatToPcmWaveProvider : IWaveProvider
{
    private readonly ISampleProvider _source;
    private readonly int _containerBytes;
    private readonly int _validBits;
    private readonly bool _isFloat;
    private float[] _samples = [];

    public FloatToPcmWaveProvider(ISampleProvider source, WaveFormat targetFormat)
    {
        if (source.WaveFormat.SampleRate != targetFormat.SampleRate || source.WaveFormat.Channels != targetFormat.Channels)
            throw new ArgumentException("The target format must keep the source rate and channel count.", nameof(targetFormat));

        _source = source;
        _containerBytes = targetFormat.BitsPerSample / 8;
        _validBits = ExclusiveFormatLadder.ValidBits(targetFormat);
        _isFloat = ExclusiveFormatLadder.IsFloat(targetFormat);

        if (!_isFloat && _containerBytes is not (2 or 3 or 4))
            throw new ArgumentException($"Unsupported container of {targetFormat.BitsPerSample} bits.", nameof(targetFormat));

        WaveFormat = targetFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<byte> buffer)
    {
        int sampleCount = buffer.Length / _containerBytes;

        if (_samples.Length < sampleCount)
            _samples = new float[sampleCount];

        Span<float> samples = _samples.AsSpan(0, sampleCount);
        int read = _source.Read(samples);

        for (int i = 0; i < read; i++)
            Write(buffer.Slice(i * _containerBytes, _containerBytes), samples[i]);

        return read * _containerBytes;
    }

    private void Write(Span<byte> destination, float sample)
    {
        if (_isFloat)
        {
            BinaryPrimitives.WriteSingleLittleEndian(destination, sample);
            return;
        }

        int value = ToInteger(sample, _validBits);

        switch (_containerBytes)
        {
            case 2:
                BinaryPrimitives.WriteInt16LittleEndian(destination, (short)value);
                break;

            case 3:
                destination[0] = (byte)value;
                destination[1] = (byte)(value >> 8);
                destination[2] = (byte)(value >> 16);
                break;

            default:
                BinaryPrimitives.WriteInt32LittleEndian(destination, value << (32 - _validBits));
                break;
        }
    }

    internal static int ToInteger(float sample, int validBits)
    {
        double scale = 1L << (validBits - 1);
        double max = scale - 1;
        double scaled = Math.Round(sample * scale, MidpointRounding.ToEven);

        return (int)Math.Clamp(scaled, -scale, max);
    }
}
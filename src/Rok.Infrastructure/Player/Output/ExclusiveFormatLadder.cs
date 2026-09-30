using NAudio.Wave;

namespace Rok.Infrastructure.Player.Output;

/// <summary>Formats offered to an exclusive output, in order, and the rule for chaining two tracks on one output.</summary>
internal static class ExclusiveFormatLadder
{
    /// <summary>Depth assumed when the file does not state one (lossy formats).</summary>
    internal const int UnknownDepthAssumed = 24;

    /// <summary>
    /// Native depth first (16, or packed 24), then 24 bits in a 32-bit container, then 32-bit float, all at the native
    /// rate and channel count. Above 24 bits only float is offered, since a 32-bit integer does not survive a float exactly.
    /// </summary>
    public static IReadOnlyList<WaveFormatExtensible> Candidates(int sampleRate, int channels, int nativeBitsPerSample)
    {
        int bits = EffectiveBits(nativeBitsPerSample);

        if (bits > 24)
            return [CreateFloat(sampleRate, channels)];

        WaveFormatExtensible native = bits <= 16
            ? CreatePcm(sampleRate, channels, containerBits: 16, validBits: 16)
            : CreatePcm(sampleRate, channels, containerBits: 24, validBits: 24);

        return [native, CreatePcm(sampleRate, channels, containerBits: 32, validBits: 24), CreateFloat(sampleRate, channels)];
    }

    /// <summary>
    /// A track can follow on the negotiated output without reopening when it has the same rate and channel count and
    /// its depth fits the valid bits of the output (a deeper track would be truncated).
    /// </summary>
    public static bool CanChain(WaveFormat negotiated, int nextSampleRate, int nextChannels, int nextBitsPerSample)
    {
        if (negotiated.SampleRate != nextSampleRate || negotiated.Channels != nextChannels)
            return false;

        if (IsFloat(negotiated))
            return true;

        return EffectiveBits(nextBitsPerSample) <= ValidBits(negotiated);
    }

    public static bool IsFloat(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.IeeeFloat
        || (format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);

    public static int ValidBits(WaveFormat format) =>
        format is WaveFormatExtensible { ValidBitsPerSample: > 0 } extensible ? extensible.ValidBitsPerSample : format.BitsPerSample;

    /// <summary>Same rate, channels, container, valid bits and sample type.</summary>
    public static bool AreEquivalent(WaveFormat left, WaveFormat right) =>
        left.SampleRate == right.SampleRate
        && left.Channels == right.Channels
        && left.BitsPerSample == right.BitsPerSample
        && ValidBits(left) == ValidBits(right)
        && IsFloat(left) == IsFloat(right);

    private static int EffectiveBits(int nativeBitsPerSample) => nativeBitsPerSample <= 0 ? UnknownDepthAssumed : nativeBitsPerSample;

    private static WaveFormatExtensible CreatePcm(int sampleRate, int channels, int containerBits, int validBits) =>
        new(sampleRate, containerBits, channels, useIeeeFloat: false, validBits, ChannelMask(channels));

    private static WaveFormatExtensible CreateFloat(int sampleRate, int channels) =>
        new(sampleRate, 32, channels, useIeeeFloat: true, 32, ChannelMask(channels));

    private static int ChannelMask(int channels) => channels switch
    {
        1 => 0x4,
        2 => 0x3,
        4 => 0x33,
        6 => 0x3F,
        8 => 0x63F,
        _ => 0
    };
}
using NAudio.Dsp;
using Rok.Shared.Extensions;

namespace Rok.Infrastructure.Player;

public class EqualizerBand(float frequency, float bandwidth, int channels)
{
    public float Frequency { get; set; } = frequency;
    public float Gain { get; set; } = 0f;
    public float Bandwidth { get; set; } = bandwidth;

    private readonly BiQuadFilter[] _filters = new BiQuadFilter[channels];

    public void SetGain(float gain, int sampleRate)
    {
        Gain = gain;

        for (int i = 0; i < channels; i++)
        {
            if (Gain.EqualsZero())
            {
                _filters[i] = BiQuadFilter.PeakingEQ(sampleRate, Frequency, Bandwidth, 0);
            }
            else
            {
                _filters[i] = BiQuadFilter.PeakingEQ(sampleRate, Frequency, Bandwidth, Gain);
            }
        }
    }

    public float Transform(float sample, int channel)
    {
        return _filters[channel].Transform(sample);
    }
}
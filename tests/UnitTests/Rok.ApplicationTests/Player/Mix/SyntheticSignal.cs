using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests.Player.Mix;

internal static class SyntheticSignal
{
    public const int SampleRate = 8000;

    public static float[] Sine(double seconds, double amplitude = 1.0, double frequency = 440)
    {
        var samples = new float[(int)(seconds * SampleRate)];

        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / SampleRate));

        return samples;
    }

    public static float[] Silence(double seconds) => new float[(int)(seconds * SampleRate)];

    public static float[] Ramp(double seconds, double fromAmplitude, double toAmplitude, double frequency = 440)
    {
        var samples = new float[(int)(seconds * SampleRate)];

        for (var i = 0; i < samples.Length; i++)
        {
            var amplitude = fromAmplitude + ((toAmplitude - fromAmplitude) * i / samples.Length);
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / SampleRate));
        }

        return samples;
    }

    public static float[] Noise(double seconds, double rmsDb)
    {
        var random = new Random(42);
        var peak = Math.Pow(10, rmsDb / 20) * Math.Sqrt(3);
        var samples = new float[(int)(seconds * SampleRate)];

        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(((random.NextDouble() * 2) - 1) * peak);

        return samples;
    }

    public static float[] Concat(params float[][] parts) => parts.SelectMany(p => p).ToArray();

    public static RmsEnvelope Envelope(float[] mono, double startSeconds = 0, double? trackLengthSeconds = null)
    {
        var accumulator = new RmsEnvelopeAccumulator(SampleRate);
        accumulator.Add(mono, 1);

        return accumulator.Build(startSeconds, trackLengthSeconds ?? ((double)mono.Length / SampleRate));
    }
}
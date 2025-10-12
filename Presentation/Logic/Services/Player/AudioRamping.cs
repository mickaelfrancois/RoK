namespace Rok.Logic.Services.Player;

internal static class AudioRamping
{
    // t: 0.0 -> 1.0 (ramp progress)
    // masterVolumePercent: master volume in percent (e.g., 0..100)
    // Returns volume in percent adapted for the engine
    public static double Linear(double t, double masterVolumePercent) =>
        ClampPercent((1.0 - t) * masterVolumePercent);

    // Simple perceptual curve (power curve). More natural to the ear.
    // exponent = 2 is a good starting point (x^2)
    public static double Perceptual(double t, double masterVolumePercent, double exponent = 2.0)
    {
        double inv = 1.0 - t;
        double curve = Math.Pow(inv, exponent);
        return ClampPercent(curve * masterVolumePercent);
    }

    // Interpolation in dB (recommended if you want a linear fade in perceived level).
    // We map percent (0..100) -> linear (0..1), then percent -> dB, interpolate in dB and convert back.
    // minDb typically -80 dB (practically inaudible)
    public static double DbInterpolate(double t, double masterVolumePercent, double minDb = -80.0)
    {
        // map percent -> linear gain [0..1]
        double startGain = 1.0; // for fade-out start = 1.0
        double endGain = 0.0;   // for fade-out end = 0.0

        // convert gains to dB: gainDb = 20*log10(gain) ; handle zero
        double startDb = 0.0; // 0 dB = full
        double endDb = minDb; // very low

        double curDb = (startDb * (1.0 - t)) + (endDb * t);
        
        // convert back to linear gain
        double gain = Math.Pow(10.0, curDb / 20.0);

        return ClampPercent(gain * masterVolumePercent);
    }

    private static double ClampPercent(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0.0;
        if (v < 0.0) return 0.0;
        if (v > 100.0) return 100.0;
        return v;
    }
}
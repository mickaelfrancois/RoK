namespace Rok.Application.Player;

/// <summary>Resolves the linear ReplayGain factor applied to a track.</summary>
public static class ReplayGainCalculator
{
    public const double MinPreampDb = -6;

    public const double MaxPreampDb = 6;

    public const double PreampStepDb = 0.5;

    /// <summary>
    /// Returns the linear gain for <paramref name="track"/>: 1 when the mode is off or no gain is tagged,
    /// otherwise <c>10^((gain + preamp) / 20)</c> capped by <c>1 / peak</c> of the same level.
    /// </summary>
    /// <param name="previous">Track played just before in the queue, if any (Auto mode).</param>
    /// <param name="next">Track played just after in the queue, if any (Auto mode).</param>
    public static float Resolve(EReplayGainMode mode, double preampDb, TrackDto? previous, TrackDto track, TrackDto? next)
    {
        if (mode == EReplayGainMode.Off)
            return 1f;

        bool useAlbum = mode switch
        {
            EReplayGainMode.Album => true,
            EReplayGainMode.Auto => IsInAlbumContext(previous, track, next),
            _ => false
        };

        if (!TrySelectLevel(track, useAlbum, out double gainDb, out double? peak))
            return 1f;

        double linear = Math.Pow(10, (gainDb + Math.Clamp(preampDb, MinPreampDb, MaxPreampDb)) / 20);

        if (peak is double peakValue && double.IsFinite(peakValue) && peakValue > 0)
            linear = Math.Min(linear, 1 / peakValue);

        if (!double.IsFinite(linear) || linear <= 0)
            return 1f;

        return (float)linear;
    }

    private static bool IsInAlbumContext(TrackDto? previous, TrackDto track, TrackDto? next)
    {
        if (previous is not null && PlaybackTransitionPolicy.IsConsecutiveSameAlbum(previous, track))
            return true;

        return next is not null && PlaybackTransitionPolicy.IsConsecutiveSameAlbum(track, next);
    }

    private static bool TrySelectLevel(TrackDto track, bool preferAlbum, out double gainDb, out double? peak)
    {
        (double? Gain, double? Peak) album = (track.ReplayGainAlbumGain, track.ReplayGainAlbumPeak);
        (double? Gain, double? Peak) single = (track.ReplayGainTrackGain, track.ReplayGainTrackPeak);

        (double? Gain, double? Peak) preferred = preferAlbum ? album : single;
        (double? Gain, double? Peak) fallback = preferAlbum ? single : album;

        (double? Gain, double? Peak) chosen = IsUsable(preferred.Gain) ? preferred : fallback;

        if (!IsUsable(chosen.Gain))
        {
            gainDb = 0;
            peak = null;
            return false;
        }

        gainDb = chosen.Gain!.Value;
        peak = chosen.Peak;
        return true;
    }

    private static bool IsUsable(double? gain) => gain is double value && double.IsFinite(value);
}
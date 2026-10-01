using Rok.Application.Player.Output;

namespace Rok.Application.Interfaces;

public interface IPlayerEngine
{
    event EventHandler? OnMediaChanged;

    event EventHandler? OnMediaEnded;

    event EventHandler? OnMediaStateChanged;

    event EventHandler? OnMediaAboutToEnd;

    event EventHandler<string>? OnMetadataChanged;

    double Position { get; }

    double Length { get; set; }

    bool IsLive { get; }

    bool IsBuffering { get; }

    void Pause();

    void Play();

    void Stop();

    void SetPosition(double position);

    void SetVolume(double volume);

    /// <summary>Opens <paramref name="track"/> on a fresh output, scaled by the linear <paramref name="replayGain"/> factor.</summary>
    bool SetTrack(TrackDto track, float replayGain);

    bool SetStream(RadioStationDto station);

    void SetEqualizerBand(int bandIndex, float gain);

    /// <summary>
    /// Performs a simultaneous crossfade from the current track to <paramref name="nextTrack"/> over
    /// <paramref name="durationSeconds"/>, at the current volume.
    /// </summary>
    Task CrossfadeToAsync(TrackDto nextTrack, float replayGain, double durationSeconds, CancellationToken ct);

    /// <summary>Raised once the output has moved, without reopening, to the track queued by <see cref="QueueNextTrack"/>.</summary>
    event EventHandler<GaplessTransitionEventArgs>? OnGaplessTransition;

    /// <summary>Preloads <paramref name="nextTrack"/> so that it follows the current track without any gap.</summary>
    /// <returns><c>false</c> when nothing is playing, a radio is playing, the file cannot be opened or its format differs.</returns>
    bool QueueNextTrack(TrackDto nextTrack, float replayGain);

    /// <summary>Drops the track queued by <see cref="QueueNextTrack"/>, if any, and rearms <see cref="OnMediaAboutToEnd"/>.</summary>
    void ClearNextTrack();

    /// <summary>
    /// Changes, without reopening, the linear ReplayGain factor of the live source playing or preloaded for
    /// <paramref name="trackId"/>. Unknown identifiers are ignored.
    /// </summary>
    void UpdateReplayGain(long trackId, float replayGain);

    /// <summary>Actual state of the output currently used by the music or the radio.</summary>
    AudioOutputState OutputState { get; }

    /// <summary>Raised off the UI thread when the device of the open output disappears or fails.</summary>
    event EventHandler<OutputLostEventArgs>? OnOutputLost;

    /// <summary>Raised off the render thread each time <see cref="OutputState"/> changes.</summary>
    event EventHandler<AudioOutputState>? OnOutputStateChanged;

    /// <summary>
    /// Sets the requested device and mode. While playing, the output reopens at once at the current position;
    /// otherwise it is released and reopens on the next <see cref="Play"/>.
    /// </summary>
    void SetOutputTarget(AudioOutputTarget target);

    /// <summary>Resolves the device again for the current target, with the same rules as <see cref="SetOutputTarget"/>.</summary>
    void ReopenOutput();
}
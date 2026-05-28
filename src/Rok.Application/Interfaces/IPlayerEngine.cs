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

    int CrossfadeDelay { get; }

    bool IsLive { get; }

    bool IsBuffering { get; }

    void Pause();

    void Play();

    void Stop();

    void SetPosition(double position);

    void SetVolume(double volume);

    bool SetTrack(TrackDto track);

    bool SetStream(RadioStationDto station);

    void SetEqualizerBand(int bandIndex, float gain);

    /// <summary>Performs a simultaneous crossfade from the current track to <paramref name="nextTrack"/>.</summary>
    Task CrossfadeToAsync(TrackDto nextTrack, double durationSeconds, double masterVolume, CancellationToken ct);
}

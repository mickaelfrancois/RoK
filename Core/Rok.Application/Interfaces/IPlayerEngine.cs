namespace Rok.Application.Interfaces;

public interface IPlayerEngine
{
    event EventHandler? OnMediaChanged;

    event EventHandler? OnMediaEnded;

    event EventHandler? OnMediaStateChanged;

    event EventHandler? OnMediaAboutToEnd;

    double Position { get; }

    double Length { get; set; }

    int CrossfadeDelay { get; }

    void Pause();

    void Play();

    void Stop();

    void SetPosition(double position);

    void SetVolume(double volume);

    bool SetTrack(TrackDto track);
}
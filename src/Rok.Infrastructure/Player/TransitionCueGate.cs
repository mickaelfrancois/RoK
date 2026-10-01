namespace Rok.Infrastructure.Player;

/// <summary>Decides when the position of the current track has reached an armed transition cue, once per arming.</summary>
internal sealed class TransitionCueGate
{
    private readonly Lock _lock = new();
    private bool _armed;
    private bool _fired;
    private long _trackId;
    private double _positionSeconds;

    /// <summary>Arms the cue for <paramref name="trackId"/> at <paramref name="positionSeconds"/>.</summary>
    public void Arm(long trackId, double positionSeconds)
    {
        lock (_lock)
        {
            _armed = true;
            _fired = false;
            _trackId = trackId;
            _positionSeconds = positionSeconds;
        }
    }

    /// <summary>Drops the cue.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _armed = false;
            _fired = false;
        }
    }

    /// <summary>Allows the armed cue to be raised again, for instance after a seek or a resume.</summary>
    public void Rearm()
    {
        lock (_lock)
        {
            _fired = false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> once when the cue is armed for <paramref name="currentTrackId"/> and
    /// <paramref name="position"/> has reached it.
    /// </summary>
    public bool ShouldRaise(long currentTrackId, double position)
    {
        lock (_lock)
        {
            if (!_armed || _fired || _trackId != currentTrackId || position < _positionSeconds)
                return false;

            _fired = true;

            return true;
        }
    }
}
namespace Rok.Services.Taskbar;

/// <summary>Owns the icon handles of the thumbnail toolbar and destroys each of them exactly once.</summary>
public sealed class ThumbBarIconSet : IDisposable
{
    private readonly Action<nint> _destroy;
    private bool _disposed;

    public ThumbBarIconSet(nint previous, nint play, nint pause, nint next, Action<nint> destroy)
    {
        ArgumentNullException.ThrowIfNull(destroy);

        Previous = previous;
        Play = play;
        Pause = pause;
        Next = next;
        _destroy = destroy;
    }

    public nint Previous { get; }

    public nint Play { get; }

    public nint Pause { get; }

    public nint Next { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var handle in new[] { Previous, Play, Pause, Next })
        {
            if (handle != 0)
                _destroy(handle);
        }
    }
}
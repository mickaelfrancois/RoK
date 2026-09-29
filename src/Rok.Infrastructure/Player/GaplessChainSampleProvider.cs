using System.Diagnostics.CodeAnalysis;
using NAudio.Wave;

namespace Rok.Infrastructure.Player;

/// <summary>Raised when <see cref="GaplessChainSampleProvider"/> switches from one source to the queued one.</summary>
public sealed class SourceSwitchedEventArgs(ISampleProvider previous, ISampleProvider current) : EventArgs
{
    /// <summary>Source that was exhausted.</summary>
    public ISampleProvider Previous { get; } = previous;

    /// <summary>Source now rendered.</summary>
    public ISampleProvider Current { get; } = current;
}

/// <summary>
/// Chains the current source with a queued next source, switching on the exact sample where the
/// current one is exhausted so that no silence is inserted between them.
/// </summary>
public sealed class GaplessChainSampleProvider : ISampleProvider
{
    private readonly Lock _lock = new();
    private ISampleProvider _current;
    private ISampleProvider? _next;

    /// <summary>Raised on the rendering thread when the chain moves to the queued source.</summary>
    public event EventHandler<SourceSwitchedEventArgs>? SourceSwitched;

    /// <summary>Creates a chain whose format is fixed by <paramref name="initial"/>.</summary>
    public GaplessChainSampleProvider(ISampleProvider initial)
    {
        ArgumentNullException.ThrowIfNull(initial);

        _current = initial;
        WaveFormat = initial.WaveFormat;
    }

    /// <summary>Format of the initial source, required from every queued source.</summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>Source currently rendered.</summary>
    public ISampleProvider Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>Queues <paramref name="next"/> to follow the current source.</summary>
    /// <param name="next">Source to play once the current one is exhausted.</param>
    /// <param name="replaced">Source previously queued and now discarded, for the caller to dispose.</param>
    /// <returns><c>false</c> when the sample rate or channel count differs from the chain format.</returns>
    public bool TryQueueNext(ISampleProvider next, out ISampleProvider? replaced)
    {
        ArgumentNullException.ThrowIfNull(next);

        replaced = null;

        if (next.WaveFormat.SampleRate != WaveFormat.SampleRate || next.WaveFormat.Channels != WaveFormat.Channels)
            return false;

        lock (_lock)
        {
            replaced = _next;
            _next = next;
        }

        return true;
    }

    /// <summary>Removes the queued source, if any.</summary>
    /// <returns>The removed source, or <c>null</c> when nothing was queued.</returns>
    public ISampleProvider? ClearNext()
    {
        lock (_lock)
        {
            ISampleProvider? removed = _next;
            _next = null;

            return removed;
        }
    }

    /// <summary>Fills <paramref name="buffer"/>, moving to the queued source when the current one returns 0.</summary>
    public int Read(Span<float> buffer)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            ISampleProvider source = Current;
            int read = source.Read(buffer[total..]);

            if (read > 0)
            {
                total += read;
                continue;
            }

            if (!TrySwitch(source, out SourceSwitchedEventArgs? switched))
                break;

            SourceSwitched?.Invoke(this, switched);
        }

        return total;
    }

    private bool TrySwitch(ISampleProvider exhausted, [NotNullWhen(true)] out SourceSwitchedEventArgs? switched)
    {
        lock (_lock)
        {
            if (_next is null || !ReferenceEquals(_current, exhausted))
            {
                switched = null;
                return false;
            }

            switched = new SourceSwitchedEventArgs(_current, _next);
            _current = _next;
            _next = null;

            return true;
        }
    }
}
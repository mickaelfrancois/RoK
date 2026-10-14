using System.Diagnostics;

namespace Rok.Services.Diagnostics;

/// <summary>
/// Remembers where the last <see cref="NullReferenceException"/> or <see cref="ArgumentNullException"/>
/// was thrown. When the framework rethrows such an error from a bare HRESULT the unhandled
/// exception carries no managed stack, so the throw site seen here is the only application trace.
/// </summary>
public sealed class FirstChanceRecorder(TimeProvider timeProvider)
{
    public const int MaxFrames = 6;

    public const int MaxLength = 400;

    [ThreadStatic]
    private static bool _recording;

    private readonly object _lock = new();
    private long _timestamp;
    private string? _frames;

    /// <summary>
    /// Records the current call stack when <paramref name="exception"/> is a null-reference error.
    /// Meant for a first-chance handler, which runs on the throwing thread: never throws.
    /// </summary>
    public void Record(Exception exception)
    {
        if (exception is not (NullReferenceException or ArgumentNullException) || _recording)
            return;

        _recording = true;

        try
        {
            string frames = string.Join(" < ", new StackTrace(1, false)
                .GetFrames()
                .Select(frame => frame.GetMethod())
                .Where(method => method is not null)
                .Take(MaxFrames)
                .Select(method => $"{method!.DeclaringType?.Name}.{method.Name}"));

            lock (_lock)
            {
                _timestamp = timeProvider.GetTimestamp();
                _frames = frames;
            }
        }
        catch
        {
            // Diagnostics must never add an exception to the one being observed.
        }
        finally
        {
            _recording = false;
        }
    }

    /// <summary>
    /// Returns the last recorded throw site prefixed with its age in milliseconds, or
    /// <see langword="null"/> when nothing was recorded.
    /// </summary>
    public string? Snapshot()
    {
        FirstChanceSite? site = Last();

        if (site is null)
            return null;

        string entry = $"-{site.AgeMs} {site.Frames}";

        return entry.Length > MaxLength ? entry[..MaxLength] : entry;
    }

    /// <summary>
    /// Returns the last recorded throw site, or <see langword="null"/> when nothing was recorded.
    /// </summary>
    public FirstChanceSite? Last()
    {
        lock (_lock)
        {
            if (_frames is null)
                return null;

            return new FirstChanceSite((long)timeProvider.GetElapsedTime(_timestamp).TotalMilliseconds, _frames);
        }
    }
}
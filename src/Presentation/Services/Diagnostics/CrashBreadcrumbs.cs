namespace Rok.Services.Diagnostics;

/// <summary>
/// Short-lived trail of recent UI events, attached to the crash report because the framework
/// stack of a layout <c>COMException</c> carries no application frame.
/// </summary>
public interface ICrashBreadcrumbs
{
    /// <summary>
    /// Appends an entry. Safe to call from any thread.
    /// </summary>
    void Add(string category, string detail);

    /// <summary>
    /// Returns the retained entries, oldest first, each prefixed with its age in milliseconds.
    /// </summary>
    IReadOnlyList<string> Snapshot();
}

/// <summary>
/// Thread-safe ring of the last <see cref="Capacity"/> entries.
/// </summary>
public sealed class CrashBreadcrumbs(TimeProvider timeProvider) : ICrashBreadcrumbs
{
    public const int Capacity = 12;

    public const int MaxEntryLength = 80;

    private readonly object _lock = new();
    private readonly Queue<(long Timestamp, string Text)> _entries = new(Capacity);

    public void Add(string category, string detail)
    {
        string text = $"{category} {detail}";

        lock (_lock)
        {
            if (_entries.Count == Capacity)
                _entries.Dequeue();

            _entries.Enqueue((timeProvider.GetTimestamp(), text));
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock)
        {
            List<string> result = new(_entries.Count);

            foreach ((long timestamp, string text) in _entries)
            {
                long ageMs = (long)timeProvider.GetElapsedTime(timestamp).TotalMilliseconds;
                string entry = $"-{ageMs} {text}";

                result.Add(entry.Length > MaxEntryLength ? entry[..MaxEntryLength] : entry);
            }

            return result;
        }
    }
}
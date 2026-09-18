namespace Rok.Commons;

/// <summary>
/// Decides how many pending items to reveal on each display tick, and signals the
/// reveal threshold exactly once. Keeps the WelcomePage onboarding stream free of
/// UI dependencies so its pacing logic is unit-testable.
/// </summary>
/// <param name="batchSize">Maximum number of items drained per tick.</param>
/// <param name="unlockThreshold">Displayed count at which the app unlocks.</param>
public sealed class AlbumStreamPacer(int batchSize, int unlockThreshold)
{
    private bool _unlockSignaled;

    /// <summary>
    /// Dequeues up to <paramref name="batchSize"/> items from <paramref name="pending"/>.
    /// Returns an empty list when <paramref name="pending"/> is empty.
    /// </summary>
    public IReadOnlyList<T> DrainBatch<T>(Queue<T> pending)
    {
        if (pending.Count == 0)
            return [];

        int count = Math.Min(batchSize, pending.Count);
        List<T> batch = new(count);

        for (int i = 0; i < count; i++)
            batch.Add(pending.Dequeue());

        return batch;
    }

    /// <summary>
    /// Returns <see langword="true"/> the first time <paramref name="displayedCount"/>
    /// reaches <see cref="unlockThreshold"/>, and <see langword="false"/> on every
    /// subsequent call.
    /// </summary>
    public bool ShouldUnlock(int displayedCount)
    {
        if (_unlockSignaled || displayedCount < unlockThreshold)
            return false;

        _unlockSignaled = true;
        return true;
    }

    /// <summary>
    /// Import progress in percent, capped at 100.
    /// </summary>
    public static double ProgressPercent(int displayedCount, int unlockThreshold) =>
        Math.Min(displayedCount * 100.0 / unlockThreshold, 100);
}
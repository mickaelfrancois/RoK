namespace Rok.Commons;

/// <summary>
/// What the onboarding stream must do on a display tick.
/// </summary>
public enum AlbumStreamStep
{
    /// <summary>Nothing to reveal and no reason to leave yet.</summary>
    Idle,

    /// <summary>Reveal the next batch of pending items. The tick must not navigate.</summary>
    Reveal,

    /// <summary>Leave the onboarding. Returned exactly once, on a tick that reveals nothing.</summary>
    Leave,

    /// <summary>The onboarding is already left; ignore the tick.</summary>
    Done
}

/// <summary>
/// Decides what each display tick does and guarantees the onboarding is left exactly once.
/// A tick either reveals items or leaves, never both: navigating in the tick that has just
/// mutated the list would unload the page before the new containers were ever measured.
/// Keeps the WelcomePage onboarding stream free of UI dependencies so its pacing logic is
/// unit-testable. Not thread-safe: call it from the UI thread only.
/// </summary>
/// <param name="batchSize">Maximum number of items drained per tick.</param>
/// <param name="unlockThreshold">Displayed count at which the app unlocks.</param>
public sealed class AlbumStreamPacer(int batchSize, int unlockThreshold)
{
    private bool _leaveRequested;
    private bool _left;

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
    /// Decides the action of a tick, before anything is drained. <see cref="AlbumStreamStep.Leave"/>
    /// is returned once, when <paramref name="displayedCount"/> already reached the unlock
    /// threshold or a leave was requested; the tick that crosses the threshold only reveals.
    /// </summary>
    public AlbumStreamStep Next(int displayedCount, int pendingCount)
    {
        if (_left)
            return AlbumStreamStep.Done;

        if (_leaveRequested || displayedCount >= unlockThreshold)
        {
            _left = true;
            return AlbumStreamStep.Leave;
        }

        return pendingCount > 0 ? AlbumStreamStep.Reveal : AlbumStreamStep.Idle;
    }

    /// <summary>
    /// Asks the next tick to leave without revealing anything (end of the scan with tracks).
    /// </summary>
    public void RequestLeave() => _leaveRequested = true;

    /// <summary>
    /// Claims the exit when no tick will run. Returns <see langword="true"/> once; <see langword="false"/>
    /// if the onboarding was already left by a tick or an earlier claim.
    /// </summary>
    public bool TryLeaveNow()
    {
        if (_left)
            return false;

        _left = true;
        return true;
    }

    /// <summary>
    /// Import progress in percent, capped at 100.
    /// </summary>
    public static double ProgressPercent(int displayedCount, int unlockThreshold) =>
        Math.Min(displayedCount * 100.0 / unlockThreshold, 100);
}
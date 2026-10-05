namespace Rok.Services.Diagnostics;

/// <summary>
/// Phase of the frame navigation currently tracked for crash diagnostics.
/// </summary>
public enum NavigationPhase
{
    Idle,
    Navigating,
    Navigated,
    Loaded,
    Failed
}

/// <summary>
/// Immutable view of the navigation state at a given instant.
/// </summary>
/// <param name="CurrentPage">Page currently displayed, or <see langword="null"/> before the first navigation.</param>
/// <param name="PreviousPage">Page displayed before <paramref name="CurrentPage"/>.</param>
/// <param name="PendingPage">Target of a navigation that has started but not completed.</param>
/// <param name="Phase">Phase of the last navigation step.</param>
/// <param name="MsInPhase">Milliseconds spent in <paramref name="Phase"/> so far.</param>
public sealed record NavigationSnapshot(
    string? CurrentPage,
    string? PreviousPage,
    string? PendingPage,
    NavigationPhase Phase,
    long MsInPhase);

/// <summary>
/// Tracks the phase of frame navigation so a crash report can say whether the framework failed
/// inside <c>Frame.Navigate</c>, in the <c>Navigated</c> handlers, or once the page is stable.
/// Free of UI dependencies and thread-safe: crash handlers read it from any thread.
/// </summary>
public sealed class NavigationTrail(TimeProvider timeProvider)
{
    private readonly object _lock = new();

    private string? _currentPage;
    private string? _previousPage;
    private string? _pendingPage;
    private NavigationPhase _phase = NavigationPhase.Idle;
    private long _phaseStartedAt = timeProvider.GetTimestamp();

    /// <summary>
    /// Records that a navigation towards <paramref name="targetPage"/> has started.
    /// </summary>
    public void OnNavigating(string targetPage)
    {
        lock (_lock)
        {
            _pendingPage = targetPage;
            EnterPhase(NavigationPhase.Navigating);
        }
    }

    /// <summary>
    /// Records that the frame has finished navigating to <paramref name="page"/>.
    /// </summary>
    public void OnNavigated(string? page)
    {
        lock (_lock)
        {
            _previousPage = _currentPage;
            _currentPage = page;
            _pendingPage = null;
            EnterPhase(NavigationPhase.Navigated);
        }
    }

    /// <summary>
    /// Records that <paramref name="page"/> has fired <c>Loaded</c>. A page that is no longer the
    /// current one (a stale event from a replaced page) is ignored.
    /// </summary>
    public void OnLoaded(string page)
    {
        lock (_lock)
        {
            if (_currentPage != page)
                return;

            EnterPhase(NavigationPhase.Loaded);
        }
    }

    /// <summary>
    /// Records that the navigation towards <paramref name="page"/> failed. The current page is kept.
    /// </summary>
    public void OnFailed(string page)
    {
        lock (_lock)
        {
            _pendingPage = null;
            EnterPhase(NavigationPhase.Failed);
        }
    }

    /// <summary>
    /// Returns a consistent copy of the current state.
    /// </summary>
    public NavigationSnapshot Snapshot()
    {
        lock (_lock)
        {
            long elapsedMs = (long)timeProvider.GetElapsedTime(_phaseStartedAt).TotalMilliseconds;

            return new NavigationSnapshot(_currentPage, _previousPage, _pendingPage, _phase, elapsedMs);
        }
    }

    /// <summary>
    /// True when navigating to <paramref name="targetPage"/> would only rebuild the page already
    /// displayed: same page, no parameter and no navigation in flight.
    /// </summary>
    public bool IsRedundant(string targetPage, bool hasParameter)
    {
        lock (_lock)
        {
            return !hasParameter && _pendingPage is null && _currentPage == targetPage;
        }
    }

    private void EnterPhase(NavigationPhase phase)
    {
        _phase = phase;
        _phaseStartedAt = timeProvider.GetTimestamp();
    }
}
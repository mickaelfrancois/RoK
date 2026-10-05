namespace Rok.Services.Diagnostics;

/// <summary>
/// Builds the flat property bag of the <c>crash</c> telemetry event. Pure and exception-free:
/// it runs inside an unhandled-exception handler and must never throw.
/// </summary>
public static class CrashContextBuilder
{
    private const string Unknown = "unknown";

    /// <summary>
    /// Keeps the four historical keys (<c>currentPage</c>, <c>previousPage</c>, <c>exceptionType</c>,
    /// <c>hresult</c>) unchanged and adds the navigation phase, the pending page, the time spent in
    /// the phase and the recent breadcrumbs.
    /// </summary>
    public static Dictionary<string, object> Build(
        Exception exception,
        NavigationSnapshot? navigation,
        IReadOnlyList<string> breadcrumbs)
    {
        return new Dictionary<string, object>
        {
            ["currentPage"] = navigation?.CurrentPage ?? Unknown,
            ["previousPage"] = navigation?.PreviousPage ?? Unknown,
            ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
            ["hresult"] = exception.HResult,
            ["navigationPhase"] = navigation is null ? Unknown : navigation.Phase.ToString().ToLowerInvariant(),
            ["pendingPage"] = navigation?.PendingPage ?? Unknown,
            ["msInPhase"] = navigation?.MsInPhase ?? 0L,
            ["breadcrumbs"] = breadcrumbs.Count == 0 ? Unknown : string.Join(" | ", breadcrumbs)
        };
    }
}
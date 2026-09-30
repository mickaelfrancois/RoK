namespace Rok.Infrastructure.Player.Output;

/// <summary>
/// Runs code without a <see cref="SynchronizationContext"/>. A <c>WasapiPlayer</c> built under the UI context would raise
/// <c>PlaybackStopped</c> through the UI thread; built without one, it raises the event on its own render thread, which
/// the player handlers hand off to the thread pool.
/// </summary>
internal static class SynchronizationContextScope
{
    public static T RunDetached<T>(Func<T> action)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);

        try
        {
            return action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
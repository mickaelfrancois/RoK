namespace Rok.Application.Tag;

/// <summary>
/// Restores files left in an inconsistent state by an interrupted tag write.
/// </summary>
public interface ITagWriteRecovery
{
    /// <summary>
    /// Replays the journal of pending writes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecoverAsync(CancellationToken cancellationToken);
}
namespace Rok.Application.Tag;

/// <summary>
/// Writes tag changes into audio files.
/// </summary>
public interface ITagWriter
{
    /// <summary>
    /// Writes the files one by one. Cancellation is checked between two files, never during one.
    /// </summary>
    /// <param name="requests">The files to write with their changes.</param>
    /// <param name="cancellationToken">Token observed between two files.</param>
    /// <returns>One result per request.</returns>
    Task<IReadOnlyList<TagWriteResult>> WriteAsync(IReadOnlyList<TagWriteRequest> requests, CancellationToken cancellationToken);
}
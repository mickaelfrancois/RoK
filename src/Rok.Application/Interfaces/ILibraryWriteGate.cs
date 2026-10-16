using System.Diagnostics.CodeAnalysis;

namespace Rok.Application.Interfaces;

/// <summary>
/// Mutual exclusion between a library import and a tag write.
/// </summary>
public interface ILibraryWriteGate
{
    /// <summary>
    /// Tries to enter the gate without waiting.
    /// </summary>
    /// <param name="lease">Lease to dispose to leave the gate, when entered.</param>
    /// <returns>True when entered, false when the gate is held.</returns>
    bool TryEnter([NotNullWhen(true)] out IDisposable? lease);

    /// <summary>
    /// Waits for the gate and enters it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lease to dispose to leave the gate.</returns>
    Task<IDisposable> EnterAsync(CancellationToken cancellationToken);
}
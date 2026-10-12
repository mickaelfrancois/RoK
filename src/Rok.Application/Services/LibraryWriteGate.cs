using Rok.Application.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Rok.Application.Services;

/// <inheritdoc />
public sealed class LibraryWriteGate : ILibraryWriteGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <inheritdoc />
    public bool TryEnter([NotNullWhen(true)] out IDisposable? lease)
    {
        if (_semaphore.Wait(0))
        {
            lease = new Lease(_semaphore);

            return true;
        }

        lease = null;

        return false;
    }

    /// <inheritdoc />
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        return new Lease(_semaphore);
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
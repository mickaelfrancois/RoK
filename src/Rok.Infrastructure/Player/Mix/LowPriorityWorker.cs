using System.Threading.Channels;

namespace Rok.Infrastructure.Player.Mix;

/// <summary>Runs jobs one at a time on a dedicated background thread of below-normal priority.</summary>
public sealed class LowPriorityWorker : IDisposable
{
    private readonly Channel<Action> _queue = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Thread _thread;

    /// <summary>Initializes a new instance of the <see cref="LowPriorityWorker"/> class and starts its thread.</summary>
    public LowPriorityWorker()
    {
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "Rok.MixAnalysis"
        };
        _thread.Start();
    }

    /// <summary>Queues a job. A job cancelled before its turn is skipped.</summary>
    /// <typeparam name="T">Result type of the job.</typeparam>
    /// <param name="job">Job to run on the worker thread.</param>
    /// <param name="ct">Cancellation token passed to the job.</param>
    public Task<T> RunAsync<T>(Func<CancellationToken, T> job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (ct.IsCancellationRequested)
            return Task.FromCanceled<T>(ct);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => completion.TrySetCanceled(ct));

        _ = completion.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        var queued = _queue.Writer.TryWrite(() =>
        {
            if (ct.IsCancellationRequested)
            {
                completion.TrySetCanceled(ct);
                return;
            }

            try
            {
                completion.TrySetResult(job(ct));
            }
            catch (OperationCanceledException ex)
            {
                completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        if (!queued)
            completion.TrySetException(new ObjectDisposedException(nameof(LowPriorityWorker)));

        return completion.Task;
    }

    /// <inheritdoc />
    public void Dispose() => _queue.Writer.TryComplete();

    private void Loop()
    {
        while (_queue.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (_queue.Reader.TryRead(out var work))
                work();
        }
    }
}
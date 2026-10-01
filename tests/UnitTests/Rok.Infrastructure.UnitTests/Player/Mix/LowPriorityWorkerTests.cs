using Rok.Infrastructure.Player.Mix;

namespace Rok.Infrastructure.UnitTests.Player.Mix;

public class LowPriorityWorkerTests
{
    [Fact(DisplayName = "worker_runs_jobs_on_a_below_normal_background_thread")]
    public async Task WorkerRunsJobsOnABelowNormalBackgroundThread()
    {
        // Arrange
        using var worker = new LowPriorityWorker();
        var callerThreadId = Environment.CurrentManagedThreadId;

        // Act
        var result = await worker.RunAsync(
            _ =>
            {
                var thread = Thread.CurrentThread;

                return (thread.Priority, thread.IsBackground, thread.ManagedThreadId);
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(ThreadPriority.BelowNormal, result.Priority);
        Assert.True(result.IsBackground);
        Assert.NotEqual(callerThreadId, result.ManagedThreadId);
    }

    [Fact(DisplayName = "cancelled_job_is_skipped")]
    public async Task CancelledJobIsSkipped()
    {
        // Arrange
        using var worker = new LowPriorityWorker();
        using var gate = new ManualResetEventSlim();
        using var cts = new CancellationTokenSource();
        var executed = false;
        var blocker = worker.RunAsync(_ => gate.Wait(TimeSpan.FromSeconds(10)), CancellationToken.None);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            var skipped = worker.RunAsync(
                _ =>
                {
                    executed = true;

                    return 0;
                },
                cts.Token);

            await cts.CancelAsync();
            gate.Set();
            await skipped;
        });

        await blocker;

        // Assert
        Assert.False(executed);
    }

    [Fact(DisplayName = "job_cancelled_while_another_runs_completes_cancelled_without_waiting")]
    public async Task JobCancelledWhileAnotherRunsCompletesCancelledWithoutWaiting()
    {
        // Arrange
        using var worker = new LowPriorityWorker();
        using var gate = new ManualResetEventSlim();
        using var cts = new CancellationTokenSource();
        var blocker = worker.RunAsync(_ => gate.Wait(TimeSpan.FromSeconds(10)), CancellationToken.None);
        var queued = worker.RunAsync(_ => 0, cts.Token);

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(blocker.IsCompleted);

        gate.Set();
        await blocker;
    }
}
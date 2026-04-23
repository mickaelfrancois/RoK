using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Rok;
using WinRT;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey("rok-main");

        if (!mainInstance.IsCurrent)
        {
            RedirectActivation(mainInstance);
            return;
        }

        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
    }

    private static void RedirectActivation(AppInstance mainInstance)
    {
        AppActivationArguments activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        using ManualResetEventSlim redirectDone = new(false);

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                await mainInstance.RedirectActivationToAsync(activatedArgs);
            }
            finally
            {
                redirectDone.Set();
            }
        });

        redirectDone.Wait();
    }
}
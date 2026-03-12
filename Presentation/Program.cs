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

        AppInstance mainInstance = AppInstance.FindOrRegisterForKey("rok-main");

        if (!mainInstance.IsCurrent)
        {
            AppActivationArguments activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            Task.Run(async () => await mainInstance.RedirectActivationToAsync(activatedArgs)).Wait();
            return;
        }

        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
    }
}
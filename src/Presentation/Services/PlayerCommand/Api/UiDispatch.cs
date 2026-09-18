namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Marshals web API work back onto the UI thread. Every read of, or command against, the player has to run
/// there: the player state is bound to the WinUI tree, and mutating it from a listener thread raises the
/// cross-thread <c>COMException</c> the app has already been bitten by.
/// </summary>
/// <remarks>
/// The three entry points are named apart on purpose: overloads taking <c>Func&lt;T&gt;</c> and
/// <c>Func&lt;Task&lt;T&gt;&gt;</c> are mutually ambiguous for any lambda calling an async method.
/// </remarks>
internal static class UiDispatch
{
    /// <summary>Runs a synchronous projection on the UI thread and awaits its value.</summary>
    public static Task<T> ReadAsync<T>(Action<Action> dispatch, Func<T> projection)
    {
        TaskCompletionSource<T> completion = new();

        dispatch(() =>
        {
            try { completion.SetResult(projection()); }
            catch (Exception ex) { completion.SetException(ex); }
        });

        return completion.Task;
    }


    /// <summary>Starts an asynchronous operation on the UI thread and awaits its value.</summary>
    public static Task<T> RunTaskAsync<T>(Action<Action> dispatch, Func<Task<T>> operation)
    {
        TaskCompletionSource<T> completion = new();

        dispatch(async () =>
        {
            try { completion.SetResult(await operation()); }
            catch (Exception ex) { completion.SetException(ex); }
        });

        return completion.Task;
    }


    /// <summary>Runs a command on the UI thread and awaits its completion.</summary>
    public static Task InvokeAsync(Action<Action> dispatch, Action command) =>
        ReadAsync(dispatch, () => { command(); return true; });
}
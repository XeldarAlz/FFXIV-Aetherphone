namespace Aetherphone.Core.Aethernet;

internal sealed class StoreWork : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly string logTag;

    public StoreWork(string logTag)
    {
        this.logTag = logTag;
    }

    public CancellationToken Token => cancellation.Token;

    public void Run(string operation, Func<CancellationToken, Task> action, Action? cleanup = null)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await action(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[{logTag}] {operation} failed: {exception.Message}");
            }
            finally
            {
                cleanup?.Invoke();
            }
        });
    }

    public void Run(
        string operation,
        Func<CancellationToken, Task<bool>> action,
        Action<bool> onComplete,
        Action? cleanup = null)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await action(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[{logTag}] {operation} failed: {exception.Message}");
            }
            finally
            {
                cleanup?.Invoke();
                onComplete(succeeded);
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

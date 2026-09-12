namespace Aetherphone.Core.Media;

internal static class DeferredDispose
{
    // A texture can still be referenced by the draw list of the frame that replaced it, so the
    // handle is released a beat later, on the framework thread.
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    public static void Later(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        _ = Task.Delay(Delay)
            .ContinueWith(_ => Plugin.Framework.RunOnFrameworkThread(disposable.Dispose), TaskScheduler.Default);
    }
}

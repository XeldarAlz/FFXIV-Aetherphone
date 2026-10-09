namespace Aetherphone.Core.Notifications;

internal readonly struct TapSoundScope : IDisposable
{
    private readonly UiSound previous;

    public TapSoundScope(UiSound previous)
    {
        this.previous = previous;
    }

    public void Dispose() => UiFeedback.RestoreTap(previous);
}

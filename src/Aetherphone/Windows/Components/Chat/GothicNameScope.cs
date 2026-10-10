namespace Aetherphone.Windows.Components;

internal readonly struct GothicNameScope : IDisposable
{
    private readonly bool previous;

    public GothicNameScope(bool previous)
    {
        this.previous = previous;
    }

    public void Dispose() => UserName.RestoreGothic(previous);
}

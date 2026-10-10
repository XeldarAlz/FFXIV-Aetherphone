namespace Aetherphone.Windows.Components;

internal readonly struct NameStyleScope : IDisposable
{
    private readonly bool previousGothic;

    public NameStyleScope(bool previousGothic)
    {
        this.previousGothic = previousGothic;
    }

    public void Dispose() => UserName.Restore(previousGothic);
}

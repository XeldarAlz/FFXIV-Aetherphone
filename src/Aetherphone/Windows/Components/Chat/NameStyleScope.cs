namespace Aetherphone.Windows.Components;

internal readonly struct NameStyleScope : IDisposable
{
    private readonly bool previousGothic;
    private readonly string previousDisplay;
    private readonly string previousHandle;

    public NameStyleScope(bool previousGothic, string previousDisplay, string previousHandle)
    {
        this.previousGothic = previousGothic;
        this.previousDisplay = previousDisplay;
        this.previousHandle = previousHandle;
    }

    public void Dispose() => UserName.Restore(previousGothic, previousDisplay, previousHandle);
}

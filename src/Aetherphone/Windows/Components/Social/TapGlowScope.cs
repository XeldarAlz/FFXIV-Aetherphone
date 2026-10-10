namespace Aetherphone.Windows.Components;

internal readonly struct TapGlowScope : IDisposable
{
    private readonly bool previousEnabled;
    private readonly Vector4 previousInk;

    public TapGlowScope(bool previousEnabled, Vector4 previousInk)
    {
        this.previousEnabled = previousEnabled;
        this.previousInk = previousInk;
    }

    public void Dispose() => TapGlow.Restore(previousEnabled, previousInk);
}

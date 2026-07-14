namespace Aetherphone.Core;

internal sealed class PhoneVisibility
{
    private Func<bool>? probe;

    public bool IsVisible => probe is not null && probe();

    public void Bind(Func<bool> source)
    {
        probe = source;
    }
}

namespace Aetherphone.Core.Runtime;

internal sealed class PhoneVisibility
{
    private Func<bool>? probe;
    private Func<Rect>? frameProbe;

    public bool IsVisible => probe is not null && probe();

    public void Bind(Func<bool> source, Func<Rect>? frame = null)
    {
        probe = source;
        frameProbe = frame;
    }

    public bool TryGetFrame(out Rect frame)
    {
        if (frameProbe is null)
        {
            frame = default;
            return false;
        }

        frame = frameProbe();
        return frame.Width > 0f && frame.Height > 0f;
    }
}

using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Animation;

internal struct NotificationShake
{
    private readonly float duration;
    private readonly float frequency;
    private readonly float amplitude;
    private float remaining;

    public NotificationShake(float duration, float frequency, float amplitude)
    {
        this.duration = duration;
        this.frequency = frequency;
        this.amplitude = amplitude;
        remaining = 0f;
    }

    public void Trigger()
    {
        remaining = duration;
    }

    public float Advance(float delta)
    {
        if (remaining <= 0f)
        {
            return 0f;
        }

        remaining = MathF.Max(0f, remaining - delta);
        var falloff = remaining / duration;
        return MathF.Sin(remaining * frequency) * amplitude * ImGuiHelpers.GlobalScale * falloff;
    }
}

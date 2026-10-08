using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class DailySpinIdle
{
    private const float TurnRate = 0.3f;

    private float angle;
    private float phase;

    public void Draw(ImDrawListPtr drawList, Rect rect, float deltaSeconds, float scale)
    {
        angle = WheelChoreography.Normalize(angle + deltaSeconds * TurnRate);
        phase += deltaSeconds;
        var head = SpinRingArt.RimInset * scale;
        var rim = SpinRingArt.RimInset * 0.6f * scale + CasinoLights.BulbDiameter * scale;
        var radius = MathF.Min(rect.Width * 0.5f, (rect.Height - head) * 0.5f) - rim;
        if (radius <= 1f)
        {
            return;
        }

        var center = new Vector2(rect.Center.X, rect.Center.Y + head * 0.5f);
        SpinRingArt.DrawRim(drawList, center, radius, phase, 0.8f, scale);
        SpinRingArt.Draw(drawList, center, radius, angle, -1, 0f, scale);
        SpinRingArt.DrawPointer(drawList, center, radius, 0f, scale);
    }
}

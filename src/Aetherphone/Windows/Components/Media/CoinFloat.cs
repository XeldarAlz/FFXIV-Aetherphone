using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class CoinFloat
{
    private const float LifeSeconds = 1.7f;
    private const float RiseDistance = 48f;
    private const float DotCount = 6f;
    private const float DotDrift = 26f;
    private const float TextScale = 1.08f;

    private struct Floater
    {
        public string Text;
        public Vector2 Origin;
        public float Age;
        public bool Muted;
        public bool Active;
    }

    private readonly Floater[] slots = new Floater[4];

    public void Spawn(string text, Vector2 origin, bool muted = false)
    {
        var oldest = 0;
        for (var index = 0; index < slots.Length; index++)
        {
            if (!slots[index].Active)
            {
                oldest = index;
                break;
            }

            if (slots[index].Age > slots[oldest].Age)
            {
                oldest = index;
            }
        }

        slots[oldest] = new Floater { Text = text, Origin = origin, Age = 0f, Muted = muted, Active = true };
    }

    public void Draw(ImDrawListPtr drawList, Vector4 accent, Vector4 mutedInk, float delta)
    {
        var scale = UiScale.Current;
        for (var index = 0; index < slots.Length; index++)
        {
            if (!slots[index].Active)
            {
                continue;
            }

            slots[index].Age += delta;
            var age = slots[index].Age;
            if (age >= LifeSeconds)
            {
                slots[index].Active = false;
                continue;
            }

            var progress = age / LifeSeconds;
            var rise = progress * RiseDistance * scale;
            var alpha = 1f - progress;
            var position = slots[index].Origin - new Vector2(0f, rise);
            var ink = slots[index].Muted ? mutedInk : accent;

            var size = Typography.Measure(slots[index].Text, TextScale, FontWeight.Bold);
            Typography.Draw(drawList, new Vector2(position.X - size.X * 0.5f, position.Y - size.Y * 0.5f),
                slots[index].Text, Palette.WithAlpha(ink, alpha), TextScale, FontWeight.Bold);

            if (slots[index].Muted)
            {
                continue;
            }

            var spread = MathF.Min(1f, progress * 1.6f) * DotDrift * scale;
            for (var dot = 0; dot < (int)DotCount; dot++)
            {
                var angle = dot * (MathF.Tau / DotCount) - MathF.PI * 0.5f;
                var dotCenter = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f) * spread;
                drawList.AddCircleFilled(dotCenter, 2.2f * scale * (1f - progress * 0.5f),
                    ImGui.GetColorU32(Palette.WithAlpha(accent, alpha * 0.65f)), 10);
            }
        }
    }
}

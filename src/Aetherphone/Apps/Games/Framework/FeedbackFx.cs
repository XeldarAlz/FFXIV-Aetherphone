using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class FeedbackFx
{
    private const float TraumaDecay = 1.7f;
    private const float MaxShake = 11f;
    private const int FloatCapacity = 32;
    private const int RingCapacity = 12;

    private struct FloatText
    {
        public string Text;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;
        public Vector4 Color;
        public FontWeight Weight;
    }

    private struct Ring
    {
        public Vector2 Center;
        public float Life;
        public float MaxLife;
        public float FromRadius;
        public float ToRadius;
        public float Thickness;
        public Vector4 Color;
    }

    private readonly FloatText[] floats = new FloatText[FloatCapacity];
    private readonly Ring[] rings = new Ring[RingCapacity];
    private readonly Random random = new();
    private int activeFloats;
    private int activeRings;
    private float trauma;
    private float flashAlpha;
    private float freezeSeconds;
    private Vector4 flashColor;

    public void Clear()
    {
        activeFloats = 0;
        activeRings = 0;
        trauma = 0f;
        flashAlpha = 0f;
        freezeSeconds = 0f;
    }

    public void AddTrauma(float amount)
    {
        trauma = MathF.Min(1f, trauma + amount);
    }

    public void HitStop(float seconds)
    {
        freezeSeconds = MathF.Max(freezeSeconds, seconds);
    }

    public float ScaleDelta(float deltaSeconds)
    {
        if (freezeSeconds <= 0f)
        {
            return deltaSeconds;
        }

        freezeSeconds -= deltaSeconds;
        return 0f;
    }

    public void Flash(Vector4 color, float alpha)
    {
        flashColor = color;
        flashAlpha = MathF.Max(flashAlpha, alpha);
    }

    public void Shockwave(Vector2 center, float toRadius, Vector4 color, float life = 0.5f, float thickness = 3f,
        float fromRadius = 0f)
    {
        if (activeRings >= RingCapacity)
        {
            return;
        }

        ref var ring = ref rings[activeRings];
        ring.Center = center;
        ring.MaxLife = life;
        ring.Life = life;
        ring.FromRadius = fromRadius;
        ring.ToRadius = toRadius;
        ring.Thickness = thickness;
        ring.Color = color;
        activeRings++;
    }

    public void AddText(string text, Vector2 position, Vector4 color, float scale = 1f, float rise = 46f,
        FontWeight weight = FontWeight.Bold)
    {
        if (activeFloats >= FloatCapacity)
        {
            return;
        }

        ref var entry = ref floats[activeFloats];
        entry.Text = text;
        entry.Position = position;
        entry.Velocity = new Vector2(((float)random.NextDouble() - 0.5f) * 18f, -rise);
        entry.MaxLife = 0.9f;
        entry.Life = entry.MaxLife;
        entry.Scale = scale;
        entry.Color = color;
        entry.Weight = weight;
        activeFloats++;
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        trauma = MathF.Max(0f, trauma - TraumaDecay * deltaSeconds);
        flashAlpha = MathF.Max(0f, flashAlpha - deltaSeconds * 3.2f);
        for (var index = activeFloats - 1; index >= 0; index--)
        {
            ref var entry = ref floats[index];
            entry.Life -= deltaSeconds;
            if (entry.Life <= 0f)
            {
                floats[index] = floats[activeFloats - 1];
                activeFloats--;
                continue;
            }

            entry.Position += entry.Velocity * deltaSeconds;
            entry.Velocity.Y *= MathF.Max(0f, 1f - 1.1f * deltaSeconds);
        }

        for (var index = activeRings - 1; index >= 0; index--)
        {
            ref var ring = ref rings[index];
            ring.Life -= deltaSeconds;
            if (ring.Life <= 0f)
            {
                rings[index] = rings[activeRings - 1];
                activeRings--;
            }
        }
    }

    public Vector2 ShakeOffset(float scale)
    {
        if (trauma <= 0f)
        {
            return Vector2.Zero;
        }

        var magnitude = trauma * trauma * MaxShake * scale;
        var x = ((float)random.NextDouble() * 2f - 1f) * magnitude;
        var y = ((float)random.NextDouble() * 2f - 1f) * magnitude;
        return new Vector2(x, y);
    }

    public void DrawFlash(ImDrawListPtr drawList, Rect area, float rounding)
    {
        if (flashAlpha <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(flashColor with { W = flashColor.W * flashAlpha }),
            rounding);
    }

    public void DrawRings(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < activeRings; index++)
        {
            ref readonly var ring = ref rings[index];
            var progress = 1f - ring.Life / ring.MaxLife;
            var eased = Easing.EaseOutCubic(progress);
            var radius = ring.FromRadius + (ring.ToRadius - ring.FromRadius) * eased;
            var alpha = (1f - progress) * ring.Color.W;
            var thickness = MathF.Max(1f, ring.Thickness * scale * (1f - progress * 0.6f));
            drawList.AddCircle(ring.Center, radius, ImGui.GetColorU32(ring.Color with { W = alpha }), 0, thickness);
        }
    }

    public void DrawText()
    {
        for (var index = 0; index < activeFloats; index++)
        {
            ref readonly var entry = ref floats[index];
            var fade = entry.Life / entry.MaxLife;
            var alpha = fade > 0.6f ? 1f : fade / 0.6f;
            var pop = entry.Life > entry.MaxLife - 0.12f ? 1.18f : 1f;
            Typography.DrawCentered(entry.Position, entry.Text, entry.Color with { W = entry.Color.W * alpha },
                entry.Scale * pop, entry.Weight);
        }
    }
}

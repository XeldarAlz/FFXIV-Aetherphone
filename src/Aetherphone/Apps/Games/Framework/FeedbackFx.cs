using System;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class FeedbackFx
{
    private const float TraumaDecay = 1.7f;
    private const float MaxShake = 11f;
    private const int FloatCapacity = 32;

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

    private readonly FloatText[] floats = new FloatText[FloatCapacity];
    private readonly Random random = new();
    private int activeFloats;

    private float trauma;
    private float flashAlpha;
    private Vector4 flashColor;

    public void Clear()
    {
        activeFloats = 0;
        trauma = 0f;
        flashAlpha = 0f;
    }

    public void AddTrauma(float amount)
    {
        trauma = MathF.Min(1f, trauma + amount);
    }

    public void Flash(Vector4 color, float alpha)
    {
        flashColor = color;
        flashAlpha = MathF.Max(flashAlpha, alpha);
    }

    public void AddText(string text, Vector2 position, Vector4 color, float scale = 1f, float rise = 46f, FontWeight weight = FontWeight.Bold)
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

        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(flashColor with { W = flashColor.W * flashAlpha }), rounding);
    }

    public void DrawText()
    {
        for (var index = 0; index < activeFloats; index++)
        {
            ref readonly var entry = ref floats[index];
            var fade = entry.Life / entry.MaxLife;
            var alpha = fade > 0.6f ? 1f : fade / 0.6f;
            var pop = entry.Life > entry.MaxLife - 0.12f ? 1.18f : 1f;
            Typography.DrawCentered(entry.Position, entry.Text, entry.Color with { W = entry.Color.W * alpha }, entry.Scale * pop, entry.Weight);
        }
    }
}

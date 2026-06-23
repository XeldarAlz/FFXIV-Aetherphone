using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Lodestone;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AvatarView
{
    private const float FadeDurationSeconds = 0.34f;
    private const float PulsePeriodSeconds = 1.15f;
    private const float PulseFloor = 0.72f;
    private const float SettleOvershoot = 0.06f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<string, float> fadeByKey = new(StringComparer.Ordinal);

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 baseColor, string monogram, float monogramScale, AvatarHandle handle, int segments)
    {
        var circleColor = handle.State == AvatarLoadState.Loading ? Pulse(baseColor) : baseColor;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(circleColor), segments);

        var fade = handle.Texture is not null && handle.Key.Length > 0 ? Advance(handle.Key) : 0f;

        if (fade < 1f)
        {
            Typography.DrawCentered(center, monogram, White with { W = 1f - fade }, monogramScale);
        }

        if (handle.Texture is { } texture && fade > 0f)
        {
            var settledRadius = radius * (1f + SettleOvershoot * (1f - fade));
            var corner = new Vector2(settledRadius, settledRadius);
            var tint = ((uint)(fade * 255f) << 24) | 0x00FFFFFFu;
            drawList.AddImageRounded(texture.Handle, center - corner, center + corner, Vector2.Zero, Vector2.One, tint, settledRadius);
        }
    }

    private static Vector4 Pulse(Vector4 color)
    {
        var milliseconds = Environment.TickCount64 % (long)(PulsePeriodSeconds * 1000f);
        var phase = milliseconds / (PulsePeriodSeconds * 1000f);
        var wave = Easing.SmoothStep(0.5f + 0.5f * MathF.Sin(phase * MathF.PI * 2f));
        var brightness = PulseFloor + (1f - PulseFloor) * wave;
        return new Vector4(color.X * brightness, color.Y * brightness, color.Z * brightness, color.W);
    }

    private static float Advance(string key)
    {
        fadeByKey.TryGetValue(key, out var progress);
        if (progress < 1f)
        {
            progress = MathF.Min(1f, progress + ImGui.GetIO().DeltaTime / FadeDurationSeconds);
            fadeByKey[key] = progress;
        }

        return Easing.EaseOutCubic(progress);
    }
}

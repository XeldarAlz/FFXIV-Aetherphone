using Aetherphone.Core;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Fishing;

internal static class FishingArt
{
    public const float CardRadius = 22f;
    public const float CardPadding = 16f;
    public const float CardGap = 14f;
    private const float CapsulePadX = 8f;
    private const float CapsulePadY = 3f;
    private const float ChevronStroke = 2f;
    private const float BarTrackAlpha = 0.12f;
    private const float DayBarMarkerWidth = 2f;
    private const float DayBarTickAlpha = 0.35f;
    private const int DayBarTicks = 4;

    public static void TimeTile(ImDrawListPtr drawList, Vector2 center, float size, OceanTimeOfDay timeOfDay)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var surface = IconTile.Surface(FishingText.TimeOfDayTint(timeOfDay));
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor, surface);
        ProgressRing.CenterIcon(drawList, center, FishingText.TimeOfDayIcon(timeOfDay), AccentRing.Ink, size * 0.5f);
    }

    public static bool ItemIcon(ImDrawListPtr drawList, ITextureProvider textures, uint iconId, Vector2 min,
        float size, float scale, Vector4 fallback)
    {
        var max = min + new Vector2(size, size);
        var radius = size * Metrics.Radius.TileFactor;
        if (GameIconTile.Draw(drawList, textures, iconId, min, max, radius, scale, edgeStroke: true,
                requireIcon: true))
        {
            return true;
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fallback));
        return false;
    }

    public static float Capsule(ImDrawListPtr drawList, Vector2 min, string text, Vector4 fill, Vector4 ink,
        float scale)
    {
        var size = Typography.Measure(text, TextStyles.Caption1);
        var padX = CapsulePadX * scale;
        var padY = CapsulePadY * scale;
        var max = min + new Vector2(size.X + padX * 2f, size.Y + padY * 2f);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(drawList, min + new Vector2(padX, padY), text, ink, TextStyles.Caption1);
        return max.X - min.X;
    }

    public static float CapsuleHeight(float scale) =>
        Typography.LineHeight(TextStyles.Caption1) + CapsulePadY * 2f * scale;

    public static void Chevron(ImDrawListPtr drawList, Vector2 tip, float size, Vector4 color, float scale)
    {
        var packed = ImGui.GetColorU32(color);
        var stroke = ChevronStroke * scale;
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, stroke);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y + size), tip, packed, stroke);
    }

    public static void Progress(ImDrawListPtr drawList, Rect bar, float fraction, Vector4 track, Vector4 fill)
    {
        var radius = bar.Height * 0.5f;
        Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(track, BarTrackAlpha)));
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var fillMax = new Vector2(bar.Min.X + MathF.Max(bar.Height, bar.Width * clamped), bar.Max.Y);
        Squircle.Fill(drawList, bar.Min, fillMax, radius, ImGui.GetColorU32(fill));
    }

    public static void DayBar(ImDrawListPtr drawList, Rect bar, in FishWindowRule rule, int nowMinute, Vector4 track,
        Vector4 fill, Vector4 marker, float scale)
    {
        var radius = bar.Height * 0.5f;
        Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(track, BarTrackAlpha)));
        var packedFill = ImGui.GetColorU32(fill);
        if (rule.AllDay)
        {
            Squircle.Fill(drawList, bar.Min, bar.Max, radius, packedFill);
        }
        else if (rule.StartMinute < rule.EndMinute)
        {
            FillSpan(drawList, bar, rule.StartMinute, rule.EndMinute, packedFill);
        }
        else
        {
            FillSpan(drawList, bar, 0, rule.EndMinute, packedFill);
            FillSpan(drawList, bar, rule.StartMinute, FishWindowRule.MinutesPerDay, packedFill);
        }

        var tickColor = ImGui.GetColorU32(Palette.WithAlpha(track, DayBarTickAlpha));
        for (var tick = 1; tick < DayBarTicks; tick++)
        {
            var x = bar.Min.X + bar.Width * tick / DayBarTicks;
            drawList.AddLine(new Vector2(x, bar.Max.Y + 2f * scale), new Vector2(x, bar.Max.Y + 5f * scale),
                tickColor, 1f);
        }

        var markerX = bar.Min.X + bar.Width * nowMinute / FishWindowRule.MinutesPerDay;
        var half = DayBarMarkerWidth * scale * 0.5f;
        drawList.AddRectFilled(new Vector2(markerX - half, bar.Min.Y - 3f * scale),
            new Vector2(markerX + half, bar.Max.Y + 3f * scale), ImGui.GetColorU32(marker), half);
    }

    public static Rect Pressed(Rect rect, uint key, bool hovered)
    {
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(key, down, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private static void FillSpan(ImDrawListPtr drawList, Rect bar, int startMinute, int endMinute, uint color)
    {
        if (endMinute <= startMinute)
        {
            return;
        }

        var left = bar.Min.X + bar.Width * startMinute / FishWindowRule.MinutesPerDay;
        var right = bar.Min.X + bar.Width * endMinute / FishWindowRule.MinutesPerDay;
        var radius = MathF.Min(bar.Height * 0.5f, (right - left) * 0.5f);
        Squircle.Fill(drawList, new Vector2(left, bar.Min.Y), new Vector2(right, bar.Max.Y), radius, color);
    }
}

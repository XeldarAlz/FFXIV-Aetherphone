using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class PhaseRibbon
{
    private const float SideInset = 12f;
    private const float VerticalInset = 4f;
    private const float RingRadius = 12f;
    private const float AvatarRadius = 7f;
    private const float AvatarStep = 9f;
    private const int MaxAvatars = 3;

    private static readonly Vector4[] AvatarTints =
    {
        CasinoColors.LightA,
        CasinoColors.LightB,
        CasinoColors.Money,
    };

    public static void Draw(ImDrawListPtr drawList, Rect rect, string label, long remainingMs, int windowSeconds,
        int crowd, Vector4 accent, float scale)
    {
        if (rect.Height <= 0f)
        {
            return;
        }

        var min = new Vector2(rect.Min.X + SideInset * scale, rect.Min.Y + VerticalInset * scale);
        var max = new Vector2(rect.Max.X - SideInset * scale, rect.Max.Y - VerticalInset * scale);
        var radius = (max.Y - min.Y) * 0.5f;
        Material.LiquidGlass(drawList, min, max, radius, scale, GlassTone.Dark, 0f);
        var centerY = (min.Y + max.Y) * 0.5f;
        var cursor = min.X + radius;
        if (windowSeconds > 0)
        {
            var ringCenter = new Vector2(cursor, centerY);
            var ring = RingRadius * scale;
            TurnTimerRing.Draw(drawList, ringCenter, ring, remainingMs, windowSeconds, accent, scale);
            var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
            Typography.DrawCentered(drawList, ringCenter, GameNumber.Label(seconds), CasinoColors.InkTitle,
                TextStyles.Caption1);
            cursor = ringCenter.X + ring + Metrics.Space.Sm * scale;
        }

        var crowdWidth = 0f;
        if (crowd > 0)
        {
            var count = GameNumber.Label(crowd);
            var countSize = Typography.Measure(count, TextStyles.Caption1);
            var avatars = Math.Min(MaxAvatars, crowd);
            crowdWidth = AvatarRadius * 2f * scale + (avatars - 1) * AvatarStep * scale + Metrics.Space.Xs * scale
                + countSize.X;
            var right = max.X - radius * 0.6f;
            Typography.Draw(drawList, new Vector2(right - countSize.X, centerY - countSize.Y * 0.5f), count,
                CasinoColors.InkBody, TextStyles.Caption1);
            var avatarRight = right - countSize.X - Metrics.Space.Xs * scale - AvatarRadius * scale;
            for (var avatar = 0; avatar < avatars; avatar++)
            {
                var center = new Vector2(avatarRight - avatar * AvatarStep * scale, centerY);
                drawList.AddCircleFilled(center, AvatarRadius * scale,
                    ImGui.GetColorU32(AvatarTints[avatar] with { W = 0.85f }), 16);
                drawList.AddCircle(center, AvatarRadius * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f)), 16,
                    MathF.Max(1f, scale));
            }
        }

        var labelWidth = MathF.Max(0f, max.X - radius * 0.6f - crowdWidth - Metrics.Space.Sm * scale - cursor);
        var fitted = Typography.FitText(label, labelWidth, TextStyles.FootnoteEmphasized);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(cursor, centerY - labelHeight * 0.5f), fitted, CasinoColors.InkTitle,
            TextStyles.FootnoteEmphasized);
    }
}

using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal static class SupportRow
{
    private const string HoverId = "##settings.supportRow";
    private const float RowHeight = 64f;
    private const float TileUnits = 34f;
    private const float LineGap = 2f;
    private const double HeartbeatMs = 1400.0;
    private const double WashDriftMs = 5200.0;
    private const double GlintPeriodMs = 3400.0;
    private const float GlintWindow = 0.42f;
    private const float GlintTrailOffset = 0.16f;
    private const float GlintHalfWidth = 0.55f;
    private const float GlintSlant = 0.7f;
    private const double BorderSpinMs = 3600.0;
    private const float BorderSharpness = 3f;
    private const double NudgeMs = 2400.0;
    private const float NudgeDistance = 4f;
    private const float HoverSlide = 5f;
    private const float RippleWindow = 0.55f;
    private const float RippleReach = 0.42f;
    private const int Particles = 12;
    private const double ParticlePeriodMs = 4600.0;
    private const double ParticleStaggerMs = 530.0;
    private const float ParticleSway = 7f;
    private const int Twinkles = 3;
    private const double TwinklePeriodMs = 1700.0;
    private const float SparkleWaist = 0.26f;
    private const float GoldenRatio = 0.618034f;

    private static readonly Vector4 Coral = SupportPage.PatreonCoral;
    private static readonly Vector4 Violet = new(0.60f, 0.40f, 0.98f, 1f);
    private static readonly Vector4 Gold = new(1.00f, 0.80f, 0.34f, 1f);
    private static readonly Vector4 Blush = new(1.00f, 0.62f, 0.68f, 1f);
    private static readonly Vector2[] TwinkleAnchors = { new(-0.62f, -0.70f), new(0.78f, -0.52f), new(0.66f, 0.80f) };

    public static bool Draw(PhoneTheme theme, string title)
    {
        var scale = UiScale.Current;
        var card = GroupCard.Begin(theme, 1, RowHeight);
        var row = card.NextRow();
        var bounds = card.Bounds;
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        var hover = HoverFx.Amount(HoverId, hovered);
        var beat = Pulse.Heartbeat(HeartbeatMs);

        DrawWash(drawList, bounds, radius, hover);
        DrawParticles(drawList, bounds, radius, scale, hover);
        DrawGlints(drawList, bounds, radius, hover);
        DrawBorder(drawList, bounds, radius, scale, hover);

        var tile = TileUnits * scale;
        var tileCenter = new Vector2(row.Min.X + Metrics.Space.Md * scale + tile * 0.5f, row.Center.Y);
        DrawHeartTile(drawList, tileCenter, tile, beat, hover, scale);

        var textLeft = tileCenter.X + tile * 0.5f + Metrics.Space.Md * scale;
        var nudge = (NudgeDistance * Pulse.Heartbeat(NudgeMs) * (1f - hover) + HoverSlide * hover) * scale;
        var chevronTip = new Vector2(row.Max.X + nudge, row.Center.Y);
        var maxWidth = MathF.Max(1f, row.Max.X - SettingsRow.ChevronReserve(scale) - textLeft);
        DrawText(drawList, theme, title, textLeft, row.Center.Y, maxWidth, scale);
        SettingsRow.DrawChevron(drawList, chevronTip, scale, Palette.Mix(Coral, Gold, hover));

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var clicked = UiInteract.Click(bounds.Min, bounds.Max, hovered);
        card.End();
        return clicked;
    }

    private static void DrawWash(ImDrawListPtr drawList, Rect bounds, float radius, float hover)
    {
        var drift = Pulse.Wave(WashDriftMs) * 0.65f;
        var alpha = 0.26f + 0.12f * hover;
        Squircle.FillHorizontalGradient(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Mix(Coral, Violet, drift), alpha)),
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Mix(Violet, Coral, drift), alpha * 0.8f)));
        Squircle.FillVerticalGradient(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f + 0.04f * hover)),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0f)));
    }

    private static void DrawParticles(ImDrawListPtr drawList, Rect bounds, float radius, float scale, float hover)
    {
        var left = bounds.Min.X + radius;
        var span = MathF.Max(1f, bounds.Width - radius * 2f);
        var height = bounds.Height;
        drawList.PushClipRect(bounds.Min, bounds.Max, true);
        for (var index = 0; index < Particles; index++)
        {
            var seed = index * GoldenRatio % 1f;
            var progress = (Pulse.Phase(ParticlePeriodMs + index * ParticleStaggerMs) + seed) % 1f;
            var sway = MathF.Sin(progress * MathF.PI * 2f + index * 1.7f) * ParticleSway * scale;
            var position = new Vector2(left + span * seed + sway,
                bounds.Max.Y + 6f * scale - progress * (height + 12f * scale));
            var fade = MathF.Sin(progress * MathF.PI);
            var alpha = fade * (0.30f + 0.35f * hover);
            var size = (5f + 4f * (index * 0.37f % 1f) + 2f * hover) * scale;
            switch (index % 3)
            {
                case 0:
                    ProgressRing.CenterIcon(drawList, position, FontAwesomeIcon.Heart,
                        Palette.WithAlpha(Blush, alpha), size * 1.6f);
                    break;
                case 1:
                    DrawSparkle(drawList, position, size, ImGui.GetColorU32(Palette.WithAlpha(Gold, alpha * 1.2f)));
                    break;
                default:
                    drawList.AddCircleFilled(position, size * 0.28f,
                        ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
                    break;
            }
        }

        drawList.PopClipRect();
    }

    private static void DrawGlints(ImDrawListPtr drawList, Rect bounds, float radius, float hover)
    {
        var phase = Pulse.Phase(GlintPeriodMs);
        var peak = 0.22f + 0.14f * hover;
        DrawGlint(drawList, bounds, radius, phase / GlintWindow, peak, 1f);
        DrawGlint(drawList, bounds, radius, (phase - GlintTrailOffset) / GlintWindow, peak * 0.6f, 0.45f);
    }

    private static void DrawGlint(ImDrawListPtr drawList, Rect bounds, float radius, float sweep, float peak,
        float widthFactor)
    {
        if (sweep <= 0f || sweep >= 1f)
        {
            return;
        }

        var height = bounds.Height;
        var normal = Vector2.Normalize(new Vector2(height, height * GlintSlant));
        var halfWidth = height * GlintHalfWidth * widthFactor;
        var near = Vector2.Dot(bounds.Min, normal) - halfWidth;
        var far = Vector2.Dot(bounds.Max, normal) + halfWidth;
        var eased = sweep * sweep * (3f - 2f * sweep);
        Squircle.FillGlint(drawList, bounds.Min, bounds.Max, radius, normal, near + (far - near) * eased,
            halfWidth, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, peak)));
    }

    private static void DrawBorder(ImDrawListPtr drawList, Rect bounds, float radius, float scale, float hover)
    {
        Material.EdgeSquircle(drawList, bounds.Min, bounds.Max, radius, scale);
        Squircle.Stroke(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Coral, 0.30f + 0.25f * hover)), 1.2f * scale);
        var angle = Pulse.Phase(BorderSpinMs) * MathF.PI * 2f;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var thickness = (1.6f + 0.6f * hover) * scale;
        Squircle.StrokeDirectional(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.85f + 0.15f * hover)), thickness, direction, BorderSharpness);
        Squircle.StrokeDirectional(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Blush, 0.75f + 0.25f * hover)), thickness, -direction,
            BorderSharpness);
    }

    private static void DrawHeartTile(ImDrawListPtr drawList, Vector2 center, float tile, float beat, float hover,
        float scale)
    {
        var heartPhase = Pulse.Phase(HeartbeatMs);
        if (heartPhase < RippleWindow)
        {
            var ripple = heartPhase / RippleWindow;
            var eased = 1f - (1f - ripple) * (1f - ripple);
            var grow = tile * RippleReach * eased;
            var rippleHalf = new Vector2(tile * 0.5f + grow, tile * 0.5f + grow);
            Squircle.Stroke(drawList, center - rippleHalf, center + rippleHalf,
                (tile * 0.5f + grow) * Metrics.Radius.TileFactor * 2f,
                ImGui.GetColorU32(Palette.WithAlpha(Blush, 0.55f * (1f - ripple))), (2f - ripple) * scale);
        }

        ProgressRing.Glow(center, tile * 0.6f, Coral, 0.35f + 0.55f * beat + 0.3f * hover);
        var size = tile * (1f + 0.07f * beat + 0.05f * hover);
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var tileMin = center - half;
        var tileMax = center + half;
        var rounding = size * Metrics.Radius.TileFactor;
        Squircle.FillVerticalGradient(drawList, tileMin, tileMax, rounding,
            ImGui.GetColorU32(Palette.Lighten(Coral, 0.16f)), ImGui.GetColorU32(Palette.Mix(Coral, Violet, 0.35f)));
        Squircle.FillVerticalGradient(drawList, tileMin, new Vector2(tileMax.X, center.Y), rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0f)));
        Material.EdgeSquircle(drawList, tileMin, tileMax, rounding, scale);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Heart, Vector4.One, size * (0.50f + 0.08f * beat));
        DrawTwinkles(drawList, center, size, scale);
    }

    private static void DrawTwinkles(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        for (var index = 0; index < Twinkles; index++)
        {
            var phase = (Pulse.Phase(TwinklePeriodMs) + index / (float)Twinkles) % 1f;
            var glow = MathF.Sin(phase * MathF.PI);
            glow *= glow;
            if (glow <= 0.01f)
            {
                continue;
            }

            var position = center + TwinkleAnchors[index] * size * 0.62f;
            DrawSparkle(drawList, position, (2f + 3.5f * glow) * scale,
                ImGui.GetColorU32(new Vector4(1f, 0.96f, 0.86f, glow)));
        }
    }

    private static void DrawText(ImDrawListPtr drawList, PhoneTheme theme, string title, float left, float centerY,
        float maxWidth, float scale)
    {
        var fittedTitle = Typography.FitText(title, maxWidth, TextStyles.BodyEmphasized);
        var subtitle = Typography.FitText(Loc.T(L.Settings.SupportBecomeMember), maxWidth, TextStyles.Footnote);
        var titleSize = Typography.Measure(fittedTitle, TextStyles.BodyEmphasized);
        var subtitleSize = Typography.Measure(subtitle, TextStyles.Footnote);
        var top = centerY - (titleSize.Y + LineGap * scale + subtitleSize.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, theme.TextStrong, TextStyles.BodyEmphasized);
        var subtitleColor = Palette.Mix(Palette.Lighten(Coral, 0.22f), Gold, Pulse.Wave(WashDriftMs * 0.5) * 0.7f);
        Typography.Draw(drawList, new Vector2(left, top + titleSize.Y + LineGap * scale), subtitle, subtitleColor,
            TextStyles.Footnote);
    }

    private static void DrawSparkle(ImDrawListPtr drawList, Vector2 center, float extent, uint color)
    {
        var waist = extent * SparkleWaist;
        drawList.AddQuadFilled(new Vector2(center.X, center.Y - extent), new Vector2(center.X + waist, center.Y),
            new Vector2(center.X, center.Y + extent), new Vector2(center.X - waist, center.Y), color);
        drawList.AddQuadFilled(new Vector2(center.X - extent, center.Y), new Vector2(center.X, center.Y - waist),
            new Vector2(center.X + extent, center.Y), new Vector2(center.X, center.Y + waist), color);
    }
}

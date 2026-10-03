using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Jobs;

internal static class JobsArt
{
    public const float SectionGap = 24f;
    public const float HeaderGap = 10f;
    public const float CardPad = 16f;
    public const float TileGap = 12f;
    public const float LineGap = 2f;

    private const float RingTrackAlpha = 0.16f;
    private const float LockedIconAlpha = 0.32f;
    private const float IconFill = 0.74f;
    private const float IconRadiusFactor = 0.24f;
    private const float CappedGlowAlpha = 0.20f;
    private const float ActiveDotFraction = 0.17f;
    private const float StateTileSize = 44f;
    private const float StateGap = 14f;
    private const float StateLineGap = 3f;
    private const float ChevronThickness = 1.8f;
    private const float BarRestedAlpha = 0.38f;
    private const float BarTrackAlpha = 0.14f;
    private const double SweepPeriodMilliseconds = 900.0;
    private const float SweepArc = 1.6f;
    private const float SweepHeadAlpha = 0.9f;

    private static readonly Vector4[] RoleTints =
    {
        AccentRing.Azure,
        AccentRing.Green,
        AccentRing.Red,
        AccentRing.Orange,
        AccentRing.Violet,
        AccentRing.Gold,
        AccentRing.Teal,
    };

    private static readonly FontAwesomeIcon[] RoleIcons =
    {
        FontAwesomeIcon.ShieldAlt,
        FontAwesomeIcon.Heartbeat,
        FontAwesomeIcon.FistRaised,
        FontAwesomeIcon.Crosshairs,
        FontAwesomeIcon.Magic,
        FontAwesomeIcon.Hammer,
        FontAwesomeIcon.Leaf,
    };

    public static Vector4 Tint(JobRole role) => RoleTints[(int)role];

    public static FontAwesomeIcon Icon(JobRole role) => RoleIcons[(int)role];

    public static float FrameDelta() => MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);

    public static void JobRing(ImDrawListPtr drawList, ITextureProvider textures, Vector2 center, float radius,
        float thickness, float fraction, JobRow job, Vector4 trackInk, bool pending, float scale)
    {
        var tint = Tint(job.Role);
        if (job.IsCapped)
        {
            drawList.AddCircleFilled(center, radius + thickness * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(tint, CappedGlowAlpha)), 48);
        }

        ProgressRing.Track(drawList, center, radius, thickness, Palette.WithAlpha(trackInk, RingTrackAlpha));
        if (pending)
        {
            ProgressRing.Sweep(center, radius, thickness, tint, SweepPeriodMilliseconds, SweepArc, SweepHeadAlpha,
                drawList);
        }
        else if (!job.IsLocked)
        {
            ProgressRing.Fill(drawList, center, radius, thickness, fraction, tint);
        }

        var iconHalf = (radius - thickness) * IconFill;
        var iconMin = center - new Vector2(iconHalf, iconHalf);
        var iconMax = center + new Vector2(iconHalf, iconHalf);
        var alpha = job.IsLocked ? LockedIconAlpha : 1f;
        if (!GameIconTile.Draw(drawList, textures, job.IconId, iconMin, iconMax, iconHalf * 2f * IconRadiusFactor,
                scale, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.18f * alpha)), requireIcon: true))
        {
            ProgressRing.CenterIcon(drawList, center, Icon(job.Role), Palette.WithAlpha(tint, alpha), iconHalf);
        }
        else if (job.IsLocked)
        {
            drawList.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f - alpha)),
                iconHalf * 2f * IconRadiusFactor);
        }

        if (job.IsLocked)
        {
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Lock, Palette.WithAlpha(trackInk, 0.8f),
                iconHalf * 0.7f);
        }
    }

    public static void ActiveDot(ImDrawListPtr drawList, Vector2 ringCenter, float radius, Vector4 accent,
        Vector4 rim)
    {
        var dotRadius = MathF.Max(3f, radius * ActiveDotFraction);
        var offset = radius * 0.7071f;
        var center = ringCenter + new Vector2(offset, -offset);
        drawList.AddCircleFilled(center, dotRadius + 2f, ImGui.GetColorU32(rim), 20);
        drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(accent), 20);
    }

    public static void Bar(ImDrawListPtr drawList, Rect rect, float fraction, float restedFraction, Vector4 tint,
        Vector4 trackInk)
    {
        var radius = rect.Height * 0.5f;
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Palette.WithAlpha(trackInk, BarTrackAlpha)),
            radius);
        var width = rect.Width;
        var filled = Math.Clamp(fraction, 0f, 1f) * width;
        var rested = Math.Clamp(restedFraction, 0f, 1f - Math.Clamp(fraction, 0f, 1f)) * width;
        if (rested > 0.5f)
        {
            var restedMax = new Vector2(MathF.Min(rect.Max.X, rect.Min.X + filled + rested + radius), rect.Max.Y);
            drawList.AddRectFilled(rect.Min, restedMax, ImGui.GetColorU32(Palette.WithAlpha(tint, BarRestedAlpha)),
                radius);
        }

        if (filled <= 0.5f)
        {
            return;
        }

        drawList.AddRectFilled(rect.Min, new Vector2(rect.Min.X + MathF.Max(filled, rect.Height), rect.Max.Y),
            ImGui.GetColorU32(tint), radius);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = ChevronThickness * scale;
        var tip = new Vector2(center.X + size * 0.5f, center.Y);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y - size), tip, color, thickness);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y + size), tip, color, thickness);
    }

    public static void GlyphTile(ImDrawListPtr drawList, Vector2 center, float size, Vector4 tint,
        FontAwesomeIcon icon)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink, size * 0.46f);
    }

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink)
    {
        Typography.Draw(drawList, origin, Typography.FitText(title, width, TextStyles.Title3), ink, TextStyles.Title3);
        return Typography.LineHeight(TextStyles.Title3);
    }

    public static float StateHeight(string title, string body, float width, float scale)
    {
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               CardPad * 2f * scale;
    }

    public static float State(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon,
        Vector4 tint, string title, string body, float scale)
    {
        var height = StateHeight(title, body, width, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Widget * scale, true);
        var pad = CardPad * scale;
        var tileSize = StateTileSize * scale;
        GlyphTile(drawList, new Vector2(origin.X + pad + tileSize * 0.5f, origin.Y + pad + tileSize * 0.5f), tileSize,
            tint, icon);
        var textLeft = origin.X + pad + tileSize + StateGap * scale;
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale), body,
            ui.MutedInk, TextStyles.Subheadline, textWidth);
        return height;
    }

    private static float StateTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (CardPad * 2f + StateTileSize + StateGap) * scale);
}

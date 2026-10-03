using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Hunts;

internal static class HuntsArt
{
    public const float CardRadius = 22f;
    public const float CardPadding = 16f;
    public const float CardGap = 14f;
    public const float LineGap = 3f;
    public const float RowGap = 12f;
    public const float TileHeight = 56f;
    public const float TileGap = 8f;
    public const float TileRadius = 14f;
    public const float RingTrackAlpha = 0.18f;
    public const float MapZoomWindow = 0.30f;
    private const float CapsulePadX = 8f;
    private const float CapsulePadY = 3f;
    private const float TileOffWashAlpha = 0.06f;
    private const float TileHoverMix = 0.08f;
    private const float PinRadius = 5f;
    private const float PinHalo = 9f;
    private const float PinHaloAlpha = 0.30f;
    private const float TimelineTrackAlpha = 0.14f;
    private const float TimelineWindowAlpha = 0.34f;
    private const float TimelineTick = 4f;
    private const float TimelineNowRadius = 5f;
    private const float DotRadius = 4f;

    public static readonly Vector4 OpenColor = new(0.20f, 0.78f, 0.35f, 1f);
    public static readonly Vector4 CappedColor = new(0.20f, 0.55f, 0.95f, 1f);
    public static readonly Vector4 UnmetColor = new(0.60f, 0.40f, 0.90f, 1f);
    public static readonly Vector4 LiveColor = new(0.95f, 0.66f, 0.12f, 1f);
    public static readonly Vector4 RankSSColor = new(0.95f, 0.35f, 0.35f, 1f);
    public static readonly Vector4 RankSColor = new(0.65f, 0.45f, 0.95f, 1f);
    public static readonly Vector4 RankAColor = new(0.30f, 0.65f, 0.95f, 1f);
    public static readonly Vector4 RankBColor = new(0.30f, 0.72f, 0.62f, 1f);
    public static readonly Vector4 RankFColor = new(0.92f, 0.56f, 0.24f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector4 RankColor(string rank, Vector4 fallback) => rank switch
    {
        "SS" => RankSSColor,
        "S" => RankSColor,
        "A" => RankAColor,
        "B" => RankBColor,
        "F" => RankFColor,
        _ => fallback,
    };

    public static Vector4 StatusColor(HuntWindowStatus status, Vector4 fallback) => status switch
    {
        HuntWindowStatus.Open => OpenColor,
        HuntWindowStatus.Capped => CappedColor,
        HuntWindowStatus.Unmet => UnmetColor,
        HuntWindowStatus.Spawned or HuntWindowStatus.Scheduled => LiveColor,
        _ => fallback,
    };

    public static void StatusRing(ImDrawListPtr drawList, Vector2 center, float radius, float thickness,
        float fraction, Vector4 ink, string rank, Vector4 rankInk)
    {
        var ringRadius = radius - thickness * 0.5f;
        ProgressRing.Track(drawList, center, ringRadius, thickness, Palette.WithAlpha(ink, RingTrackAlpha));
        ProgressRing.Fill(drawList, center, ringRadius, thickness, fraction, ink);
        if (rank.Length == 0)
        {
            return;
        }

        var style = rank.Length > 1 ? TextStyles.FootnoteEmphasized : TextStyles.Headline;
        var size = Typography.Measure(rank, style);
        Typography.Draw(drawList, center - size * 0.5f, rank, rankInk, style);
    }

    public static void RankTile(ImDrawListPtr drawList, Vector2 min, float size, string rank, Vector4 color)
    {
        var max = min + new Vector2(size, size);
        IconTile.FillShaded(drawList, min, max, size * Metrics.Radius.TileFactor, IconTile.Surface(color));
        var style = rank.Length > 1 ? TextStyles.FootnoteEmphasized : TextStyles.Headline;
        var textSize = Typography.Measure(rank, style);
        Typography.Draw(drawList, (min + max - textSize) * 0.5f, rank, AccentRing.Ink, style);
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

    public static float CapsuleWidth(string text, float scale) =>
        Typography.Measure(text, TextStyles.Caption1).X + CapsulePadX * 2f * scale;

    public static float CapsuleHeight(float scale) =>
        Typography.LineHeight(TextStyles.Caption1) + CapsulePadY * 2f * scale;

    public static bool ToggleTile(AppSkin ui, string id, Rect rect, string title, string subtitle, bool active,
        Vector4 tint, bool interactive, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, Motion.PressScaleControl);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = TileRadius * scale;
        var fill = active ? IconTile.Surface(tint) : Palette.Mix(ui.FieldSurface, ui.TitleInk, TileOffWashAlpha);
        if (hovered)
        {
            fill = Palette.Mix(fill, active ? White : ui.TitleInk, TileHoverMix);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (active)
        {
            IconTile.FillShaded(drawList, min, max, radius, fill);
        }
        else
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        }

        var titleInk = active ? AccentRing.Ink : ui.TitleInk;
        var subtitleInk = active ? Palette.WithAlpha(AccentRing.Ink, 0.78f) : ui.MutedInk;
        var inner = MathF.Max(1f, max.X - min.X - 12f * scale);
        var fittedTitle = Typography.FitText(title, inner, TextStyles.SubheadlineEmphasized);
        var titleSize = Typography.Measure(fittedTitle, TextStyles.SubheadlineEmphasized);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, (min + max - titleSize) * 0.5f, fittedTitle, titleInk,
                TextStyles.SubheadlineEmphasized);
        }
        else
        {
            var fittedSubtitle = Typography.FitText(subtitle, inner, TextStyles.Caption1);
            var subtitleSize = Typography.Measure(fittedSubtitle, TextStyles.Caption1);
            var blockTop = (min.Y + max.Y - titleSize.Y - subtitleSize.Y) * 0.5f;
            var centerX = (min.X + max.X) * 0.5f;
            Typography.Draw(drawList, new Vector2(centerX - titleSize.X * 0.5f, blockTop), fittedTitle, titleInk,
                TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(centerX - subtitleSize.X * 0.5f, blockTop + titleSize.Y),
                fittedSubtitle, subtitleInk, TextStyles.Caption1);
        }

        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static Rect TileRect(Vector2 origin, float width, int index, int columns, float scale)
    {
        var gap = TileGap * scale;
        var tileWidth = (width - gap * (columns - 1)) / columns;
        var column = index % columns;
        var row = index / columns;
        var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (TileHeight * scale + gap));
        return new Rect(min, min + new Vector2(tileWidth, TileHeight * scale));
    }

    public static float TileGridHeight(int count, int columns, float scale)
    {
        if (count <= 0)
        {
            return 0f;
        }

        var rows = (count + columns - 1) / columns;
        return rows * TileHeight * scale + (rows - 1) * TileGap * scale;
    }

    public static bool MapThumbnail(ImDrawListPtr drawList, IDalamudTextureWrap? texture, Vector2 min, Vector2 max,
        float radius, Vector2 focus, bool hasFocus, Vector4 pinInk, float scale)
    {
        if (texture is null)
        {
            return false;
        }

        var window = hasFocus ? MapZoomWindow : 1f;
        var half = window * 0.5f;
        var centerX = hasFocus ? Math.Clamp(focus.X, half, 1f - half) : 0.5f;
        var centerY = hasFocus ? Math.Clamp(focus.Y, half, 1f - half) : 0.5f;
        var uvMin = new Vector2(centerX - half, centerY - half);
        var uvMax = new Vector2(centerX + half, centerY + half);
        drawList.AddImageRounded(texture.Handle, min, max, uvMin, uvMax, 0xFFFFFFFFu, radius,
            ImDrawFlags.RoundCornersAll);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        if (!hasFocus)
        {
            return true;
        }

        var pin = new Vector2(min.X + (focus.X - uvMin.X) / window * (max.X - min.X),
            min.Y + (focus.Y - uvMin.Y) / window * (max.Y - min.Y));
        drawList.AddCircleFilled(pin, PinHalo * scale, ImGui.GetColorU32(Palette.WithAlpha(pinInk, PinHaloAlpha)), 24);
        drawList.AddCircleFilled(pin, PinRadius * scale + 1.5f * scale, ImGui.GetColorU32(White), 20);
        drawList.AddCircleFilled(pin, PinRadius * scale, ImGui.GetColorU32(pinInk), 20);
        return true;
    }

    public static void Timeline(ImDrawListPtr drawList, Rect bar, in HuntTimeline timeline, Vector4 ink,
        Vector4 nowInk, Vector4 tickInk, float scale)
    {
        var radius = bar.Height * 0.5f;
        Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(tickInk,
            TimelineTrackAlpha)));
        var windowMin = new Vector2(bar.Min.X + bar.Width * timeline.Minimum, bar.Min.Y);
        var windowMax = new Vector2(bar.Min.X + bar.Width * timeline.Cap, bar.Max.Y);
        Squircle.Fill(drawList, windowMin, windowMax, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ink, TimelineWindowAlpha)));
        var elapsedMax = new Vector2(bar.Min.X + MathF.Max(bar.Height, bar.Width * timeline.Now), bar.Max.Y);
        Squircle.Fill(drawList, bar.Min, elapsedMax, radius, ImGui.GetColorU32(ink));
        var tick = TimelineTick * scale;
        var tickColor = ImGui.GetColorU32(Palette.WithAlpha(tickInk, 0.55f));
        Tick(drawList, bar, timeline.Minimum, tick, tickColor);
        Tick(drawList, bar, timeline.Cap, tick, tickColor);
        if (timeline.HasAverage)
        {
            Tick(drawList, bar, timeline.Average, tick * 0.6f, tickColor);
        }

        var nowCenter = new Vector2(bar.Min.X + bar.Width * timeline.Now, bar.Center.Y);
        drawList.AddCircleFilled(nowCenter, TimelineNowRadius * scale + 2f * scale, ImGui.GetColorU32(White), 20);
        drawList.AddCircleFilled(nowCenter, TimelineNowRadius * scale, ImGui.GetColorU32(nowInk), 20);
    }

    public static void Dot(ImDrawListPtr drawList, Vector2 center, Vector4 ink, float scale) =>
        drawList.AddCircleFilled(center, DotRadius * scale, ImGui.GetColorU32(ink), 16);

    public static void HaloDot(ImDrawListPtr drawList, Vector2 center, Vector4 ink, bool halo, float scale)
    {
        if (halo)
        {
            drawList.AddCircleFilled(center, DotRadius * scale * 2f,
                ImGui.GetColorU32(Palette.WithAlpha(ink, PinHaloAlpha)), 20);
        }

        Dot(drawList, center, ink, scale);
    }

    private static void Tick(ImDrawListPtr drawList, Rect bar, float fraction, float reach, uint color)
    {
        var x = bar.Min.X + bar.Width * fraction;
        drawList.AddLine(new Vector2(x, bar.Min.Y - reach), new Vector2(x, bar.Max.Y + reach), color, 1.5f);
    }
}

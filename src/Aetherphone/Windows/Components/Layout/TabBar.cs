using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct TabItem(string Label, string Glyph, string ActiveGlyph = "", int Badge = 0,
    string? AnchorKey = null, bool CustomIcon = false)
{
    public string GlyphFor(bool active) => active && ActiveGlyph.Length > 0 ? ActiveGlyph : Glyph;
}

internal readonly record struct TabBarAction(string Glyph, string Label, int Badge = 0, string? AnchorKey = null);

internal readonly record struct TabBarResult(int Tapped, bool ActionTapped)
{
    public static readonly TabBarResult None = new(-1, false);
}

internal readonly record struct TabItemPose(Vector2 IconCenter, float Scale)
{
    public float AvatarRadius(float uiScale) => TabBarLayout.AvatarRadius * uiScale * Scale;

    public float AvatarRingRadius(float uiScale) => AvatarRadius(uiScale) + TabBarLayout.AvatarRingGap * uiScale;
}

internal interface ITabIconDrawer
{
    void DrawTabIcon(ImDrawListPtr drawList, int index, TabItemPose pose, bool active);
}

internal sealed class TabBar
{
    public const float GlassOpacity = 0.86f;
    public const float FlatGlassOpacity = 0.95f;
    private const float LayerHeadroomUnits = 12f;
    private const float HighlightTint = 0.22f;
    private const float HighlightRimAlpha = 0.16f;
    private const float PressDepth = 1f - Motion.PressScaleControl;
    private const float MaxFrameSeconds = 0.1f;
    private const float BadgeScale = 0.8f;
    private const float BadgeOffsetX = 10f;
    private const float BadgeOffsetY = 9f;
    private static readonly Vector4 MutedOnDarkGlass = new(0.93f, 0.94f, 0.97f, 0.70f);
    private static readonly Vector4 StrongOnDarkGlass = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HighlightRim = new(1f, 1f, 1f, 1f);

    private Spring[] hover = Array.Empty<Spring>();
    private Spring[] press = Array.Empty<Spring>();
    private TabItemPose[] poses = Array.Empty<TabItemPose>();
    private Spring highlightX;
    private Spring actionHover;
    private Spring actionPress;
    private bool highlightSettled;
    private int lastFrame = -2;

    public Rect Bounds { get; private set; }

    public static float ContentInset(float scale) => TabBarLayout.ContentInset(scale);

    private static float CurrentGlassOpacity => WallpaperBackdrop.Available ? GlassOpacity : FlatGlassOpacity;

    private static bool BarHover(Vector2 min, Vector2 max) =>
        !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max);

    public static Rect ContentArea(Rect area, float scale) => TabBarLayout.ContentArea(area, scale);

    public static Rect Zone(Rect area, float scale) => TabBarLayout.Zone(area, scale);

    public static AppSurface.BottomInsetScope ReserveContent(float scale) =>
        AppSurface.ReserveBottom(ContentInset(scale));

    public TabItemPose Pose(int index) => index >= 0 && index < poses.Length ? poses[index] : default;

    public TabBarResult Draw(Rect area, AppSkin ui, ReadOnlySpan<TabItem> items, int active,
        TabBarAction? action = null, ITabIconDrawer? icons = null)
    {
        if (items.Length == 0)
        {
            return TabBarResult.None;
        }

        EnsureCapacity(items.Length);
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        SyncVisit(ImGui.GetFrameCount());

        var activeIndex = Math.Clamp(active, 0, items.Length - 1);
        var capsule = TabBarLayout.FullCapsule(area, scale, action.HasValue);
        Bounds = capsule;

        var theme = ui.Theme;
        var backdrop = ui.BackdropColor;
        var tone = Material.ToneFor(backdrop);
        var zone = TabBarLayout.Zone(area, scale);
        var layerRect = new Rect(new Vector2(zone.Min.X, zone.Min.Y - LayerHeadroomUnits * scale), zone.Max);
        using var layer = ScreenLayer.Begin("tabbar", layerRect, false);
        UiInteract.HoverOverlay(layerRect);
        var drawList = ImGui.GetWindowDrawList();
        var radius = capsule.Height * 0.5f;
        Material.ThemedGlass(drawList, capsule.Min, capsule.Max, radius, scale, backdrop, CurrentGlassOpacity);

        var activeCell = TabBarLayout.Cell(capsule, items.Length, activeIndex, scale);
        StepHighlight(activeCell.Center.X, delta);
        DrawHighlight(drawList, activeCell, ui.Accent, Math.Clamp(press[activeIndex].Value, 0f, 1f), scale);

        var inactiveInk = tone == GlassTone.Dark ? MutedOnDarkGlass : theme.TextMuted;
        var result = TabBarResult.None;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var cell = TabBarLayout.Cell(capsule, items.Length, index, scale);
            var isActive = index == activeIndex;
            var iconCenter = TabBarLayout.IconCenter(cell);
            var hovered = BarHover(cell.Min, cell.Max);
            hover[index].Step(hovered ? 1f : 0f, Motion.HoverLift, delta);
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            press[index].Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta);
            var itemScale = (1f - PressDepth * Math.Clamp(press[index].Value, 0f, 1f))
                            * (1f + Motion.HoverLiftIcon * Math.Clamp(hover[index].Value, 0f, 1f));
            poses[index] = new TabItemPose(iconCenter, itemScale);
            if (item.AnchorKey is { } anchorKey)
            {
                UiAnchors.Report(anchorKey, cell);
            }

            if (item.CustomIcon && icons is not null)
            {
                icons.DrawTabIcon(drawList, index, poses[index], isActive);
            }
            else
            {
                PhoneIcon.Draw(drawList, iconCenter, item.GlyphFor(isActive), isActive ? ui.Accent : inactiveInk,
                    TabBarLayout.IconSize * scale * itemScale);
            }

            if (item.Badge > 0)
            {
                AppBadge.Draw(iconCenter + new Vector2(BadgeOffsetX, -BadgeOffsetY) * scale, item.Badge, theme,
                    scale * BadgeScale);
            }

            HoverTooltip.Enqueue(cell, item.Label, Math.Clamp(hover[index].Value, 0f, 1f), HoverLabelSide.Above);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cell.Min, cell.Max, hovered))
            {
                result = new TabBarResult(index, false);
            }
        }

        if (action is { } trailing && DrawAction(drawList, area, ui, trailing, tone, backdrop, delta, scale))
        {
            result = new TabBarResult(result.Tapped, true);
        }

        return result;
    }

    private void SyncVisit(int frame)
    {
        if (frame - lastFrame > 1)
        {
            highlightSettled = false;
            actionHover.SnapTo(0f);
            actionPress.SnapTo(0f);
            for (var index = 0; index < hover.Length; index++)
            {
                hover[index].SnapTo(0f);
                press[index].SnapTo(0f);
            }
        }

        lastFrame = frame;
    }

    private void EnsureCapacity(int count)
    {
        if (hover.Length == count)
        {
            return;
        }

        hover = new Spring[count];
        press = new Spring[count];
        poses = new TabItemPose[count];
    }

    private void StepHighlight(float targetX, float delta)
    {
        if (!highlightSettled)
        {
            highlightX.SnapTo(targetX);
            highlightSettled = true;
            return;
        }

        highlightX.Step(targetX, Motion.TabBar, delta);
    }

    private void DrawHighlight(ImDrawListPtr drawList, Rect activeCell, Vector4 accent, float pressAmount, float scale)
    {
        var highlight = TabBarLayout.Highlight(activeCell, scale)
            .Translate(new Vector2(highlightX.Value - activeCell.Center.X, 0f));
        var half = highlight.Size * 0.5f * (1f - PressDepth * pressAmount);
        var min = highlight.Center - half;
        var max = highlight.Center + half;
        Squircle.Fill(drawList, min, max, half.Y, ImGui.GetColorU32(Palette.WithAlpha(accent, HighlightTint)));
        Squircle.Stroke(drawList, min, max, half.Y,
            ImGui.GetColorU32(Palette.WithAlpha(HighlightRim, HighlightRimAlpha)), 1f * scale);
    }

    private bool DrawAction(ImDrawListPtr drawList, Rect area, AppSkin ui, in TabBarAction action, GlassTone tone,
        Vector4 backdrop, float delta, float scale)
    {
        var circle = TabBarLayout.ActionCircle(area, scale);
        var hovered = BarHover(circle.Min, circle.Max);
        actionHover.Step(hovered ? 1f : 0f, Motion.HoverLift, delta);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        actionPress.Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta);
        var grow = (1f - PressDepth * Math.Clamp(actionPress.Value, 0f, 1f))
                   * (1f + Motion.HoverLiftIcon * Math.Clamp(actionHover.Value, 0f, 1f));
        var half = circle.Size * 0.5f * grow;
        var min = circle.Center - half;
        var max = circle.Center + half;
        Material.ThemedGlass(drawList, min, max, half.Y, scale, backdrop, CurrentGlassOpacity);
        var ink = tone == GlassTone.Dark ? StrongOnDarkGlass : ui.Theme.TextStrong;
        PhoneIcon.Draw(drawList, circle.Center, action.Glyph, ink, TabBarLayout.IconSize * scale * grow);
        if (action.Badge > 0)
        {
            AppBadge.Draw(circle.Center + new Vector2(BadgeOffsetX, -BadgeOffsetY) * scale, action.Badge, ui.Theme,
                scale * BadgeScale);
        }

        if (action.AnchorKey is { } anchorKey)
        {
            UiAnchors.Report(anchorKey, circle);
        }

        HoverTooltip.Enqueue(circle, action.Label, Math.Clamp(actionHover.Value, 0f, 1f), HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(circle.Min, circle.Max, hovered);
    }
}

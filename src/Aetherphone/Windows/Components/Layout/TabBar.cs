using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
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
    private const float LayerBleedUnits = 4f;
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

    private static int hostDepth;

    private Spring[] hover = Array.Empty<Spring>();
    private Spring[] press = Array.Empty<Spring>();
    private TabItemPose[] poses = Array.Empty<TabItemPose>();
    private Spring highlightSlot;
    private Spring actionHover;
    private Spring actionPress;
    private bool highlightSettled;
    private int lastFrame = -2;

    public Rect Bounds { get; private set; }

    public UiSound TapSound { get; set; } = UiSound.Tap;

    public float Height { get; init; } = TabBarLayout.Height;

    public float SideInset { get; init; } = TabBarLayout.SideInset;

    public static float ContentInset(float scale, float height = TabBarLayout.Height) =>
        TabBarLayout.ContentInset(scale, height);

    private static float CurrentGlassOpacity => WallpaperBackdrop.Available ? GlassOpacity : FlatGlassOpacity;

    private static bool BarHover(Vector2 min, Vector2 max) =>
        !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max);

    public static Rect ContentArea(Rect area, float scale, float height = TabBarLayout.Height) =>
        TabBarLayout.ContentArea(area, scale, height);

    public static Rect Zone(Rect area, float scale) => TabBarLayout.Zone(area, scale);

    public static Rect GlassLayer(Rect rect, float scale)
    {
        var bleed = new Vector2(LayerBleedUnits * scale, 0f);
        return new Rect(rect.Min - bleed, rect.Max + bleed);
    }

    public static bool Hosting => hostDepth > 0;

    public static HostScope ReserveContent(float scale, float extraInset = 0f) =>
        new(true, true, ContentInset(scale) + extraInset);

    public static HostScope Host(bool active = true) => new(active, false, 0f);

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
        var capsule = TabBarLayout.FullCapsule(area, scale, action.HasValue, Height, SideInset);
        Bounds = capsule;

        var theme = ui.Theme;
        var backdrop = ui.BackdropColor;
        var tone = Material.ToneFor(backdrop);
        var zone = TabBarLayout.Zone(area, scale, Height);
        var layerRect = new Rect(new Vector2(zone.Min.X, zone.Min.Y - LayerHeadroomUnits * scale), zone.Max);
        using var layer = ScreenLayer.Begin("tabbar", GlassLayer(layerRect, scale), false);
        UiInteract.HoverOverlay(layerRect);
        var drawList = ImGui.GetWindowDrawList();
        var radius = capsule.Height * 0.5f;
        Material.ThemedGlass(drawList, capsule.Min, capsule.Max, radius, scale, backdrop, CurrentGlassOpacity);

        StepHighlight(activeIndex, delta);
        DrawHighlight(drawList, TabBarLayout.Highlight(capsule, items.Length, highlightSlot.Value, scale), ui.Accent,
            Math.Clamp(press[activeIndex].Value, 0f, 1f), scale);

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
            var itemScale = 1f - PressDepth * Math.Clamp(press[index].Value, 0f, 1f);
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
                    TabBarLayout.IconSize * scale * itemScale, optical: true);
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

            if (UiInteract.Click(cell.Min, cell.Max, hovered, TapSound))
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

    private void StepHighlight(int targetSlot, float delta)
    {
        if (!highlightSettled)
        {
            highlightSlot.SnapTo(targetSlot);
            highlightSettled = true;
            return;
        }

        highlightSlot.Step(targetSlot, Motion.TabBar, delta);
    }

    private static void DrawHighlight(ImDrawListPtr drawList, Rect highlight, Vector4 accent, float pressAmount,
        float scale)
    {
        var half = highlight.Size * 0.5f * (1f - PressDepth * pressAmount);
        var min = highlight.Center - half;
        var max = highlight.Center + half;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(accent, HighlightTint)), half.Y);
        drawList.AddRect(min, max, ImGui.GetColorU32(Palette.WithAlpha(HighlightRim, HighlightRimAlpha)), half.Y,
            ImDrawFlags.None, 1f * scale);
    }

    private bool DrawAction(ImDrawListPtr drawList, Rect area, AppSkin ui, in TabBarAction action, GlassTone tone,
        Vector4 backdrop, float delta, float scale)
    {
        var circle = TabBarLayout.ActionCircle(area, scale, Height, SideInset);
        var hovered = BarHover(circle.Min, circle.Max);
        actionHover.Step(hovered ? 1f : 0f, Motion.HoverLift, delta);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        actionPress.Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta);
        var grow = 1f - PressDepth * Math.Clamp(actionPress.Value, 0f, 1f);
        var half = circle.Size * 0.5f * grow;
        var min = circle.Center - half;
        var max = circle.Center + half;
        Material.ThemedGlass(drawList, min, max, half.Y, scale, backdrop, CurrentGlassOpacity);
        var ink = tone == GlassTone.Dark ? StrongOnDarkGlass : ui.Theme.TextStrong;
        PhoneIcon.Draw(drawList, circle.Center, action.Glyph, ink, TabBarLayout.IconSize * scale * grow, optical: true);
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

        return UiInteract.Click(circle.Min, circle.Max, hovered, TapSound);
    }

    public ref struct HostScope
    {
        private readonly bool active;
        private readonly bool reservesInset;
        private readonly AppSurface.BottomInsetScope inset;

        internal HostScope(bool active, bool reservesInset, float bottomInset)
        {
            this.active = active;
            this.reservesInset = reservesInset;
            inset = reservesInset ? AppSurface.ReserveBottom(bottomInset) : default;
            if (active)
            {
                hostDepth++;
            }
        }

        public void Dispose()
        {
            if (reservesInset)
            {
                inset.Dispose();
            }

            if (active)
            {
                hostDepth--;
            }
        }
    }
}

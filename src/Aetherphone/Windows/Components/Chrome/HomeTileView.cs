using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class HomeTileView
{
    public const float TiltDepth = 0.07f;
    private const float FolderMiniPadFraction = 0.14f;
    private const float FolderMiniCellFraction = (1f - FolderMiniPadFraction * 2f) / FolderMiniColumns;
    public const int FolderMiniColumns = 3;
    public const float FolderMiniIconFraction = FolderMiniCellFraction * 0.78f;
    private const float EffectGlowReach = 0.35f;
    private const int EffectGlowLayers = 6;
    private const float EffectGlowAlphaLow = 0.18f;
    private const float EffectGlowAlphaHigh = 0.30f;
    private const float EffectGlowPeriodSeconds = 2.4f;
    private const int EffectMoteCount = 8;
    private const float EffectMoteOrbitSeconds = 9f;
    private const float EffectMoteOrbitReach = 1.22f;
    private const float EffectMoteOrbitWobble = 0.10f;
    private const float EffectMoteWobbleRate = 1.7f;
    private const float EffectMoteWobbleStagger = 1.3f;
    private const float EffectMotePulseRate = 2.1f;
    private const float EffectMotePulseStagger = 2.6f;
    private const float EffectMoteRadiusMin = 2f;
    private const float EffectMoteRadiusMax = 3f;
    private const float EffectMoteAlphaMin = 0.15f;
    private const float EffectMoteAlphaMax = 0.95f;
    private const float EffectMoteHaloReach = 2.2f;
    private const float EffectMoteHaloAlpha = 0.28f;
    private const float EffectMoteCoreReach = 0.45f;
    private const float EffectMoteCoreLift = 0.55f;
    private const int EffectMoteSegments = 12;
    private static readonly Vector4 EffectMoteCoreLight = new(1f, 1f, 1f, 1f);

    public static Vector2 FolderMiniCenter(int index) =>
        new(FolderMiniPadFraction + (index % FolderMiniColumns + 0.5f) * FolderMiniCellFraction,
            FolderMiniPadFraction + (index / FolderMiniColumns + 0.5f) * FolderMiniCellFraction);

    public static void DrawApp(Vector2 center, float size, IPhoneApp app, PhoneTheme theme, float drawScale,
        float labelAlpha, bool showLabels, float labelWidth, Configuration configuration, float zoom = 1f,
        PointerState pointer = default)
    {
        var scale = UiScale.Current * zoom;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var drawMin = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var drawMax = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * Metrics.Radius.HomeTileFactor * drawScale;
        var surface = IconTile.Surface(app.Accent);
        var ink = AppAccents.InkFor(app.Id);
        var effect = app.TileEffect;
        if (effect != HomeTileEffect.None)
        {
            DrawEffectGlow(dl, drawMin, drawMax, radius, drawHalf, app.Accent);
        }

        var firstVertex = dl.VtxBuffer.Size;
        Material.PointerHalo(dl, drawMin, drawMax, radius, pointer.Lift, scale);
        if (!AppIconTile.TryDraw(dl, app.Id, app.Accent, drawMin, drawMax, radius, 1f, true, scale))
        {
            DrawAccentTile(dl, app, center, size * drawScale, drawMin, drawMax, radius, surface, ink, scale);
        }

        FinishPointer(dl, firstVertex, center, drawMin, drawMax, radius, drawHalf, pointer, scale);
        DrawLabel(center, size, app.DisplayName, theme, scale, labelAlpha, showLabels, labelWidth, zoom);
        if (app.BadgeCount > 0 && IsBadgeVisible(app, configuration))
        {
            DrawBadge(center, size, app.BadgeCount, app.BadgeAsDot, theme, scale);
        }

        if (effect == HomeTileEffect.Aura)
        {
            DrawEffectMotes(dl, center, drawHalf, app.Accent, scale);
        }
    }

    private static void DrawEffectGlow(ImDrawListPtr dl, Vector2 min, Vector2 max, float radius, float half,
        Vector4 accent)
    {
        var phase = (float)(ImGui.GetTime() * Math.Tau / EffectGlowPeriodSeconds);
        var peak = EffectGlowAlphaLow + (EffectGlowAlphaHigh - EffectGlowAlphaLow) * (0.5f + 0.5f * MathF.Sin(phase));
        var reach = half * EffectGlowReach;
        var weightTotal = EffectGlowLayers * (EffectGlowLayers + 1) * 0.5f;
        for (var layer = EffectGlowLayers; layer >= 1; layer--)
        {
            var pad = reach * layer / EffectGlowLayers;
            var alpha = peak * (EffectGlowLayers - layer + 1) / weightTotal;
            Squircle.Fill(dl, new Vector2(min.X - pad, min.Y - pad), new Vector2(max.X + pad, max.Y + pad),
                radius + pad, ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)));
        }
    }

    private static void DrawEffectMotes(ImDrawListPtr dl, Vector2 center, float half, Vector4 accent, float scale)
    {
        var time = (float)ImGui.GetTime();
        var orbit = time * (MathF.Tau / EffectMoteOrbitSeconds);
        var core = Palette.Mix(accent, EffectMoteCoreLight, EffectMoteCoreLift);
        for (var moteIndex = 0; moteIndex < EffectMoteCount; moteIndex++)
        {
            var offset = moteIndex * (MathF.Tau / EffectMoteCount);
            var angle = orbit + offset;
            var wobble = MathF.Sin(time * EffectMoteWobbleRate + moteIndex * EffectMoteWobbleStagger);
            var distance = half * (EffectMoteOrbitReach + EffectMoteOrbitWobble * wobble);
            var position = new Vector2(center.X + MathF.Cos(angle) * distance, center.Y + MathF.Sin(angle) * distance);
            var pulse = 0.5f + 0.5f * MathF.Sin(time * EffectMotePulseRate + moteIndex * EffectMotePulseStagger);
            var alpha = EffectMoteAlphaMin + (EffectMoteAlphaMax - EffectMoteAlphaMin) * pulse;
            var moteRadius = (EffectMoteRadiusMin + (EffectMoteRadiusMax - EffectMoteRadiusMin) * pulse) * scale;
            dl.AddCircleFilled(position, moteRadius * EffectMoteHaloReach,
                ImGui.GetColorU32(Palette.WithAlpha(accent, alpha * EffectMoteHaloAlpha)), EffectMoteSegments);
            dl.AddCircleFilled(position, moteRadius, ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)),
                EffectMoteSegments);
            dl.AddCircleFilled(position, moteRadius * EffectMoteCoreReach,
                ImGui.GetColorU32(Palette.WithAlpha(core, alpha)), EffectMoteSegments);
        }
    }

    private static void DrawAccentTile(ImDrawListPtr dl, IPhoneApp app, Vector2 center, float side, Vector2 min,
        Vector2 max, float radius, Vector4 surface, Vector4 ink, float scale)
    {
        Elevation.IconRest(dl, min, max, radius, scale);
        IconTile.FillShaded(dl, min, max, radius, surface);
        Material.EdgeSquircle(dl, min, max, radius, scale);
        if (AppIconArt.TryDraw(dl, app.Id, center, side, ink, Palette.Mix(surface, ink, 0.28f)))
        {
            return;
        }

        var glyphHeight = Typography.Measure(app.Glyph).Y;
        var glyphScale = glyphHeight > 0f ? side * 0.5f / glyphHeight : 1f;
        Typography.DrawCentered(center, app.Glyph, ink, glyphScale);
    }

    private static bool IsBadgeVisible(IPhoneApp app, Configuration configuration) =>
        !app.HasBadge || configuration.IsAppBadgeEnabled(app.Id);

    private static void FinishPointer(ImDrawListPtr dl, int firstVertex, Vector2 center, Vector2 min, Vector2 max,
        float radius, float half, in PointerState pointer, float scale)
    {
        if (pointer.Hovered)
        {
            Material.PointerSpecular(dl, min, max, radius, pointer.Tilt, pointer.Lift, scale);
        }

        if (pointer.Dim > 0.001f)
        {
            Squircle.Fill(dl, min, max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, pointer.Dim)));
        }

        if (pointer.Hovered)
        {
            VertexWarp.Tilt(dl, firstVertex, center, half, -pointer.Tilt, TiltDepth);
        }
    }

    public static void DrawShortcut(Vector2 center, float size, ShortcutEntry shortcut, IDalamudTextureWrap? icon,
        PhoneTheme theme, float drawScale, float labelAlpha, bool showLabels, float labelWidth, float zoom = 1f,
        PointerState pointer = default)
    {
        var scale = UiScale.Current * zoom;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var min = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var max = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * Metrics.Radius.HomeTileFactor * drawScale;
        var firstVertex = dl.VtxBuffer.Size;
        Material.PointerHalo(dl, min, max, radius, pointer.Lift, scale);
        ShortcutArt.DrawSurface(dl, center, size * drawScale, shortcut, icon, scale);
        FinishPointer(dl, firstVertex, center, min, max, radius, drawHalf, pointer, scale);
        DrawLabel(center, size, shortcut.Name, theme, scale, labelAlpha, showLabels, labelWidth, zoom);
    }

    public static void DrawFolder(Vector2 center, float size, HomeTile folder, PhoneTheme theme, float drawScale,
        float labelAlpha, bool showLabels, string fallbackName, float labelWidth,
        Func<ShortcutEntry, IDalamudTextureWrap?> shortcutIcon, Configuration configuration, float zoom = 1f,
        PointerState pointer = default)
    {
        var scale = UiScale.Current * zoom;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var min = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var max = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * Metrics.Radius.HomeTileFactor * drawScale;
        var firstVertex = dl.VtxBuffer.Size;
        Material.PointerHalo(dl, min, max, radius, pointer.Lift, scale);
        Elevation.IconRest(dl, min, max, radius, scale);
        Material.LiquidGlass(dl, min, max, radius, scale, GlassTone.Light, WallpaperLegibility.Strength(theme));
        if (!string.IsNullOrEmpty(folder.FolderTint))
        {
            Squircle.Fill(dl, min, max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ThemeCatalog.ResolveAccent(folder.FolderTint), 0.34f)));
        }

        DrawFolderMiniGrid(dl, min, max, folder, shortcutIcon, scale);
        FinishPointer(dl, firstVertex, center, min, max, radius, drawHalf, pointer, scale);
        DrawFolderLabel(center, size, folder, theme, labelAlpha, showLabels, fallbackName, labelWidth, zoom);
        var badgeTotal = 0;
        var badgeHasDot = false;
        for (var memberIndex = 0; memberIndex < folder.Members.Count; memberIndex++)
        {
            var folderApp = folder.Members[memberIndex].App;
            if (folderApp is null || folderApp.BadgeCount <= 0 || !IsBadgeVisible(folderApp, configuration))
            {
                continue;
            }

            if (folderApp.BadgeAsDot)
            {
                badgeHasDot = true;
            }
            else
            {
                badgeTotal += folderApp.BadgeCount;
            }
        }

        if (badgeTotal > 0)
        {
            DrawBadge(center, size, badgeTotal, false, theme, scale);
        }
        else if (badgeHasDot)
        {
            DrawBadge(center, size, 1, true, theme, scale);
        }
    }

    public static void DrawFolderLabel(Vector2 center, float size, HomeTile folder, PhoneTheme theme,
        float labelAlpha, bool showLabels, string fallbackName, float labelWidth, float zoom = 1f)
    {
        var name = string.IsNullOrEmpty(folder.FolderName) ? fallbackName : folder.FolderName;
        DrawLabel(center, size, name, theme, UiScale.Current * zoom, labelAlpha, showLabels, labelWidth, zoom);
    }

    public static void DrawFolderMiniGrid(ImDrawListPtr dl, Vector2 min, Vector2 max, HomeTile folder,
        Func<ShortcutEntry, IDalamudTextureWrap?> shortcutIcon, float scale)
    {
        var side = MathF.Min(max.X - min.X, max.Y - min.Y);
        var mini = side * FolderMiniIconFraction;
        var count = Math.Min(FolderMiniColumns * FolderMiniColumns, folder.Members.Count);
        for (var index = 0; index < count; index++)
        {
            var cellCenter = min + FolderMiniCenter(index) * side;
            var member = folder.Members[index];
            if (member.IsShortcut)
            {
                ShortcutArt.DrawSurface(dl, cellCenter, mini, member.Shortcut!, shortcutIcon(member.Shortcut!), scale);
                continue;
            }

            DrawMiniApp(dl, cellCenter, mini, member.App!);
        }
    }

    private static void DrawMiniApp(ImDrawListPtr dl, Vector2 center, float size, IPhoneApp app)
    {
        var half = size * 0.5f;
        var miniMin = new Vector2(center.X - half, center.Y - half);
        var miniMax = new Vector2(center.X + half, center.Y + half);
        if (AppIconTile.TryDraw(dl, app.Id, app.Accent, miniMin, miniMax, size * 0.3f, 1f, false))
        {
            return;
        }

        var surface = IconTile.Surface(app.Accent);
        var ink = AppAccents.InkFor(app.Id);
        Squircle.Fill(dl, miniMin, miniMax, size * 0.3f, ImGui.GetColorU32(surface));
        AppIconArt.TryDraw(dl, app.Id, center, size, ink, Palette.Mix(surface, ink, 0.28f));
    }

    private static void DrawBadge(Vector2 center, float size, int count, bool asDot, PhoneTheme theme, float scale)
    {
        var badgeCenter = new Vector2(center.X + size * 0.5f - 5f * scale, center.Y - size * 0.5f + 5f * scale);
        if (asDot)
        {
            AppBadge.DrawDot(badgeCenter, theme, scale);
        }
        else
        {
            AppBadge.Draw(badgeCenter, count, theme, scale);
        }
    }

    public static bool RemoveBadge(Vector2 center, float scale, PhoneTheme theme)
    {
        var radius = 9f * scale;
        var dl = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, hovered ? 1f : 0.88f)),
            24);
        var arm = radius * 0.4f;
        var ink = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f));
        dl.AddLine(new Vector2(center.X - arm, center.Y), new Vector2(center.X + arm, center.Y), ink, 1.8f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawLabel(Vector2 center, float size, string label, PhoneTheme theme, float scale,
        float labelAlpha, bool showLabels, float labelWidth, float zoom)
    {
        if (!showLabels || labelAlpha <= 0.01f)
        {
            return;
        }

        var labelCenter = new Vector2(center.X, center.Y + size * 0.5f + 11f * scale);
        var strength = WallpaperLegibility.Strength(theme);
        var halo = Palette.WithAlpha(new Vector4(0f, 0f, 0f, 1f), (0.22f + 0.30f * strength) * labelAlpha);
        var text = Palette.WithAlpha(theme.TextStrong, 0.98f * labelAlpha);
        var style = zoom == 1f
            ? TextStyles.IconLabel
            : new TextStyle(TextStyles.IconLabel.Scale * zoom, TextStyles.IconLabel.Weight);
        var available = labelWidth * 0.92f;
        var layout = HomeLabelFitter.For(label, available, style);
        var lineStyle = layout.Scale >= 0.999f ? style : new TextStyle(style.Scale * layout.Scale, style.Weight);
        var haloRadius = (1.3f + 0.5f * strength) * scale;
        Typography.DrawCenteredHalo(labelCenter, layout.First, text, halo, haloRadius, labelWidth, lineStyle);
        if (layout.Second is null)
        {
            return;
        }

        var secondCenter = new Vector2(labelCenter.X, labelCenter.Y + Typography.LineHeight(lineStyle) * 0.92f);
        Typography.DrawCenteredHalo(secondCenter, layout.Second, text, halo, haloRadius, labelWidth, lineStyle);
    }
}

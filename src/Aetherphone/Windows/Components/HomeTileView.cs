using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class HomeTileView
{
    private static readonly Vector4 IconTileBackground = new(0.078f, 0.078f, 0.078f, 1f);
    private static readonly Vector4 IconTileFrost = new(0.078f, 0.078f, 0.078f, 0.88f);

    public static void DrawApp(Vector2 center, float size, IPhoneApp app, PhoneTheme theme, float drawScale,
        float labelAlpha, float labelWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var drawMin = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var drawMax = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * 0.26f * drawScale;
        Squircle.Fill(dl, drawMin, drawMax, radius, ImGui.GetColorU32(IconTileFrost));
        Material.EdgeSquircle(dl, drawMin, drawMax, radius, scale);
        if (!AppIconArt.TryDraw(app.Id, center, size * drawScale, app.Accent, IconTileBackground))
        {
            var glyphHeight = Typography.Measure(app.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * drawScale * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(center, app.Glyph, app.Accent, glyphScale);
        }

        DrawLabel(center, size, app.DisplayName, theme, scale, labelAlpha, labelWidth);
        if (app.BadgeCount > 0)
        {
            var badgeCenter = new Vector2(center.X + size * 0.5f - 5f * scale, center.Y - size * 0.5f + 5f * scale);
            if (app.BadgeAsDot)
            {
                AppBadge.DrawDot(badgeCenter, theme, scale);
            }
            else
            {
                AppBadge.Draw(badgeCenter, app.BadgeCount, theme, scale);
            }
        }
    }

    public static void DrawFolder(Vector2 center, float size, HomeTile folder, PhoneTheme theme, float drawScale,
        float labelAlpha, string fallbackName, float labelWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var min = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var max = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * 0.26f * drawScale;
        Squircle.Fill(dl, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)));
        Material.EdgeSquircle(dl, min, max, radius, scale);
        var pad = drawHalf * 0.28f;
        var inner = drawHalf * 2f - pad * 2f;
        var cell = inner / 3f;
        var mini = cell * 0.78f;
        var count = Math.Min(9, folder.Apps.Count);
        for (var index = 0; index < count; index++)
        {
            var col = index % 3;
            var row = index / 3;
            var cellCenter = new Vector2(min.X + pad + (col + 0.5f) * cell, min.Y + pad + (row + 0.5f) * cell);
            var miniMin = new Vector2(cellCenter.X - mini * 0.5f, cellCenter.Y - mini * 0.5f);
            var miniMax = new Vector2(cellCenter.X + mini * 0.5f, cellCenter.Y + mini * 0.5f);
            var appItem = folder.Apps[index];
            Squircle.Fill(dl, miniMin, miniMax, mini * 0.3f, ImGui.GetColorU32(IconTileBackground));
            AppIconArt.TryDraw(appItem.Id, cellCenter, mini, appItem.Accent, IconTileBackground);
        }

        var name = string.IsNullOrEmpty(folder.FolderName) ? fallbackName : folder.FolderName;
        DrawLabel(center, size, name, theme, scale, labelAlpha, labelWidth);
    }

    private static void DrawLabel(Vector2 center, float size, string label, PhoneTheme theme, float scale,
        float labelAlpha, float labelWidth)
    {
        if (labelAlpha <= 0.01f)
        {
            return;
        }

        var labelCenter = new Vector2(center.X, center.Y + size * 0.5f + 11f * scale);
        var halo = Palette.WithAlpha(new Vector4(0f, 0f, 0f, 1f), 0.22f * labelAlpha);
        var text = Palette.WithAlpha(theme.TextStrong, 0.98f * labelAlpha);
        Typography.DrawCenteredHalo(labelCenter, label, text, halo, 1.3f * scale, labelWidth * 0.92f,
            TextStyles.IconLabel);
    }
}

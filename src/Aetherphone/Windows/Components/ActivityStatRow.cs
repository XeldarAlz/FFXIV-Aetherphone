using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ActivityStatRow
{
    public const float RowHeight = 60f;

    private const float TileSize = 32f;

    public static void Draw(Rect row, PhoneTheme theme, Vector4 tint, FontAwesomeIcon icon, string label, string value, string? detail = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, tint, icon);

        var textLeft = row.Min.X + tile + 12f * scale;
        if (detail is { Length: > 0 })
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), label, theme.TextStrong, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), detail, theme.TextMuted, TextStyles.Footnote);
        }
        else
        {
            var labelSize = Typography.Measure(label, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong, TextStyles.Headline);
        }

        if (value.Length > 0)
        {
            var valueSize = Typography.Measure(value, 1.06f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, tint, 1.06f, FontWeight.SemiBold);
        }
    }

    public static void DrawProgress(Rect row, PhoneTheme theme, Vector4 tint, FontAwesomeIcon icon, string label, string value, float fraction, string? detail = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, tint, icon);

        var textLeft = row.Min.X + tile + 12f * scale;
        var topY = row.Min.Y + 9f * scale;

        var valueSize = Typography.Measure(value, 1.0f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, topY), value, tint, 1.0f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, topY), label, theme.TextStrong, TextStyles.Headline);

        if (detail is { Length: > 0 })
        {
            Typography.Draw(new Vector2(textLeft, topY + 19f * scale), detail, theme.TextMuted, TextStyles.Caption1);
        }

        var barTop = row.Max.Y - 16f * scale;
        var barMin = new Vector2(textLeft, barTop);
        var barMax = new Vector2(row.Max.X, barTop + 5f * scale);
        var rounding = (barMax.Y - barMin.Y) * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(theme.SurfaceMuted), rounding);

        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped > 0.001f)
        {
            var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * clamped, barMax.Y);
            drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(tint), rounding);
        }
    }

    private static void IconTile(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.30f, ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.48f);
    }
}

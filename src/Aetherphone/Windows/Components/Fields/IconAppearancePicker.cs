using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class IconAppearancePicker
{
    public const float Height = 108f;
    private const float PreviewTile = 32f;
    private const float LabelGap = 5f;
    private const float HighlightInset = 3f;
    private const float HighlightPadY = 6f;
    private const float HighlightFillAlpha = 0.14f;
    private const float HighlightStrokeAlpha = 0.55f;

    private static readonly IconAppearance[] Order =
    {
        IconAppearance.Default, IconAppearance.Dark, IconAppearance.Tinted, IconAppearance.Clear,
    };

    private static readonly string[] PreviewCandidates = { "message", "messages", "photos", "settings", "music" };
    private static Spring highlight = new(0f);
    private static bool highlightPrimed;

    public static IconAppearance Draw(Rect row, IconAppearance selected, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var title = Loc.T(L.Settings.IconAppearance);
        var titleTop = row.Min.Y + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(row.Min.X, titleTop), title, theme.TextStrong, TextStyles.Body);
        var cellsTop = titleTop + Typography.LineHeight(TextStyles.Body) + Metrics.Space.Xs * scale;
        var cellsBottom = row.Max.Y - Metrics.Space.Sm * scale;
        var cellWidth = row.Width / Order.Length;
        var tile = PreviewTile * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Caption1);
        var blockHeight = tile + LabelGap * scale + labelHeight;
        var blockTop = cellsTop + MathF.Max(0f, (cellsBottom - cellsTop - blockHeight) * 0.5f);
        var selectedIndex = IndexOf(selected);
        var position = AnimateHighlight(selectedIndex);
        var padY = HighlightPadY * scale;
        DrawHighlight(drawList, row, blockTop - padY, blockTop + blockHeight + padY, cellWidth, position, theme,
            scale);
        var previewId = PreviewId();
        var result = selected;
        for (var index = 0; index < Order.Length; index++)
        {
            var cellMin = new Vector2(row.Min.X + index * cellWidth, cellsTop);
            var cellMax = new Vector2(cellMin.X + cellWidth, cellsBottom);
            var hovered = UiInteract.Hover(cellMin, cellMax);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cellMin, cellMax, hovered))
            {
                result = Order[index];
            }

            var centerX = cellMin.X + cellWidth * 0.5f;
            var tileMin = new Vector2(centerX - tile * 0.5f, blockTop);
            var tileMax = new Vector2(centerX + tile * 0.5f, blockTop + tile);
            DrawPreview(drawList, previewId, Order[index], tileMin, tileMax, tile * Metrics.Radius.TileFactor, scale);
            var proximity = 1f - Math.Clamp(MathF.Abs(position - index), 0f, 1f);
            var ink = Vector4.Lerp(theme.TextMuted, theme.TextStrong, proximity);
            var label = Typography.FitText(Label(Order[index]), cellWidth - Metrics.Space.Sm * scale,
                TextStyles.Caption1);
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(centerX - labelSize.X * 0.5f, tileMax.Y + LabelGap * scale), label,
                ink, TextStyles.Caption1);
        }

        return result;
    }

    private static void DrawHighlight(ImDrawListPtr drawList, Rect row, float top, float bottom, float cellWidth,
        float position, PhoneTheme theme, float scale)
    {
        var centerX = row.Min.X + (position + 0.5f) * cellWidth;
        var half = cellWidth * 0.5f - HighlightInset * scale;
        var min = new Vector2(centerX - half, top);
        var max = new Vector2(centerX + half, bottom);
        var radius = Metrics.Radius.Md * scale;
        Squircle.Fill(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, HighlightFillAlpha)));
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, HighlightStrokeAlpha)), 1f * scale);
    }

    private static void DrawPreview(ImDrawListPtr drawList, string appId, IconAppearance appearance, Vector2 min,
        Vector2 max, float radius, float scale)
    {
        var accent = AppAccents.For(appId);
        if (AppIconTile.TryDraw(drawList, appId, accent, min, max, radius, 1f, appearance, true, scale))
        {
            return;
        }

        var surface = IconTile.Surface(accent);
        Elevation.IconRest(drawList, min, max, radius, scale);
        IconTile.FillShaded(drawList, min, max, radius, surface);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        var ink = AccentRing.Ink;
        AppIconArt.TryDraw(drawList, appId, (min + max) * 0.5f, max.X - min.X, ink, Palette.Mix(surface, ink, 0.28f));
    }

    private static float AnimateHighlight(int selectedIndex)
    {
        if (!highlightPrimed)
        {
            highlightPrimed = true;
            highlight.SnapTo(selectedIndex);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        return highlight.Step(selectedIndex, Motion.Release, deltaSeconds);
    }

    private static string PreviewId()
    {
        for (var index = 0; index < PreviewCandidates.Length; index++)
        {
            if (AppIconCache.IsPainted(PreviewCandidates[index]))
            {
                return PreviewCandidates[index];
            }
        }

        return PreviewCandidates[0];
    }

    private static int IndexOf(IconAppearance appearance)
    {
        for (var index = 0; index < Order.Length; index++)
        {
            if (Order[index] == appearance)
            {
                return index;
            }
        }

        return 0;
    }

    private static string Label(IconAppearance appearance) =>
        appearance switch
        {
            IconAppearance.Dark => Loc.T(L.Settings.IconAppearanceDark),
            IconAppearance.Tinted => Loc.T(L.Settings.IconAppearanceTinted),
            IconAppearance.Clear => Loc.T(L.Settings.IconAppearanceClear),
            _ => Loc.T(L.Settings.IconAppearanceDefault),
        };
}

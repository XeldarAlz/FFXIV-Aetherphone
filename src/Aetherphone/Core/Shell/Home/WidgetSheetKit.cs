using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell.Home;

internal enum SheetHeaderAction : byte
{
    None,
    Back,
    Done,
}

internal static class WidgetSheetKit
{
    public const float HeaderUnits = 44f;
    public const float RowUnits = 46f;
    public const float SidePadUnits = 16f;
    public const float SectionGapUnits = 14f;
    public const float BottomInsetUnits = 24f;
    public const float RowPadUnits = 14f;
    private const float CardRadiusUnits = 12f;
    private const float CardFillAlpha = 0.08f;
    private const float HairlineAlpha = 0.14f;
    private const float HoverAlpha = 0.07f;
    private const float MutedAlpha = 0.6f;
    private const float ButtonReserveUnits = 84f;
    private const float GlyphUnits = 15f;
    private const float PreviewSmallFraction = 0.42f;
    private const float PreviewSmallMaxUnits = 158f;
    private const float PreviewWideMaxUnits = 338f;
    private const float PreviewMediumAspect = 0.47f;
    private const float PreviewLargeFraction = 0.82f;
    private const float PreviewLargeAspect = 0.95f;

    public static Vector4 Faded(Vector4 color, float opacity) => color with { W = color.W * opacity };

    public static Vector4 Muted(Vector4 ink, float opacity) => ink with { W = ink.W * MutedAlpha * opacity };

    public static SheetHeaderAction Header(ImDrawListPtr drawList, Rect content, string title, Vector4 ink,
        Vector4 accent, float opacity, float scale, bool interactive, bool showBack)
    {
        var height = HeaderUnits * scale;
        var centerY = content.Min.Y + height * 0.5f;
        var reserve = ButtonReserveUnits * scale;
        var titleWidth = MathF.Max(1f, content.Width - reserve * 2f);
        var fitted = Typography.FitText(title, titleWidth, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, centerY), fitted, Faded(ink, opacity),
            TextStyles.Headline);
        var pad = SidePadUnits * scale;
        var doneLabel = Loc.T(L.Home.Done);
        var doneSize = Typography.Measure(doneLabel, TextStyles.Headline);
        var doneRect = new Rect(new Vector2(content.Max.X - pad - doneSize.X - 8f * scale, centerY - height * 0.4f),
            new Vector2(content.Max.X - pad + 8f * scale, centerY + height * 0.4f));
        if (TextAction(drawList, doneRect, doneLabel, accent, opacity, interactive))
        {
            return SheetHeaderAction.Done;
        }

        if (!showBack)
        {
            return SheetHeaderAction.None;
        }

        var backCenter = new Vector2(content.Min.X + pad + 6f * scale, centerY);
        var half = height * 0.4f;
        var backRect = new Rect(backCenter - new Vector2(half, half), backCenter + new Vector2(half, half));
        var hovered = interactive && UiInteract.Hover(backRect.Min, backRect.Max);
        PhoneIcon.Draw(drawList, backCenter, IconGlyph.Of(FontAwesomeIcon.ChevronLeft),
            Faded(accent, opacity * (hovered ? 1f : 0.9f)), GlyphUnits * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(backRect.Min, backRect.Max, hovered) ? SheetHeaderAction.Back : SheetHeaderAction.None;
    }

    public static Rect Preview(Rect content, float top, WidgetSize size, float scale)
    {
        var available = MathF.Max(1f, content.Width - SidePadUnits * 2f * scale);
        float width;
        float height;
        switch (size)
        {
            case WidgetSize.Small:
                width = MathF.Min(available * PreviewSmallFraction, PreviewSmallMaxUnits * scale);
                height = width;
                break;
            case WidgetSize.Large:
                width = MathF.Min(available * PreviewLargeFraction, PreviewWideMaxUnits * scale);
                height = width * PreviewLargeAspect;
                break;
            default:
                width = MathF.Min(available, PreviewWideMaxUnits * scale);
                height = width * PreviewMediumAspect;
                break;
        }

        var left = content.Center.X - width * 0.5f;
        return new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
    }

    public static float PreviewHeight(Rect content, WidgetSize size, float scale) =>
        Preview(content, 0f, size, scale).Height;

    public static void Card(ImDrawListPtr drawList, Rect card, Vector4 ink, float opacity, float scale) =>
        Squircle.Fill(drawList, card.Min, card.Max, CardRadiusUnits * scale,
            ImGui.GetColorU32(ink with { W = CardFillAlpha * opacity }));

    public static void Hairline(ImDrawListPtr drawList, Rect card, float y, Vector4 ink, float opacity, float scale)
    {
        var inset = RowPadUnits * scale;
        drawList.AddLine(new Vector2(card.Min.X + inset, y), new Vector2(card.Max.X, y),
            ImGui.GetColorU32(ink with { W = HairlineAlpha * opacity }), MathF.Max(1f, 0.5f * scale));
    }

    public static bool HoverWash(ImDrawListPtr drawList, Rect row, Vector4 ink, float opacity, float scale,
        bool interactive)
    {
        var hovered = interactive && UiInteract.Hover(row.Min, row.Max);
        if (!hovered)
        {
            return false;
        }

        Squircle.Fill(drawList, row.Min, row.Max, CardRadiusUnits * scale,
            ImGui.GetColorU32(ink with { W = HoverAlpha * opacity }));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    public static float Label(ImDrawListPtr drawList, Rect row, string text, Vector4 color, float maxWidth,
        float scale)
    {
        var left = row.Min.X + RowPadUnits * scale;
        var fitted = Typography.FitText(text, MathF.Max(1f, maxWidth), TextStyles.Body);
        var size = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - size.Y * 0.5f), fitted, color, TextStyles.Body);
        return left + size.X;
    }

    public static void TrailingText(ImDrawListPtr drawList, Rect row, string text, Vector4 color, float right,
        float maxWidth)
    {
        if (text.Length == 0)
        {
            return;
        }

        var fitted = Typography.FitText(text, MathF.Max(1f, maxWidth), TextStyles.Body);
        var size = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(right - size.X, row.Center.Y - size.Y * 0.5f), fitted, color,
            TextStyles.Body);
    }

    public static void Glyph(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Vector4 color,
        float scale, float units = GlyphUnits) =>
        PhoneIcon.Draw(drawList, center, IconGlyph.Of(icon), color, units * scale);

    public static void Check(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var ink = ImGui.GetColorU32(color);
        var thickness = 2f * scale;
        drawList.AddLine(center + new Vector2(-5f, 0f) * scale, center + new Vector2(-1.5f, 3.6f) * scale, ink,
            thickness);
        drawList.AddLine(center + new Vector2(-1.5f, 3.6f) * scale, center + new Vector2(5.2f, -4f) * scale, ink,
            thickness);
    }

    public static void WheelScroll(ref float scrollY, Rect view, float contentHeight, float scale, bool interactive)
    {
        if (interactive && UiInteract.Hover(view.Min, view.Max))
        {
            scrollY -= ImGui.GetIO().MouseWheel * RowUnits * scale;
        }

        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, contentHeight - view.Height));
    }

    private static bool TextAction(ImDrawListPtr drawList, Rect rect, string label, Vector4 color, float opacity,
        bool interactive)
    {
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        Typography.DrawCentered(drawList, rect.Center, label, Faded(color, opacity * (hovered ? 1f : 0.9f)),
            TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}

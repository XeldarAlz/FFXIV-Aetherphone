using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal static class FeedbackArt
{
    public const float StateTileSize = 64f;
    public const float PillHeight = 22f;

    private const float GlyphFraction = 0.5f;
    private const float PillPadX = 9f;
    private const float PillFillAlpha = 0.18f;
    private const float PillInkLighten = 0.35f;
    private const float StateGap = 14f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = 40f;
    private const float StateActionPad = 44f;
    private const float StateMaxTextWidth = 290f;
    private const float CheckShortArm = 0.36f;
    private const float CheckLongArm = 0.64f;
    private const float DisabledFillAlpha = 0.35f;
    private const float DisabledInkAlpha = 0.7f;
    private const float HoverDarken = 0.12f;
    private const float SpinnerRadius = 8f;
    private const float SpinnerGap = 8f;
    private const float SpinnerThickness = 2.2f;
    private const float SpinnerTrackAlpha = 0.3f;
    private const float SpinnerArc = 1.9f;
    private const double SpinnerSpeed = 6.5;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private static readonly Vector2 CheckStart = new(-0.40f, 0.02f);
    private static readonly Vector2 CheckCorner = new(-0.12f, 0.30f);
    private static readonly Vector2 CheckEnd = new(0.42f, -0.28f);

    public static void CategoryTile(ImDrawListPtr drawList, Vector2 center, float size, in FeedbackKind kind,
        float alpha = 1f)
    {
        GlyphTile(drawList, center, size, kind.Icon, kind.Tint, alpha);
    }

    public static void GlyphTile(ImDrawListPtr drawList, Vector2 center, float size, FontAwesomeIcon icon,
        Vector4 tint, float alpha = 1f)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(tint), alpha);
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink with { W = alpha }, size * GlyphFraction);
    }

    public static float PillWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.FootnoteEmphasized).X + PillPadX * 2f * scale;

    public static void StatusPill(ImDrawListPtr drawList, Vector2 min, string label, Vector4 tint, float scale)
    {
        var height = PillHeight * scale;
        var max = new Vector2(min.X + PillWidth(label, scale), min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, PillFillAlpha)));
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + PillPadX * scale, min.Y + (height - size.Y) * 0.5f), label,
            Palette.Lighten(tint, PillInkLighten), TextStyles.FootnoteEmphasized);
    }

    public static void Check(ImDrawListPtr drawList, Vector2 center, float radius, float progress, Vector4 color,
        float thickness)
    {
        if (progress <= 0f)
        {
            return;
        }

        var start = center + CheckStart * radius;
        var corner = center + CheckCorner * radius;
        var end = center + CheckEnd * radius;
        var packed = ImGui.GetColorU32(color);
        var shortShare = Math.Clamp(progress / CheckShortArm, 0f, 1f);
        drawList.AddLine(start, Vector2.Lerp(start, corner, shortShare), packed, thickness);
        if (progress <= CheckShortArm)
        {
            return;
        }

        var longShare = Math.Clamp((progress - CheckShortArm) / CheckLongArm, 0f, 1f);
        drawList.AddCircleFilled(corner, thickness * 0.5f, packed, 12);
        drawList.AddLine(corner, Vector2.Lerp(corner, end, longShare), packed, thickness);
    }

    public static float StatePanelHeight(string title, string hint, bool hasAction, float maxWidth, float scale)
    {
        var textWidth = MathF.Min(maxWidth, StateMaxTextWidth * scale);
        var height = StateTileSize * scale + StateGap * scale
            + Typography.MeasureWrappedBlock(title, TextStyles.Title3, textWidth).Y;
        if (hint.Length > 0)
        {
            height += StateHintGap * scale + Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, textWidth).Y;
        }

        if (hasAction)
        {
            height += (StateActionGap + StateActionHeight) * scale;
        }

        return height;
    }

    public static bool StatePanel(AppSkin ui, float top, float centerX, float maxWidth, FontAwesomeIcon icon,
        Vector4 tint, string title, string hint, string actionLabel, string actionId)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var textWidth = MathF.Min(maxWidth, StateMaxTextWidth * scale);
        var tileSize = StateTileSize * scale;
        GlyphTile(drawList, new Vector2(centerX, top + tileSize * 0.5f), tileSize, icon, tint);
        var cursorY = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, top + tileSize + StateGap * scale), textWidth);
        if (hint.Length > 0)
        {
            cursorY = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, cursorY + StateHintGap * scale), textWidth);
        }

        if (actionLabel.Length == 0)
        {
            return false;
        }

        var width = MathF.Min(maxWidth, Typography.Measure(actionLabel, TextStyles.Headline).X + StateActionPad * scale);
        var actionTop = cursorY + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, actionTop),
            new Vector2(centerX + width * 0.5f, actionTop + StateActionHeight * scale));
        return PressPill(ui, rect, actionLabel, true, actionId);
    }

    public static bool SendPill(AppSkin ui, Rect rect, string label, bool enabled, bool busy, string id)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        var half = rect.Size * 0.5f * new Vector2(grow, grow);
        var min = rect.Center - half;
        var max = rect.Center + half;
        var active = enabled || busy;
        var fill = !active ? Palette.WithAlpha(ui.Accent, DisabledFillAlpha)
            : hovered ? Palette.Mix(ui.Accent, Black, HoverDarken) : ui.Accent;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(fill));
        var ink = active ? AccentRing.Ink : AccentRing.Ink with { W = DisabledInkAlpha };
        var spinnerRadius = busy ? SpinnerRadius * scale : 0f;
        var spinnerSlot = busy ? spinnerRadius * 2f + SpinnerGap * scale : 0f;
        var maxLabel = MathF.Max(1f, max.X - min.X - (max.Y - min.Y) - spinnerSlot);
        var fitted = Typography.FitText(label, maxLabel, TextStyles.Headline);
        var size = Typography.Measure(fitted, TextStyles.Headline);
        var startX = (min.X + max.X - size.X - spinnerSlot) * 0.5f;
        var centerY = (min.Y + max.Y) * 0.5f;
        if (busy)
        {
            DrawSpinner(drawList, new Vector2(startX + spinnerRadius, centerY), spinnerRadius, ink, scale);
        }

        Typography.Draw(drawList, new Vector2(startX + spinnerSlot, centerY - size.Y * 0.5f), fitted, ink,
            TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static void DrawSpinner(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 ink, float scale)
    {
        var thickness = SpinnerThickness * scale;
        ProgressRing.Track(drawList, center, radius, thickness, ink with { W = ink.W * SpinnerTrackAlpha });
        var start = (float)(ImGui.GetTime() * SpinnerSpeed % (MathF.PI * 2f));
        drawList.PathArcTo(center, radius, start, start + SpinnerArc, 16);
        drawList.PathStroke(ImGui.GetColorU32(ink), ImDrawFlags.None, thickness);
    }

    public static bool PressPill(AppSkin ui, Rect rect, string label, bool enabled, string id)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        var half = rect.Size * 0.5f * grow;
        ui.PaintAccentPill(new Rect(rect.Center - half, rect.Center + half), label, enabled, hovered,
            TextStyles.Headline);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}

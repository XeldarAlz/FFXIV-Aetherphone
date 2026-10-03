using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomeChrome
{
    private const float PillWidthUnits = 96f;
    private const float PillHeightUnits = 26f;
    private const float DotSpacingUnits = 14f;
    private const float DotRadiusUnits = 3f;
    private const float DotsPillPadUnits = 11f;
    private const float DotsPillHeightUnits = 18f;

    private readonly Pager pager;
    private readonly HomeInteractionController interaction;
    private readonly Spotlight.SpotlightOverlay spotlight;
    private Spring dotsPresence;

    public HomeChrome(Pager pager, HomeInteractionController interaction, Spotlight.SpotlightOverlay spotlight)
    {
        this.pager = pager;
        this.interaction = interaction;
        this.spotlight = spotlight;
    }

    public void DrawPageControls(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var pageCount = interaction.DisplayPageCount();
        var scale = metrics.Scale;
        var paging = pager.Dragging || MathF.Abs(pager.Value - MathF.Round(pager.Value)) > 0.02f;
        var dotsWanted = pageCount > 1 && (paging || interaction.Editing || interaction.DragTile is not null);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        dotsPresence.Step(dotsWanted ? 1f : 0f, Motion.Appear, delta);
        var dots = Math.Clamp(dotsPresence.Value, 0f, 1f);
        if (interaction.Editing && dots <= 0.01f)
        {
            DrawPageArrows(metrics, theme, alpha, interactive, pageCount);
            return;
        }

        var spacing = DotSpacingUnits * scale;
        var searchHalf = new Vector2(PillWidthUnits * 0.5f * scale, PillHeightUnits * 0.5f * scale);
        var dotsHalf = new Vector2(((Math.Max(pageCount, 1) - 1) * spacing) * 0.5f + DotsPillPadUnits * scale,
            DotsPillHeightUnits * 0.5f * scale);
        var half = Vector2.Lerp(searchHalf, dotsHalf, dots);
        var center = new Vector2(metrics.Content.Center.X, metrics.DotsCenterY);
        var pill = new Rect(center - half, center + half);
        var searchLive = interactive && !interaction.Editing && interaction.DragTile is null && dots < 0.5f;
        var hovered = searchLive && UiInteract.Hover(pill.Min, pill.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("home.searchpill", pressed);
        var drawHalf = half * press;
        var drawList = ImGui.GetWindowDrawList();
        Material.LiquidGlass(drawList, center - drawHalf, center + drawHalf, drawHalf.Y, scale, GlassTone.Light,
            WallpaperLegibility.Strength(theme), alpha);
        if (hovered)
        {
            Squircle.Fill(drawList, center - drawHalf, center + drawHalf, drawHalf.Y,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * alpha)));
        }

        var searchAlpha = alpha * (1f - dots);
        if (searchAlpha > 0.01f)
        {
            UiAnchors.Report("home.search", pill);
            DrawSearchContent(drawList, center, press, theme, searchAlpha, scale);
            if (UiInteract.Click(pill.Min, pill.Max, hovered))
            {
                interaction.CancelPress();
                spotlight.Open();
            }
        }

        if (dots > 0.01f && pageCount > 1)
        {
            DrawDots(drawList, center, pageCount, spacing, theme, alpha * dots, interactive && dots >= 0.5f, scale);
        }

        DrawPageArrows(metrics, theme, alpha, interactive, pageCount);
    }

    private void DrawDots(ImDrawListPtr drawList, Vector2 center, int pageCount, float spacing, PhoneTheme theme,
        float alpha, bool interactive, float scale)
    {
        var radius = DotRadiusUnits * scale;
        var startX = center.X - (pageCount - 1) * spacing * 0.5f;
        var active = Math.Clamp((int)MathF.Round(pager.Value), 0, pageCount - 1);
        for (var index = 0; index < pageCount; index++)
        {
            var dot = new Vector2(startX + index * spacing, center.Y);
            var hovered = interactive && interaction.DragTile is null &&
                          UiInteract.Hover(dot - new Vector2(spacing * 0.5f), dot + new Vector2(spacing * 0.5f));
            var dotAlpha = index == active ? 0.95f : hovered ? 0.55f : 0.32f;
            drawList.AddCircleFilled(dot, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, dotAlpha * alpha)), 16);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                pager.AnimateTo(index, pageCount);
                interaction.CancelPress();
            }
        }
    }

    private void DrawPageArrows(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive, int pageCount)
    {
        if (pageCount <= 1)
        {
            return;
        }

        DrawPageArrow(metrics, theme, alpha, interactive, -1, pageCount);
        DrawPageArrow(metrics, theme, alpha, interactive, 1, pageCount);
    }

    private static void DrawSearchContent(ImDrawListPtr drawList, Vector2 center, float press, PhoneTheme theme,
        float alpha, float scale)
    {
        var label = Loc.T(L.Spotlight.Search);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var iconHeight = 10f * scale * press;
        var gap = 5f * scale;
        var contentWidth = iconHeight + gap + labelSize.X * press;
        var left = center.X - contentWidth * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconHeight * 0.5f, center.Y),
            Dalamud.Interface.FontAwesomeIcon.Search, Palette.WithAlpha(theme.TextStrong, 0.85f * alpha), iconHeight);
        Typography.Draw(drawList, new Vector2(left + iconHeight + gap, center.Y - labelSize.Y * 0.5f * press), label,
            Palette.WithAlpha(theme.TextStrong, 0.85f * alpha), TextStyles.FootnoteEmphasized);
    }

    private void DrawPageArrow(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive, int direction,
        int pageCount)
    {
        var target = pager.Page + direction;
        if (target < 0 || target > pageCount - 1)
        {
            return;
        }

        var scale = metrics.Scale;
        var tabWidth = 20f * scale;
        var tabHalfHeight = 30f * scale;
        var centerY = metrics.Grid.Center.Y;
        var leftEdge = metrics.Content.Min.X - theme.SidePadding * scale;
        var rightEdge = metrics.Content.Max.X + theme.SidePadding * scale;
        var tab = direction < 0
            ? new Rect(new Vector2(leftEdge, centerY - tabHalfHeight),
                new Vector2(leftEdge + tabWidth, centerY + tabHalfHeight))
            : new Rect(new Vector2(rightEdge - tabWidth, centerY - tabHalfHeight),
                new Vector2(rightEdge, centerY + tabHalfHeight));
        var hit = new Rect(tab.Min - new Vector2(4f * scale), tab.Max + new Vector2(4f * scale));
        var hovered = interactive && interaction.DragTile is null && UiInteract.Hover(hit.Min, hit.Max);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 7f * scale;
        var corners = direction < 0 ? ImDrawFlags.RoundCornersRight : ImDrawFlags.RoundCornersLeft;
        drawList.AddRectFilled(tab.Min, tab.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, (hovered ? 0.42f : 0.28f) * alpha)), rounding, corners);
        drawList.AddRect(tab.Min, tab.Max,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 0.35f : 0.18f) * alpha)), rounding, corners,
            1f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var center = tab.Center;
        var reach = 4.2f * scale;
        var thickness = 2.2f * scale;
        var tip = new Vector2(center.X + reach * 0.55f * direction, center.Y);
        var upper = new Vector2(tip.X - reach * direction, tip.Y - reach);
        var lower = new Vector2(tip.X - reach * direction, tip.Y + reach);
        var ink = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 1f : 0.85f) * alpha));
        drawList.AddLine(upper, tip, ink, thickness);
        drawList.AddLine(tip, lower, ink, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(upper, cap, ink, 8);
        drawList.AddCircleFilled(tip, cap, ink, 8);
        drawList.AddCircleFilled(lower, cap, ink, 8);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pager.AnimateTo(target, pageCount);
            interaction.CancelPress();
            interaction.CancelTap();
        }
    }

    public void DrawEditChrome(Rect content, in HomeMetrics metrics, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = metrics.Scale;
        var add = AddRect(content, metrics);
        var addHovered = UiInteract.Hover(add.Min, add.Max);
        var addCenter = add.Center;
        drawList.AddCircleFilled(addCenter, add.Width * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, addHovered ? 0.26f : 0.17f)), 32);
        var arm = add.Width * 0.22f;
        var ink = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.95f));
        drawList.AddLine(addCenter - new Vector2(arm, 0f), addCenter + new Vector2(arm, 0f), ink, 2f * scale);
        drawList.AddLine(addCenter - new Vector2(0f, arm), addCenter + new Vector2(0f, arm), ink, 2f * scale);
        if (addHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var done = DoneRect(content, metrics);
        var doneHovered = UiInteract.Hover(done.Min, done.Max);
        Squircle.Fill(drawList, done.Min, done.Max, done.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, doneHovered ? 1f : 0.88f)));
        Typography.DrawCentered(done.Center, Loc.T(L.Home.Done), new Vector4(1f, 1f, 1f, 1f), 0.82f,
            FontWeight.SemiBold);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    public static Rect DoneRect(Rect content, in HomeMetrics metrics)
    {
        var width = 64f * metrics.Scale;
        var height = 30f * metrics.Scale;
        var max = new Vector2(content.Max.X - 4f * metrics.Scale, content.Min.Y + height);
        return new Rect(new Vector2(max.X - width, content.Min.Y), max);
    }

    public static Rect AddRect(Rect content, in HomeMetrics metrics)
    {
        var size = 30f * metrics.Scale;
        var min = new Vector2(content.Min.X + 4f * metrics.Scale, content.Min.Y);
        return new Rect(min, min + new Vector2(size, size));
    }
}

using Aetherphone.Apps.Velvet;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet.Kit;

internal readonly record struct VFilterSelection(int Mask, int GenderMask, string Region);

internal sealed class VFilterSheet
{
    private const float RevealSeconds = 0.16f;
    private const float PanelWidth = 250f;
    private const float HeaderHeight = 34f;
    private const float RegionHeaderHeight = 30f;
    private const float RegionRowHeight = 46f;
    private const float GenderHeaderHeight = 30f;
    private const float PillRowHeight = 34f;
    private const int PillColumns = 2;
    private const float FooterHeight = 54f;

    private static int PillRowsFor(int optionCount) => (optionCount + PillColumns - 1) / PillColumns;

    private bool open;
    private double openedAt;
    private int openedFrame;
    private Rect anchor;

    public bool Open => open;

    public void Toggle(Rect anchorRect)
    {
        if (open)
        {
            Close();
            return;
        }

        anchor = anchorRect;
        open = true;
        openedAt = ImGui.GetTime();
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close() => open = false;

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public VFilterSelection Draw(Rect screen, float scale, int mask, int genderMask, string region)
    {
        if (!open)
        {
            return new VFilterSelection(mask, genderMask, region);
        }

        var defs = VelvetIntent.All;
        var drawList = ImGui.GetForegroundDrawList();
        var reveal = Easing.EaseOutQuint(Math.Clamp((float)((ImGui.GetTime() - openedAt) / RevealSeconds), 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.7f, 0f, 1f));

        var width = MathF.Min(PanelWidth * scale, screen.Width - 24f * scale);
        var headerHeight = HeaderHeight * scale;
        var footerHeight = FooterHeight * scale;
        var padY = 8f * scale;
        var radius = 16f * scale;
        var intentGridHeight = PillRowsFor(defs.Length) * PillRowHeight * scale;
        var genderSectionHeight = GenderHeaderHeight * scale
            + PillRowsFor(VelvetGender.All.Length) * PillRowHeight * scale;
        var height = padY + headerHeight + intentGridHeight + genderSectionHeight
            + RegionHeaderHeight * scale + RegionRowHeight * scale + footerHeight + padY;

        var margin = 8f * scale;
        var left = Math.Clamp(anchor.Max.X - width, screen.Min.X + margin,
            MathF.Max(screen.Min.X + margin, screen.Max.X - margin - width));
        var top = anchor.Max.Y + 6f * scale;
        if (top + height > screen.Max.Y - margin)
        {
            top = anchor.Min.Y - 6f * scale - height;
        }

        top = MathF.Max(top, screen.Min.Y + margin);

        var pivot = new Vector2(Math.Clamp(anchor.Center.X, left, left + width), top);
        var revealScale = 0.94f + 0.06f * reveal;
        var min = pivot + (new Vector2(left, top) - pivot) * revealScale;
        var max = pivot + (new Vector2(left + width, top + height) - pivot) * revealScale;

        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Squircle.Fill(drawList, min, max, radius, VelvetTheme.Alpha(VelvetTheme.CardHi, MathF.Min(0.99f, alpha)).Packed());
        Squircle.Stroke(drawList, min, max, radius, VelvetTheme.Alpha(VelvetTheme.CardStroke, alpha).Packed(), 1f * scale);
        Material.EdgeSquircle(drawList, min, max, radius, scale);

        var headerCenterY = min.Y + padY + headerHeight * 0.5f;
        var headerLabel = Loc.T(L.Velvet.LookingForLabel);
        var headerSize = Typography.Measure(headerLabel, 0.82f, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, headerCenterY - headerSize.Y * 0.5f), headerLabel,
            VelvetTheme.Alpha(VelvetTheme.HeaderInk, alpha), 0.82f, FontWeight.SemiBold);

        var rowsTop = min.Y + padY + headerHeight;
        for (var index = 0; index < defs.Length; index++)
        {
            var def = defs[index];
            var selected = VelvetIntent.Has(mask, def.Flag);
            if (DrawPill(drawList, min, max, rowsTop, index, Loc.T(def.Label), def.Hue, selected, alpha, scale))
            {
                mask = VelvetIntent.Toggle(mask, def.Flag);
            }
        }

        var genderTop = rowsTop + intentGridHeight;
        DrawGenderSection(drawList, min, max, genderTop, scale, alpha, ref genderMask);

        var regionTop = genderTop + genderSectionHeight;
        DrawRegionSection(drawList, min, max, regionTop, scale, alpha, ref region);

        var footerTop = regionTop + RegionHeaderHeight * scale + RegionRowHeight * scale;
        drawList.AddLine(new Vector2(min.X + 12f * scale, footerTop), new Vector2(max.X - 12f * scale, footerTop),
            VelvetTheme.Alpha(VelvetTheme.Divider, alpha).Packed(), 1f);
        var footerCenterY = footerTop + footerHeight * 0.5f;

        var clearLabel = Loc.T(L.Velvet.FilterClearAll);
        var clearSize = Typography.Measure(clearLabel, 0.9f, FontWeight.Medium);
        var clearMin = new Vector2(min.X + 12f * scale, footerCenterY - 16f * scale);
        var clearMax = new Vector2(clearMin.X + clearSize.X + 16f * scale, footerCenterY + 16f * scale);
        var clearEnabled = VelvetIntent.Sanitize(mask) != 0 || VelvetGender.Sanitize(genderMask) != 0
            || region.Length > 0;
        var clearHovered = clearEnabled && ImGui.IsMouseHoveringRect(clearMin, clearMax);
        var clearInk = !clearEnabled ? VelvetTheme.Faint : clearHovered ? VelvetTheme.TitleInk : VelvetTheme.MutedInk;
        Typography.Draw(drawList, new Vector2(clearMin.X + 8f * scale, footerCenterY - clearSize.Y * 0.5f), clearLabel,
            VelvetTheme.Alpha(clearInk, alpha), 0.9f, FontWeight.Medium);
        if (clearHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                mask = VelvetIntent.Any;
                genderMask = VelvetGender.None;
                region = string.Empty;
            }
        }

        var doneWidth = 82f * scale;
        var doneHeight = 34f * scale;
        var doneMax = new Vector2(max.X - 14f * scale, footerCenterY + doneHeight * 0.5f);
        var doneMin = new Vector2(doneMax.X - doneWidth, footerCenterY - doneHeight * 0.5f);
        var doneHovered = ImGui.IsMouseHoveringRect(doneMin, doneMax);
        var doneFill = doneHovered ? VelvetTheme.Lerp(VelvetTheme.Rose, VelvetTheme.OnAccent, 0.12f) : VelvetTheme.Rose;
        Squircle.Fill(drawList, doneMin, doneMax, doneHeight * 0.5f, VelvetTheme.Alpha(doneFill, alpha).Packed());
        var doneLabel = Loc.T(L.Velvet.FilterDone);
        var doneSize = Typography.Measure(doneLabel, 0.9f, FontWeight.SemiBold);
        Typography.Draw(drawList,
            new Vector2((doneMin.X + doneMax.X) * 0.5f - doneSize.X * 0.5f, footerCenterY - doneSize.Y * 0.5f),
            doneLabel, VelvetTheme.Alpha(VelvetTheme.OnAccent, alpha), 0.9f, FontWeight.SemiBold);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                Close();
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max, false) &&
            ImGui.GetFrameCount() != openedFrame)
        {
            Close();
        }

        return new VFilterSelection(mask, genderMask, region);
    }

    private static void DrawGenderSection(ImDrawListPtr drawList, Vector2 min, Vector2 max, float top, float scale,
        float alpha, ref int genderMask)
    {
        var headerHeight = GenderHeaderHeight * scale;
        var headerCenterY = top + headerHeight * 0.5f;
        var headerLabel = Loc.T(L.Velvet.CardGender);
        var headerSize = Typography.Measure(headerLabel, 0.82f, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, headerCenterY - headerSize.Y * 0.5f), headerLabel,
            VelvetTheme.Alpha(VelvetTheme.HeaderInk, alpha), 0.82f, FontWeight.SemiBold);

        var options = VelvetGender.All;
        var gridTop = top + headerHeight;
        for (var index = 0; index < options.Length; index++)
        {
            var flag = options[index];
            var selected = VelvetGender.Has(genderMask, flag);
            if (DrawPill(drawList, min, max, gridTop, index, VelvetGender.Label(flag), VelvetTheme.Rose, selected,
                    alpha, scale))
            {
                genderMask = VelvetGender.Toggle(genderMask, flag);
            }
        }
    }

    private static bool DrawPill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float gridTop, int index,
        string label, Vector4 accent, bool selected, float alpha, float scale)
    {
        var gridLeft = min.X + 12f * scale;
        var gridRight = max.X - 12f * scale;
        var rowHeight = PillRowHeight * scale;
        var colGap = 8f * scale;
        var cellWidth = (gridRight - gridLeft - colGap * (PillColumns - 1)) / PillColumns;
        var pillHeight = 28f * scale;
        var column = index % PillColumns;
        var row = index / PillColumns;
        var cellLeft = gridLeft + column * (cellWidth + colGap);
        var cellTop = gridTop + row * rowHeight;
        var pillMin = new Vector2(cellLeft, cellTop + (rowHeight - pillHeight) * 0.5f);
        var pillMax = new Vector2(cellLeft + cellWidth, pillMin.Y + pillHeight);
        var hovered = ImGui.IsMouseHoveringRect(pillMin, pillMax);

        Vector4 fill;
        if (selected)
        {
            fill = VelvetTheme.Alpha(accent, 0.92f * alpha);
        }
        else if (hovered)
        {
            fill = VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.10f * alpha);
        }
        else
        {
            fill = VelvetTheme.Alpha(VelvetTheme.Sunken, alpha);
        }

        Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f, fill.Packed());
        if (!selected)
        {
            Squircle.Stroke(drawList, pillMin, pillMax, pillHeight * 0.5f,
                VelvetTheme.Alpha(VelvetTheme.CardStroke, alpha).Packed(), 1f * scale);
        }

        var ink = selected ? VelvetTheme.OnAccent : hovered ? VelvetTheme.TitleInk : VelvetTheme.MutedInk;
        var fitted = Typography.FitText(label, cellWidth - 12f * scale, 0.82f, FontWeight.SemiBold);
        var labelSize = Typography.Measure(fitted, 0.82f, FontWeight.SemiBold);
        Typography.Draw(drawList,
            new Vector2((pillMin.X + pillMax.X) * 0.5f - labelSize.X * 0.5f,
                (pillMin.Y + pillMax.Y) * 0.5f - labelSize.Y * 0.5f),
            fitted, VelvetTheme.Alpha(ink, alpha), 0.82f, FontWeight.SemiBold);

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawRegionSection(ImDrawListPtr drawList, Vector2 min, Vector2 max, float top, float scale,
        float alpha, ref string region)
    {
        var headerHeight = RegionHeaderHeight * scale;
        var rowHeight = RegionRowHeight * scale;
        var headerCenterY = top + headerHeight * 0.5f;
        var headerLabel = Loc.T(L.Velvet.RegionLabel);
        var headerSize = Typography.Measure(headerLabel, 0.82f, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, headerCenterY - headerSize.Y * 0.5f), headerLabel,
            VelvetTheme.Alpha(VelvetTheme.HeaderInk, alpha), 0.82f, FontWeight.SemiBold);

        var codes = SocialRegion.Codes;
        var segmentCount = codes.Length + 1;
        var barHeight = 32f * scale;
        var segMin = new Vector2(min.X + 12f * scale, top + headerHeight + (rowHeight - barHeight) * 0.5f);
        var segMax = new Vector2(max.X - 12f * scale, segMin.Y + barHeight);
        Squircle.Fill(drawList, segMin, segMax, 9f * scale, VelvetTheme.Alpha(VelvetTheme.Sunken, alpha).Packed());
        var segmentWidth = (segMax.X - segMin.X) / segmentCount;

        for (var index = 1; index < segmentCount; index++)
        {
            var dividerX = segMin.X + index * segmentWidth;
            drawList.AddLine(new Vector2(dividerX, segMin.Y + 7f * scale), new Vector2(dividerX, segMax.Y - 7f * scale),
                VelvetTheme.Alpha(VelvetTheme.Divider, alpha).Packed(), 1f);
        }

        for (var index = 0; index < segmentCount; index++)
        {
            var code = index == 0 ? string.Empty : codes[index - 1];
            var label = index == 0 ? Loc.T(L.Velvet.RegionAny) : code;
            var segLeft = segMin.X + index * segmentWidth;
            var segRight = index == segmentCount - 1 ? segMax.X : segLeft + segmentWidth;
            var selected = string.Equals(region, code, StringComparison.Ordinal);
            var hovered = ImGui.IsMouseHoveringRect(new Vector2(segLeft, segMin.Y), new Vector2(segRight, segMax.Y));
            if (selected)
            {
                Squircle.Fill(drawList, new Vector2(segLeft + 3f * scale, segMin.Y + 3f * scale),
                    new Vector2(segRight - 3f * scale, segMax.Y - 3f * scale), 7f * scale,
                    VelvetTheme.Alpha(VelvetTheme.RegionAccent, 0.92f * alpha).Packed());
            }

            var ink = selected ? VelvetTheme.OnAccent : hovered ? VelvetTheme.TitleInk : VelvetTheme.MutedInk;
            var labelSize = Typography.Measure(label, 0.82f, FontWeight.SemiBold);
            Typography.Draw(drawList,
                new Vector2((segLeft + segRight) * 0.5f - labelSize.X * 0.5f, (segMin.Y + segMax.Y) * 0.5f - labelSize.Y * 0.5f),
                label, VelvetTheme.Alpha(ink, alpha), 0.82f, FontWeight.SemiBold);

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    region = code;
                }
            }
        }
    }

}

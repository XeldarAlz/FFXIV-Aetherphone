using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet.Kit;

internal sealed class VFilterSheet
{
    private const float RevealSeconds = 0.16f;
    private const float PanelWidth = 250f;
    private const float HeaderHeight = 34f;
    private const float RowHeight = 42f;
    private const float FooterHeight = 54f;

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

    public int Draw(Rect screen, float scale, int mask)
    {
        if (!open)
        {
            return mask;
        }

        var defs = VelvetIntent.All;
        var drawList = ImGui.GetForegroundDrawList();
        var reveal = Easing.EaseOutQuint(Math.Clamp((float)((ImGui.GetTime() - openedAt) / RevealSeconds), 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.7f, 0f, 1f));

        var width = MathF.Min(PanelWidth * scale, screen.Width - 24f * scale);
        var headerHeight = HeaderHeight * scale;
        var rowHeight = RowHeight * scale;
        var footerHeight = FooterHeight * scale;
        var padY = 8f * scale;
        var radius = 16f * scale;
        var height = padY + headerHeight + defs.Length * rowHeight + footerHeight + padY;

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
            var rowMin = new Vector2(min.X + 6f * scale, rowsTop + index * rowHeight);
            var rowMax = new Vector2(max.X - 6f * scale, rowMin.Y + rowHeight);
            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
            var selected = VelvetIntent.Has(mask, def.Flag);
            var hovered = ImGui.IsMouseHoveringRect(rowMin, rowMax);
            if (selected)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 10f * scale, VelvetTheme.Alpha(def.Hue, 0.16f * alpha).Packed());
            }
            else if (hovered)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 10f * scale,
                    VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.06f * alpha).Packed());
            }

            var iconCenter = new Vector2(rowMin.X + 20f * scale, centerY);
            AppSkin.Icon(drawList, iconCenter, def.Icon.ToIconString(), VelvetTheme.Alpha(def.Hue, alpha), 0.9f);

            var labelInk = selected ? VelvetTheme.TitleInk : VelvetTheme.BodyInk;
            var label = Loc.T(def.Label);
            var labelSize = Typography.Measure(label, 0.95f, FontWeight.Medium);
            Typography.Draw(drawList, new Vector2(iconCenter.X + 22f * scale, centerY - labelSize.Y * 0.5f), label,
                VelvetTheme.Alpha(labelInk, alpha), 0.95f, FontWeight.Medium);

            DrawCheckbox(drawList, new Vector2(rowMax.X - 22f * scale, centerY), selected, def.Hue, alpha, scale);

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    mask = VelvetIntent.Toggle(mask, def.Flag);
                }
            }
        }

        var footerTop = rowsTop + defs.Length * rowHeight;
        drawList.AddLine(new Vector2(min.X + 12f * scale, footerTop), new Vector2(max.X - 12f * scale, footerTop),
            VelvetTheme.Alpha(VelvetTheme.Divider, alpha).Packed(), 1f);
        var footerCenterY = footerTop + footerHeight * 0.5f;

        var clearLabel = Loc.T(L.Velvet.FilterClearAll);
        var clearSize = Typography.Measure(clearLabel, 0.9f, FontWeight.Medium);
        var clearMin = new Vector2(min.X + 12f * scale, footerCenterY - 16f * scale);
        var clearMax = new Vector2(clearMin.X + clearSize.X + 16f * scale, footerCenterY + 16f * scale);
        var clearEnabled = VelvetIntent.Sanitize(mask) != 0;
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

        return mask;
    }

    private static void DrawCheckbox(ImDrawListPtr drawList, Vector2 center, bool selected, Vector4 hue, float alpha,
        float scale)
    {
        var half = 10f * scale;
        var boxMin = new Vector2(center.X - half, center.Y - half);
        var boxMax = new Vector2(center.X + half, center.Y + half);
        if (!selected)
        {
            Squircle.Stroke(drawList, boxMin, boxMax, 6f * scale,
                VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.30f * alpha).Packed(), 1.4f * scale);
            return;
        }

        Squircle.Fill(drawList, boxMin, boxMax, 6f * scale, VelvetTheme.Alpha(hue, alpha).Packed());
        var tick = VelvetTheme.Alpha(VelvetTheme.OnAccent, alpha).Packed();
        var thickness = 1.9f * scale;
        drawList.AddLine(center + new Vector2(-4f * scale, 0f), center + new Vector2(-1.2f * scale, 3.4f * scale), tick,
            thickness);
        drawList.AddLine(center + new Vector2(-1.2f * scale, 3.4f * scale), center + new Vector2(4.6f * scale, -3.8f * scale),
            tick, thickness);
    }
}

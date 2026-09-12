using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal readonly struct PhotoEditPanelStyle
{
    private static readonly Vector4 DarkFill = new(0.07f, 0.07f, 0.09f, 0.96f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteMuted = new(1f, 1f, 1f, 0.68f);
    private static readonly Vector4 WhiteRail = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 WhiteWell = new(1f, 1f, 1f, 0.10f);

    public readonly Vector4 PanelFill;
    public readonly Vector4 Ink;
    public readonly Vector4 MutedInk;
    public readonly Vector4 Rail;
    public readonly Vector4 Accent;
    public readonly Vector4 IconBackground;
    public readonly bool Overlay;

    public PhotoEditPanelStyle(Vector4 panelFill, Vector4 ink, Vector4 mutedInk, Vector4 rail, Vector4 accent,
        Vector4 iconBackground, bool overlay)
    {
        PanelFill = panelFill;
        Ink = ink;
        MutedInk = mutedInk;
        Rail = rail;
        Accent = accent;
        IconBackground = iconBackground;
        Overlay = overlay;
    }

    public static PhotoEditPanelStyle Dark(Vector4 accent)
    {
        return new PhotoEditPanelStyle(DarkFill, White, WhiteMuted, WhiteRail, accent, WhiteWell, true);
    }

    public static PhotoEditPanelStyle ForComposer(in PhotoComposeStyle compose, Vector4 ink, Vector4 iconBackground)
    {
        return new PhotoEditPanelStyle(AppSkin.Transparent, ink, compose.MutedInk, compose.ScrubberTrack,
            compose.Accent, iconBackground, false);
    }
}

internal static class PhotoEditPanel
{
    public const float Height = 172f;
    private const float PanelRounding = 22f;
    private const float StageRounding = 14f;
    private const float StageInset = 12f;
    private const float SideInset = 18f;
    private const float LoadingRadius = 13f;
    private const float HairlineAlpha = 0.10f;

    private const float DockHeight = 44f;
    private const float DockBottomInset = 8f;
    private const float DockItemWidth = 76f;
    private const float DockPillInset = 4f;
    private const float DockIconOffset = -7f;
    private const float DockLabelOffset = 12f;
    private const float DockIconScale = 0.9f;
    private const float DockSmoothTime = 0.16f;
    private const float DockPillAlpha = 0.22f;
    private const float DockPillStrokeAlpha = 0.35f;

    private const float RulerHeight = 28f;
    private const float RulerBottomInset = 62f;
    private const float RulerWidthFraction = 0.68f;
    private const int RulerTickCount = 21;
    private const int RulerMajorEvery = 5;
    private const float RulerMinorTick = 6f;
    private const float RulerMajorTick = 10f;
    private const float RulerCenterTick = 13f;
    private const float RulerBaselineInset = 4f;
    private const float RulerKnobWidth = 3f;
    private const float RulerKnobHeight = 22f;
    private const float RulerHitPad = 14f;
    private const float RulerWheelStep = 0.02f;
    private const float RulerTickAlpha = 0.55f;

    private const float LabelCenterFromBottom = 100f;
    private const float LabelGap = 8f;
    private const float UpperRowCenterFromBottom = 139f;

    private const float DialRadius = 18f;
    private const float DialSpacing = 50f;
    private const float RingRadius = 21.5f;
    private const float RingThickness = 2.2f;
    private const int RingSegments = 32;
    private const float DialIconScale = 0.8f;
    private const float RingEpsilon = 0.002f;

    private const float TileSide = 48f;
    private const float TileGap = 8f;
    private const float TileRounding = 12f;
    private const float TileStroke = 2f;
    private const float ShelfDragSlop = 5f;
    private const float ShelfWheelStep = 40f;

    private const float OrientationRadius = 17f;
    private const float OrientationGap = 6f;
    private const float OrientationIconScale = 0.8f;
    private const float DimmedAlpha = 0.35f;
    private const float MaxDeltaSeconds = 0.1f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 KnobShadow = new(0f, 0f, 0f, 0.28f);
    private static readonly PhotoEditTool[] Tools = { PhotoEditTool.Adjust, PhotoEditTool.Looks, PhotoEditTool.Crop };

    public static Rect FooterRect(Rect area, float bottom, float scale)
    {
        return new Rect(new Vector2(area.Min.X, bottom - (Height * scale)), new Vector2(area.Max.X, bottom));
    }

    public static float UpperRowCenterY(Rect footer, float scale)
    {
        return footer.Max.Y - (UpperRowCenterFromBottom * scale);
    }

    public static Rect RulerRect(Rect footer, float scale)
    {
        var width = footer.Width * RulerWidthFraction;
        var bottom = footer.Max.Y - (RulerBottomInset * scale);
        return new Rect(new Vector2(footer.Center.X - (width * 0.5f), bottom - (RulerHeight * scale)),
            new Vector2(footer.Center.X + (width * 0.5f), bottom));
    }

    private static Rect DockRect(Rect footer, float scale)
    {
        var width = DockItemWidth * Tools.Length * scale;
        var bottom = footer.Max.Y - (DockBottomInset * scale);
        return new Rect(new Vector2(footer.Center.X - (width * 0.5f), bottom - (DockHeight * scale)),
            new Vector2(footer.Center.X + (width * 0.5f), bottom));
    }

    public static void DrawStage(PhotoEditSession session, Rect stage, in PhotoEditPanelStyle style, float scale,
        double now)
    {
        var drawList = ImGui.GetWindowDrawList();
        var frame = stage.Inset(StageInset * scale);
        if (session.Preview.Failed)
        {
            Typography.DrawCentered(drawList, frame.Center, Loc.T(L.Photos.EditOpenFailed), style.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        var texture = session.Preview.Texture(session.Edit, now);
        if (texture is null)
        {
            LoadingPulse.Draw(frame.Center, LoadingRadius * scale, style.Accent, style.MutedInk,
                Loc.T(L.Common.Loading));
            return;
        }

        session.StageTextureSize = texture.Size;
        var interactive = session.Controls.Tool == PhotoEditTool.Crop && !session.Saving;
        session.Crop.Draw(frame, texture, session.CropRatio, false, StageRounding * scale, interactive);
    }

    public static void DrawTools(PhotoEditSession session, Rect panel, float contentBottom, AppSkin ui,
        in PhotoEditPanelStyle style, float scale)
    {
        PaintPanel(panel, style, scale);
        var footer = FooterRect(panel, contentBottom, scale);
        var interactive = !session.Saving;
        switch (session.Controls.Tool)
        {
            case PhotoEditTool.Looks:
                DrawLooks(session.Controls, session.Preview, footer, style, scale, interactive);
                break;
            case PhotoEditTool.Crop:
                DrawGalleryCrop(session, footer, ui, style, scale, interactive);
                break;
            default:
                DrawAdjust(session.Controls, footer, style, scale, interactive);
                break;
        }

        DrawDock(session.Controls, footer, style, scale, interactive);
    }

    private static void PaintPanel(Rect panel, in PhotoEditPanelStyle style, float scale)
    {
        if (style.PanelFill.W <= 0f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, panel.Min, panel.Max, PanelRounding * scale, ImGui.GetColorU32(style.PanelFill));
        drawList.AddLine(new Vector2(panel.Min.X + (PanelRounding * scale), panel.Min.Y),
            new Vector2(panel.Max.X - (PanelRounding * scale), panel.Min.Y),
            ImGui.GetColorU32(style.Ink with { W = HairlineAlpha }), Metrics.Stroke.Hairline * scale);
    }

    private static void DrawGalleryCrop(PhotoEditSession session, Rect footer, AppSkin ui,
        in PhotoEditPanelStyle style, float scale, bool interactive)
    {
        var aspects = PhotoCropAspects.All;
        for (var index = 0; index < aspects.Length; index++)
        {
            session.AspectLabels[index] = Loc.T(AspectLabel(aspects[index]));
            session.AspectActive[index] = aspects[index] == session.Aspect;
        }

        var rowCenterY = UpperRowCenterY(footer, scale);
        var row = new Rect(new Vector2(footer.Min.X + (SideInset * scale), rowCenterY - (ChipRail.RowHeight * 0.5f * scale)),
            new Vector2(footer.Max.X - (SideInset * scale), rowCenterY + (ChipRail.RowHeight * 0.5f * scale)));
        var picked = session.AspectRail.Draw(row, ui, session.AspectLabels, session.AspectActive, style.Overlay,
            labelPadding: ChipRail.CompactLabelPadding, centered: true, interactive: interactive);
        if (picked >= 0)
        {
            session.Aspect = aspects[picked];
        }

        var size = session.StageTextureSize;
        var ruler = RulerRect(footer, scale);
        DrawOrientationButtons(ruler, session.Controls, style, scale, interactive);
        if (size.X <= 0f)
        {
            return;
        }

        var ratio = session.CropRatio;
        var fraction = session.Crop.ZoomFraction(size, ratio, false);
        var updated = DrawRuler(ruler, fraction, false, style, scale, 1f, interactive);
        if (updated != fraction)
        {
            session.Crop.SetZoomFraction(updated, size, ratio, false);
        }
    }

    public static void DrawAdjust(PhotoEditControls controls, Rect footer, in PhotoEditPanelStyle style,
        float scale, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var adjustments = PhotoEditControls.Adjustments;
        var centerY = UpperRowCenterY(footer, scale);
        var spacing = DialSpacing * scale;
        var startX = footer.Center.X - ((adjustments.Length - 1) * spacing * 0.5f);
        var radius = DialRadius * scale;
        var hit = new Vector2(RingRadius * scale, RingRadius * scale);
        for (var index = 0; index < adjustments.Length; index++)
        {
            var adjustment = adjustments[index];
            var center = new Vector2(startX + (index * spacing), centerY);
            var selected = adjustment == controls.Adjustment;
            var hovered = interactive && UiInteract.Hover(center - hit, center + hit);
            var face = selected
                ? style.Accent
                : hovered
                    ? Core.Theme.Palette.Mix(style.IconBackground, style.Ink, 0.08f)
                    : style.IconBackground;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(face), 32);
            if (!selected)
            {
                drawList.AddCircle(center, radius, ImGui.GetColorU32(style.Ink with { W = HairlineAlpha }), 32,
                    Metrics.Stroke.Hairline * scale);
            }

            DrawValueRing(drawList, center, controls.Edit.ValueOf(adjustment), adjustment, style, scale, selected);
            AppSkin.Icon(drawList, center, IconGlyph.Of(AdjustmentIcon(adjustment)),
                selected ? White : hovered ? style.Ink : style.MutedInk, DialIconScale);
            HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(AdjustmentLabel(adjustment)));
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(center - hit, center + hit, hovered))
            {
                controls.Adjustment = adjustment;
            }
        }

        var (min, max) = PhotoEdit.RangeOf(controls.Adjustment);
        var value = controls.Edit.ValueOf(controls.Adjustment);
        DrawLabelLine(footer, Loc.T(AdjustmentLabel(controls.Adjustment)),
            controls.ValueLabel(controls.Adjustment, value, Loc.Culture), style, scale);
        var ruler = RulerRect(footer, scale);
        var fraction = (value - min) / (max - min);
        var updated = DrawRuler(ruler, fraction, min < 0f, style, scale, 1f, interactive);
        if (updated != fraction)
        {
            controls.Adjust(controls.Adjustment, min + (updated * (max - min)));
        }
    }

    private static void DrawValueRing(ImDrawListPtr drawList, Vector2 center, float value,
        PhotoAdjustment adjustment, in PhotoEditPanelStyle style, float scale, bool selected)
    {
        var (min, max) = PhotoEdit.RangeOf(adjustment);
        var bipolar = min < 0f;
        var fraction = bipolar ? value / max : (value - min) / (max - min);
        if (MathF.Abs(fraction) < RingEpsilon)
        {
            return;
        }

        const float top = -MathF.PI * 0.5f;
        float start;
        float end;
        if (bipolar)
        {
            var sweep = MathF.Abs(fraction) * MathF.PI;
            start = fraction > 0f ? top : top - sweep;
            end = fraction > 0f ? top + sweep : top;
        }
        else
        {
            start = top;
            end = top + (fraction * MathF.PI * 2f);
        }

        drawList.PathArcTo(center, RingRadius * scale, start, end, RingSegments);
        drawList.PathStroke(ImGui.GetColorU32(selected ? style.Accent : style.Ink with { W = 0.55f }),
            ImDrawFlags.None, RingThickness * scale);
    }

    public static void DrawLooks(PhotoEditControls controls, PhotoEditPreview? preview, Rect footer,
        in PhotoEditPanelStyle style, float scale, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var looks = PhotoLooks.All;
        var side = TileSide * scale;
        var gap = TileGap * scale;
        var centerY = UpperRowCenterY(footer, scale);
        var row = new Rect(new Vector2(footer.Min.X + (SideInset * scale), centerY - (side * 0.5f)),
            new Vector2(footer.Max.X - (SideInset * scale), centerY + (side * 0.5f)));
        var contentWidth = (looks.Length * side) + ((looks.Length - 1) * gap);
        var maxOffset = MathF.Max(0f, contentWidth - row.Width);
        HandleShelfDrag(controls, row, maxOffset, interactive);
        var startX = maxOffset > 0f ? row.Min.X - controls.LookShelfOffset : row.Center.X - (contentWidth * 0.5f);
        var rowHovered = interactive && UiInteract.Hover(row.Min, row.Max);
        var released = ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        var tapped = released && controls.LookShelfTravel < ShelfDragSlop * scale;
        drawList.PushClipRect(row.Min, row.Max, true);
        for (var index = 0; index < looks.Length; index++)
        {
            var min = new Vector2(startX + (index * (side + gap)), row.Min.Y);
            var max = new Vector2(min.X + side, row.Max.Y);
            var selected = looks[index] == controls.Edit.Look;
            var hovered = rowHovered && ImGui.IsMouseHoveringRect(min, max);
            var texture = preview?.LookTexture(index);
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, TileRounding * scale, ImGui.GetColorU32(style.IconBackground));
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, TileRounding * scale,
                    ImDrawFlags.RoundCornersAll);
            }

            if (selected)
            {
                Squircle.Stroke(drawList, min, max, TileRounding * scale, ImGui.GetColorU32(style.Accent),
                    TileStroke * scale);
            }
            else
            {
                Squircle.Stroke(drawList, min, max, TileRounding * scale,
                    ImGui.GetColorU32(style.Ink with { W = hovered ? 0.35f : HairlineAlpha }),
                    Metrics.Stroke.Hairline * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                HoverTooltip.Show(new Rect(min, max), Loc.T(LookLabel(looks[index])));
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    controls.LookShelfPressedIndex = index;
                }

                if (tapped && controls.LookShelfPressedIndex == index)
                {
                    controls.SetLook(looks[index]);
                }
            }
        }

        drawList.PopClipRect();
        if (released)
        {
            controls.LookShelfPressedIndex = -1;
        }

        var hasLook = controls.Edit.Look != PhotoLook.None;
        var strength = controls.Edit.LookStrength;
        DrawLabelLine(footer, Loc.T(LookLabel(controls.Edit.Look)),
            hasLook ? controls.ValueLabel(PhotoAdjustment.Vignette, strength, Loc.Culture) : string.Empty, style,
            scale);
        var ruler = RulerRect(footer, scale);
        var updated = DrawRuler(ruler, strength, false, style, scale, hasLook ? 1f : DimmedAlpha,
            interactive && hasLook);
        if (hasLook && updated != strength)
        {
            controls.SetLookStrength(updated);
        }
    }

    private static void HandleShelfDrag(PhotoEditControls controls, Rect row, float maxOffset, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                controls.LookShelfOffset -= wheel * ShelfWheelStep * scale;
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                controls.LookShelfDragging = true;
                controls.LookShelfLastMouseX = ImGui.GetMousePos().X;
                controls.LookShelfTravel = 0f;
            }
        }

        if (controls.LookShelfDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouseX = ImGui.GetMousePos().X;
                var delta = mouseX - controls.LookShelfLastMouseX;
                controls.LookShelfLastMouseX = mouseX;
                controls.LookShelfTravel += MathF.Abs(delta);
                controls.LookShelfOffset -= delta;
            }
            else
            {
                controls.LookShelfDragging = false;
            }
        }

        controls.LookShelfOffset = Math.Clamp(controls.LookShelfOffset, 0f, maxOffset);
    }

    public static void DrawLabelLine(Rect footer, string name, string value, in PhotoEditPanelStyle style,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = footer.Max.Y - (LabelCenterFromBottom * scale);
        var nameSize = Typography.Measure(name, TextStyles.SubheadlineEmphasized);
        if (value.Length == 0)
        {
            Typography.DrawCentered(drawList, new Vector2(footer.Center.X, centerY), name, style.MutedInk,
                TextStyles.SubheadlineEmphasized);
            return;
        }

        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        var gap = LabelGap * scale;
        var total = nameSize.X + gap + valueSize.X;
        var left = footer.Center.X - (total * 0.5f);
        Typography.Draw(drawList, new Vector2(left, centerY - (nameSize.Y * 0.5f)), name, style.Ink,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left + nameSize.X + gap, centerY - (valueSize.Y * 0.5f)), value,
            style.Accent, TextStyles.SubheadlineEmphasized);
    }

    public static float DrawRuler(Rect rect, float value, bool bipolar, in PhotoEditPanelStyle style, float scale,
        float alpha, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var result = Math.Clamp(value, 0f, 1f);
        var pad = new Vector2(0f, RulerHitPad * scale);
        var hovered = interactive && UiInteract.Hover(rect.Min - pad, rect.Max + pad);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                result = Math.Clamp(result + (wheel * RulerWheelStep), 0f, 1f);
            }

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && rect.Width > 0f)
            {
                result = Math.Clamp((ImGui.GetMousePos().X - rect.Min.X) / rect.Width, 0f, 1f);
            }
        }

        var baselineY = rect.Max.Y - (RulerBaselineInset * scale);
        var tickColor = ImGui.GetColorU32(style.MutedInk with { W = style.MutedInk.W * RulerTickAlpha * alpha });
        var centerColor = ImGui.GetColorU32(style.Ink with { W = style.Ink.W * alpha });
        var thickness = Metrics.Stroke.Hairline * scale;
        for (var index = 0; index < RulerTickCount; index++)
        {
            var x = rect.Min.X + (rect.Width * index / (RulerTickCount - 1));
            var isCenter = bipolar && index == (RulerTickCount - 1) / 2;
            var major = index % RulerMajorEvery == 0;
            var height = (isCenter ? RulerCenterTick : major ? RulerMajorTick : RulerMinorTick) * scale;
            drawList.AddLine(new Vector2(x, baselineY - height), new Vector2(x, baselineY),
                isCenter ? centerColor : tickColor, isCenter ? Metrics.Stroke.Thin * scale : thickness);
        }

        var knobX = rect.Min.X + (rect.Width * result);
        var fillStart = bipolar ? rect.Center.X : rect.Min.X;
        drawList.AddLine(new Vector2(MathF.Min(fillStart, knobX), baselineY),
            new Vector2(MathF.Max(fillStart, knobX), baselineY),
            ImGui.GetColorU32(style.Accent with { W = style.Accent.W * alpha }), Metrics.Stroke.Ring * scale);
        var knobHalf = new Vector2(RulerKnobWidth * 0.5f * scale, RulerKnobHeight * 0.5f * scale);
        var knobCenter = new Vector2(knobX, rect.Center.Y);
        var shadowOffset = new Vector2(0f, 1f * scale);
        drawList.AddRectFilled(knobCenter - knobHalf + shadowOffset, knobCenter + knobHalf + shadowOffset,
            ImGui.GetColorU32(KnobShadow with { W = KnobShadow.W * alpha }), knobHalf.X);
        drawList.AddRectFilled(knobCenter - knobHalf, knobCenter + knobHalf,
            ImGui.GetColorU32(style.Accent with { W = style.Accent.W * alpha }), knobHalf.X);
        return result;
    }

    public static void DrawOrientationButtons(Rect ruler, PhotoEditControls controls, in PhotoEditPanelStyle style,
        float scale, bool interactive)
    {
        var radius = OrientationRadius * scale;
        var rotateCenter = new Vector2(ruler.Min.X - (OrientationGap * scale) - radius, ruler.Center.Y);
        var flipCenter = new Vector2(ruler.Max.X + (OrientationGap * scale) + radius, ruler.Center.Y);
        if (CircleIconButton(rotateCenter, radius, FontAwesomeIcon.Redo, Loc.T(L.Photos.Rotate), style, scale,
                interactive))
        {
            controls.Rotate();
        }

        if (CircleIconButton(flipCenter, radius, FontAwesomeIcon.ArrowsAltH, Loc.T(L.Photos.Flip), style, scale,
                interactive))
        {
            controls.Flip();
        }
    }

    private static bool CircleIconButton(Vector2 center, float radius, FontAwesomeIcon icon, string tooltip,
        in PhotoEditPanelStyle style, float scale, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - hit, center + hit);
        var face = hovered ? Core.Theme.Palette.Mix(style.IconBackground, style.Ink, 0.08f) : style.IconBackground;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(face), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(style.Ink with { W = HairlineAlpha }), 32,
            Metrics.Stroke.Hairline * scale);
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), hovered ? style.Ink : style.MutedInk,
            OrientationIconScale);
        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static void DrawDock(PhotoEditControls controls, Rect footer, in PhotoEditPanelStyle style, float scale,
        bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var dock = DockRect(footer, scale);
        var rounding = dock.Height * 0.5f;
        Material.Glass(drawList, dock.Min, dock.Max, rounding, style.Ink, scale);
        var itemWidth = DockItemWidth * scale;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, MaxDeltaSeconds);
        var activeIndex = Array.IndexOf(Tools, controls.Tool);
        var animated = controls.DockSpring.Step(activeIndex, DockSmoothTime, deltaSeconds);
        var pillInset = DockPillInset * scale;
        var pillMin = new Vector2(dock.Min.X + pillInset + (animated * itemWidth), dock.Min.Y + pillInset);
        var pillMax = new Vector2(pillMin.X + itemWidth - (pillInset * 2f), dock.Max.Y - pillInset);
        var pillRounding = (pillMax.Y - pillMin.Y) * 0.5f;
        Squircle.Fill(drawList, pillMin, pillMax, pillRounding,
            ImGui.GetColorU32(style.Accent with { W = DockPillAlpha }));
        Squircle.Stroke(drawList, pillMin, pillMax, pillRounding,
            ImGui.GetColorU32(style.Accent with { W = DockPillStrokeAlpha }), Metrics.Stroke.Hairline * scale);
        for (var index = 0; index < Tools.Length; index++)
        {
            var tool = Tools[index];
            var min = new Vector2(dock.Min.X + (index * itemWidth), dock.Min.Y);
            var max = new Vector2(min.X + itemWidth, dock.Max.Y);
            var center = (min + max) * 0.5f;
            var active = tool == controls.Tool;
            var hovered = interactive && UiInteract.Hover(min, max);
            var color = active ? style.Accent : hovered ? style.Ink : style.MutedInk;
            AppSkin.Icon(drawList, new Vector2(center.X, center.Y + (DockIconOffset * scale)),
                IconGlyph.Of(ToolIcon(tool)), color, DockIconScale);
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + (DockLabelOffset * scale)),
                Loc.T(ToolLabel(tool)), color, TextStyles.Caption2);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                controls.Tool = tool;
            }
        }
    }

    private static FontAwesomeIcon AdjustmentIcon(PhotoAdjustment adjustment)
    {
        switch (adjustment)
        {
            case PhotoAdjustment.Contrast:
                return FontAwesomeIcon.Adjust;
            case PhotoAdjustment.Saturation:
                return FontAwesomeIcon.Tint;
            case PhotoAdjustment.Warmth:
                return FontAwesomeIcon.ThermometerHalf;
            case PhotoAdjustment.Vignette:
                return FontAwesomeIcon.DotCircle;
            case PhotoAdjustment.Straighten:
                return FontAwesomeIcon.RulerHorizontal;
            default:
                return FontAwesomeIcon.Sun;
        }
    }

    private static LocString AdjustmentLabel(PhotoAdjustment adjustment)
    {
        switch (adjustment)
        {
            case PhotoAdjustment.Contrast:
                return L.Photos.Contrast;
            case PhotoAdjustment.Saturation:
                return L.Photos.Saturation;
            case PhotoAdjustment.Warmth:
                return L.Photos.Warmth;
            case PhotoAdjustment.Vignette:
                return L.Photos.Vignette;
            case PhotoAdjustment.Straighten:
                return L.Photos.Straighten;
            default:
                return L.Photos.Brightness;
        }
    }

    private static LocString LookLabel(PhotoLook look)
    {
        switch (look)
        {
            case PhotoLook.Warm:
                return L.Photos.LookWarm;
            case PhotoLook.Cool:
                return L.Photos.LookCool;
            case PhotoLook.Vivid:
                return L.Photos.LookVivid;
            case PhotoLook.Film:
                return L.Photos.LookFilm;
            case PhotoLook.Fade:
                return L.Photos.LookFade;
            case PhotoLook.Mono:
                return L.Photos.LookMono;
            case PhotoLook.Noir:
                return L.Photos.LookNoir;
            default:
                return L.Photos.LookOriginal;
        }
    }

    private static LocString AspectLabel(PhotoCropAspect aspect)
    {
        switch (aspect)
        {
            case PhotoCropAspect.Square:
                return L.Photos.AspectSquare;
            case PhotoCropAspect.FourByThree:
                return L.Photos.AspectFourByThree;
            case PhotoCropAspect.ThreeByFour:
                return L.Photos.AspectThreeByFour;
            case PhotoCropAspect.SixteenByNine:
                return L.Photos.AspectSixteenByNine;
            case PhotoCropAspect.NineBySixteen:
                return L.Photos.AspectNineBySixteen;
            default:
                return L.Photos.AspectOriginal;
        }
    }

    private static LocString ToolLabel(PhotoEditTool tool)
    {
        switch (tool)
        {
            case PhotoEditTool.Looks:
                return L.Photos.ToolLooks;
            case PhotoEditTool.Crop:
                return L.Photos.ToolCrop;
            default:
                return L.Photos.ToolAdjust;
        }
    }

    private static FontAwesomeIcon ToolIcon(PhotoEditTool tool)
    {
        switch (tool)
        {
            case PhotoEditTool.Looks:
                return FontAwesomeIcon.Magic;
            case PhotoEditTool.Crop:
                return FontAwesomeIcon.Crop;
            default:
                return FontAwesomeIcon.SlidersH;
        }
    }
}

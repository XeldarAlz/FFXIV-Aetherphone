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
    private static readonly Vector4 WhiteMuted = new(1f, 1f, 1f, 0.72f);
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
    public const float Height = 168f;
    public const float ComposerHeight = 140f;
    private const float TabRowHeight = 54f;
    private const float PanelRounding = 22f;
    private const float StageRounding = 14f;
    private const float StageInset = 12f;
    private const float ChipRowTop = 12f;
    private const float ScrubberGap = 30f;
    private const float ScrubberThickness = 4f;
    private const float ValueWidth = 52f;
    private const float SideInset = 18f;
    private const float TabWidth = 84f;
    private const float TabIconOffset = 19f;
    private const float TabLabelOffset = 40f;
    private const float IconRadius = 17f;
    private const float IconGap = 42f;
    private const float IconGlyphScale = 0.85f;
    private const float TabGlyphScale = 0.95f;
    private const float DimmedAlpha = 0.35f;
    private const float KnobExtra = 4f;
    private const float CenterTickHeight = 3f;
    private const float LoadingRadius = 13f;
    private const float HairlineAlpha = 0.10f;

    private static readonly PhotoEditTool[] AllTools = { PhotoEditTool.Adjust, PhotoEditTool.Looks, PhotoEditTool.Crop };

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
        var tabs = new Rect(new Vector2(panel.Min.X, contentBottom - (TabRowHeight * scale)),
            new Vector2(panel.Max.X, contentBottom));
        var content = new Rect(panel.Min, new Vector2(panel.Max.X, tabs.Min.Y));
        var interactive = !session.Saving;
        if (session.Controls.Tool == PhotoEditTool.Crop)
        {
            DrawCrop(session, content, ui, style, scale, interactive);
        }
        else
        {
            DrawColorTools(session.Controls, content, ui, style, scale, interactive, false);
        }

        DrawTabs(session.Controls, tabs, style, scale, interactive, true);
    }

    public static void DrawComposerTools(PhotoEditControls controls, Rect panel, AppSkin ui,
        in PhotoEditPanelStyle style, float scale, bool interactive)
    {
        PaintPanel(panel, style, scale);
        var tabs = new Rect(new Vector2(panel.Min.X, panel.Max.Y - (TabRowHeight * scale)), panel.Max);
        var content = new Rect(panel.Min, new Vector2(panel.Max.X, tabs.Min.Y));
        if (controls.Tool == PhotoEditTool.Crop)
        {
            controls.Tool = PhotoEditTool.Adjust;
        }

        DrawColorTools(controls, content, ui, style, scale, interactive, true);
        DrawTabs(controls, tabs, style, scale, interactive, false);
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

    private static Rect ChipRow(Rect content, float scale)
    {
        var top = content.Min.Y + (ChipRowTop * scale);
        return new Rect(new Vector2(content.Min.X + (SideInset * scale), top),
            new Vector2(content.Max.X - (SideInset * scale), top + (ChipRail.RowHeight * scale)));
    }

    private static Rect ScrubberTrack(Rect content, float top, float scale, float leftInset, float rightInset)
    {
        var centerY = top + (ScrubberGap * scale);
        var half = ScrubberThickness * 0.5f * scale;
        return new Rect(new Vector2(content.Min.X + leftInset, centerY - half),
            new Vector2(content.Max.X - rightInset, centerY + half));
    }

    private static float DrawOrientationButtons(Rect content, float rowCenterY, AppSkin ui,
        in PhotoEditPanelStyle style, float scale, bool interactive, PhotoEditControls controls)
    {
        var rotateCenter = new Vector2(content.Min.X + ((SideInset + IconRadius) * scale), rowCenterY);
        var flipCenter = new Vector2(rotateCenter.X + (IconGap * scale), rowCenterY);
        if (ui.IconButton(rotateCenter, IconRadius * scale, IconGlyph.Of(FontAwesomeIcon.Redo), style.Ink,
                style.IconBackground, IconGlyphScale, Loc.T(L.Photos.Rotate)) && interactive)
        {
            controls.Rotate();
        }

        if (ui.IconButton(flipCenter, IconRadius * scale, IconGlyph.Of(FontAwesomeIcon.ArrowsAltH), style.Ink,
                style.IconBackground, IconGlyphScale, Loc.T(L.Photos.Flip)) && interactive)
        {
            controls.Flip();
        }

        return flipCenter.X + ((IconRadius + SideInset) * scale) - content.Min.X;
    }

    private static void DrawColorTools(PhotoEditControls controls, Rect content, AppSkin ui,
        in PhotoEditPanelStyle style, float scale, bool interactive, bool withOrientation)
    {
        if (controls.Tool == PhotoEditTool.Looks)
        {
            DrawLooks(controls, content, ui, style, scale, interactive, withOrientation);
            return;
        }

        DrawAdjust(controls, content, ui, style, scale, interactive, withOrientation);
    }

    private static void DrawAdjust(PhotoEditControls controls, Rect content, AppSkin ui, in PhotoEditPanelStyle style,
        float scale, bool interactive, bool withOrientation)
    {
        var adjustments = PhotoEditControls.Adjustments;
        for (var index = 0; index < adjustments.Length; index++)
        {
            controls.AdjustmentLabels[index] = Loc.T(AdjustmentLabel(adjustments[index]));
            controls.AdjustmentActive[index] = adjustments[index] == controls.Adjustment;
        }

        var row = ChipRow(content, scale);
        var picked = controls.AdjustmentRail.Draw(row, ui, controls.AdjustmentLabels, controls.AdjustmentActive,
            style.Overlay, centered: true, interactive: interactive);
        if (picked >= 0)
        {
            controls.Adjustment = adjustments[picked];
        }

        var rowCenterY = row.Max.Y + (ScrubberGap * scale);
        var leftInset = withOrientation
            ? DrawOrientationButtons(content, rowCenterY, ui, style, scale, interactive, controls)
            : SideInset * scale;
        var (min, max) = PhotoEdit.RangeOf(controls.Adjustment);
        var value = controls.Edit.ValueOf(controls.Adjustment);
        var track = ScrubberTrack(content, row.Max.Y, scale, leftInset, (SideInset + ValueWidth) * scale);
        var fraction = (value - min) / (max - min);
        var updated = DrawScrubber(track, fraction, min < 0f, style, scale, 1f, interactive);
        if (updated != fraction)
        {
            controls.Adjust(controls.Adjustment, min + (updated * (max - min)));
        }

        var label = controls.ValueLabel(controls.Adjustment, controls.Edit.ValueOf(controls.Adjustment), Loc.Culture);
        Typography.DrawCentered(ImGui.GetWindowDrawList(),
            new Vector2(track.Max.X + (ValueWidth * 0.5f * scale), track.Center.Y), label, style.Ink,
            TextStyles.SubheadlineEmphasized);
    }

    private static void DrawLooks(PhotoEditControls controls, Rect content, AppSkin ui, in PhotoEditPanelStyle style,
        float scale, bool interactive, bool withOrientation)
    {
        var looks = PhotoLooks.All;
        for (var index = 0; index < looks.Length; index++)
        {
            controls.LookLabels[index] = Loc.T(LookLabel(looks[index]));
            controls.LookActive[index] = looks[index] == controls.Edit.Look;
        }

        var row = ChipRow(content, scale);
        var picked = controls.LookRail.Draw(row, ui, controls.LookLabels, controls.LookActive, style.Overlay,
            centered: true, interactive: interactive);
        if (picked >= 0)
        {
            controls.SetLook(looks[picked]);
        }

        var rowCenterY = row.Max.Y + (ScrubberGap * scale);
        var leftInset = withOrientation
            ? DrawOrientationButtons(content, rowCenterY, ui, style, scale, interactive, controls)
            : SideInset * scale;
        var hasLook = controls.Edit.Look != PhotoLook.None;
        var track = ScrubberTrack(content, row.Max.Y, scale, leftInset, (SideInset + ValueWidth) * scale);
        var strength = controls.Edit.LookStrength;
        var updated = DrawScrubber(track, strength, false, style, scale, hasLook ? 1f : DimmedAlpha,
            interactive && hasLook);
        if (hasLook && updated != strength)
        {
            controls.SetLookStrength(updated);
        }

        var label = controls.ValueLabel(PhotoAdjustment.Vignette, hasLook ? strength : 0f, Loc.Culture);
        Typography.DrawCentered(ImGui.GetWindowDrawList(),
            new Vector2(track.Max.X + (ValueWidth * 0.5f * scale), track.Center.Y), label,
            hasLook ? style.Ink : style.MutedInk, TextStyles.SubheadlineEmphasized);
    }

    private static void DrawCrop(PhotoEditSession session, Rect content, AppSkin ui, in PhotoEditPanelStyle style,
        float scale, bool interactive)
    {
        var aspects = PhotoCropAspects.All;
        for (var index = 0; index < aspects.Length; index++)
        {
            session.AspectLabels[index] = Loc.T(AspectLabel(aspects[index]));
            session.AspectActive[index] = aspects[index] == session.Aspect;
        }

        var row = ChipRow(content, scale);
        var picked = session.AspectRail.Draw(row, ui, session.AspectLabels, session.AspectActive, style.Overlay,
            centered: true, interactive: interactive);
        if (picked >= 0)
        {
            session.Aspect = aspects[picked];
        }

        var rowCenterY = row.Max.Y + (ScrubberGap * scale);
        var leftInset = DrawOrientationButtons(content, rowCenterY, ui, style, scale, interactive, session.Controls);
        var size = session.StageTextureSize;
        if (size.X <= 0f)
        {
            return;
        }

        var track = ScrubberTrack(content, row.Max.Y, scale, leftInset, SideInset * scale);
        var ratio = session.CropRatio;
        var fraction = session.Crop.ZoomFraction(size, ratio, false);
        var updated = DrawScrubber(track, fraction, false, style, scale, 1f, interactive);
        if (updated != fraction)
        {
            session.Crop.SetZoomFraction(updated, size, ratio, false);
        }
    }

    private static void DrawTabs(PhotoEditControls controls, Rect tabs, in PhotoEditPanelStyle style, float scale,
        bool interactive, bool includeCrop)
    {
        var drawList = ImGui.GetWindowDrawList();
        var count = includeCrop ? AllTools.Length : AllTools.Length - 1;
        var width = TabWidth * scale;
        var left = tabs.Center.X - (width * count * 0.5f);
        for (var index = 0; index < count; index++)
        {
            var tool = AllTools[index];
            var rect = new Rect(new Vector2(left + (index * width), tabs.Min.Y),
                new Vector2(left + ((index + 1) * width), tabs.Max.Y));
            var active = tool == controls.Tool;
            var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
            var color = active ? style.Accent : hovered ? style.Ink : style.MutedInk;
            var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + (TabIconOffset * scale));
            AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(TabIcon(tool)), color, TabGlyphScale);
            Typography.DrawCentered(drawList, new Vector2(rect.Center.X, rect.Min.Y + (TabLabelOffset * scale)),
                Loc.T(TabLabel(tool)), color, TextStyles.Caption1);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                controls.Tool = tool;
            }
        }
    }

    private static float DrawScrubber(Rect track, float value, bool bipolar, in PhotoEditPanelStyle style,
        float scale, float alpha, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var midY = track.Center.Y;
        var left = track.Min.X;
        var width = track.Width;
        var thickness = track.Height;
        var result = Math.Clamp(value, 0f, 1f);
        if (interactive && Scrubber.IsHovered(track))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && width > 0f)
            {
                result = Math.Clamp((ImGui.GetMousePos().X - left) / width, 0f, 1f);
            }
        }

        var railMin = new Vector2(left, midY - (thickness * 0.5f));
        var railMax = new Vector2(track.Max.X, midY + (thickness * 0.5f));
        drawList.AddRectFilled(railMin, railMax, ImGui.GetColorU32(style.Rail with { W = style.Rail.W * alpha }),
            thickness * 0.5f);
        var knobX = left + (width * result);
        var fillStart = bipolar ? left + (width * 0.5f) : left;
        var fillMin = new Vector2(MathF.Min(fillStart, knobX), railMin.Y);
        var fillMax = new Vector2(MathF.Max(fillStart, knobX), railMax.Y);
        drawList.AddRectFilled(fillMin, fillMax,
            ImGui.GetColorU32(style.Accent with { W = style.Accent.W * alpha }), thickness * 0.5f);
        if (bipolar)
        {
            var tickHalf = thickness * CenterTickHeight * 0.5f;
            drawList.AddLine(new Vector2(fillStart, midY - tickHalf), new Vector2(fillStart, midY + tickHalf),
                ImGui.GetColorU32(style.Ink with { W = 0.5f * alpha }), Metrics.Stroke.Thin * scale);
        }

        drawList.AddCircleFilled(new Vector2(knobX, midY), (thickness * 0.5f) + (KnobExtra * scale),
            ImGui.GetColorU32(style.Ink with { W = alpha }), 24);
        return result;
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

    private static LocString TabLabel(PhotoEditTool tool)
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

    private static FontAwesomeIcon TabIcon(PhotoEditTool tool)
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

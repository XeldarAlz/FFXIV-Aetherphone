using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class PhotoEditPanel
{
    public const float Height = 168f;
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
    private const float DimmedAlpha = 0.35f;
    private const float KnobExtra = 4f;
    private const float CenterTickHeight = 3f;
    private const float LoadingRadius = 13f;

    private static readonly Vector4 PanelFill = new(0.07f, 0.07f, 0.09f, 0.96f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteMuted = new(1f, 1f, 1f, 0.72f);
    private static readonly Vector4 Rail = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 Hairline = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 IconBackground = new(1f, 1f, 1f, 0.10f);

    public static void DrawStage(PhotoEditSession session, Rect stage, AppSkin ui, float scale, double now)
    {
        var drawList = ImGui.GetWindowDrawList();
        var frame = stage.Inset(StageInset * scale);
        if (session.Preview.Failed)
        {
            Typography.DrawCentered(drawList, frame.Center, Loc.T(L.Photos.EditOpenFailed), WhiteMuted,
                TextStyles.Subheadline);
            return;
        }

        var texture = session.Preview.Texture(session.Edit, now);
        if (texture is null)
        {
            LoadingPulse.Draw(frame.Center, LoadingRadius * scale, ui.Accent, WhiteMuted, Loc.T(L.Common.Loading));
            return;
        }

        session.StageTextureSize = texture.Size;
        var interactive = session.Tool == PhotoEditTool.Crop && !session.Saving;
        session.Crop.Draw(frame, texture, session.CropRatio, false, StageRounding * scale, interactive);
    }

    public static void DrawTools(PhotoEditSession session, Rect panel, float contentBottom, AppSkin ui, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, panel.Min, panel.Max, PanelRounding * scale, ImGui.GetColorU32(PanelFill));
        drawList.AddLine(new Vector2(panel.Min.X + PanelRounding * scale, panel.Min.Y),
            new Vector2(panel.Max.X - PanelRounding * scale, panel.Min.Y), ImGui.GetColorU32(Hairline),
            Metrics.Stroke.Hairline * scale);
        var tabs = new Rect(new Vector2(panel.Min.X, contentBottom - TabRowHeight * scale),
            new Vector2(panel.Max.X, contentBottom));
        var content = new Rect(panel.Min, new Vector2(panel.Max.X, tabs.Min.Y));
        var interactive = !session.Saving;
        switch (session.Tool)
        {
            case PhotoEditTool.Looks:
                DrawLooks(session, content, ui, scale, interactive);
                break;
            case PhotoEditTool.Crop:
                DrawCrop(session, content, ui, scale, interactive);
                break;
            default:
                DrawAdjust(session, content, ui, scale, interactive);
                break;
        }

        DrawTabs(session, tabs, ui, scale, interactive);
    }

    private static Rect ChipRow(Rect content, float scale)
    {
        var top = content.Min.Y + ChipRowTop * scale;
        return new Rect(new Vector2(content.Min.X + SideInset * scale, top),
            new Vector2(content.Max.X - SideInset * scale, top + ChipRail.RowHeight * scale));
    }

    private static Rect ScrubberTrack(Rect content, float top, float scale, float leftInset, float rightInset)
    {
        var centerY = top + ScrubberGap * scale;
        var half = ScrubberThickness * 0.5f * scale;
        return new Rect(new Vector2(content.Min.X + leftInset, centerY - half),
            new Vector2(content.Max.X - rightInset, centerY + half));
    }

    private static void DrawAdjust(PhotoEditSession session, Rect content, AppSkin ui, float scale, bool interactive)
    {
        var adjustments = PhotoEditSession.Adjustments;
        for (var index = 0; index < adjustments.Length; index++)
        {
            session.AdjustmentLabels[index] = Loc.T(AdjustmentLabel(adjustments[index]));
            session.AdjustmentActive[index] = adjustments[index] == session.Adjustment;
        }

        var row = ChipRow(content, scale);
        var picked = session.AdjustmentRail.Draw(row, ui, session.AdjustmentLabels, session.AdjustmentActive, true,
            centered: true, interactive: interactive);
        if (picked >= 0)
        {
            session.Adjustment = adjustments[picked];
        }

        var (min, max) = PhotoEdit.RangeOf(session.Adjustment);
        var value = session.Edit.ValueOf(session.Adjustment);
        var track = ScrubberTrack(content, row.Max.Y, scale, SideInset * scale, (SideInset + ValueWidth) * scale);
        var fraction = (value - min) / (max - min);
        var updated = DrawScrubber(track, fraction, min < 0f, ui.Accent, scale, 1f, interactive);
        if (updated != fraction)
        {
            session.Adjust(session.Adjustment, min + (updated * (max - min)));
        }

        var label = session.ValueLabel(session.Adjustment, session.Edit.ValueOf(session.Adjustment), Loc.Culture);
        Typography.DrawCentered(ImGui.GetWindowDrawList(),
            new Vector2(track.Max.X + (ValueWidth * 0.5f * scale), track.Center.Y), label, White,
            TextStyles.SubheadlineEmphasized);
    }

    private static void DrawLooks(PhotoEditSession session, Rect content, AppSkin ui, float scale, bool interactive)
    {
        var looks = PhotoLooks.All;
        for (var index = 0; index < looks.Length; index++)
        {
            session.LookLabels[index] = Loc.T(LookLabel(looks[index]));
            session.LookActive[index] = looks[index] == session.Edit.Look;
        }

        var row = ChipRow(content, scale);
        var picked = session.LookRail.Draw(row, ui, session.LookLabels, session.LookActive, true, centered: true,
            interactive: interactive);
        if (picked >= 0)
        {
            session.SetLook(looks[picked]);
        }

        var hasLook = session.Edit.Look != PhotoLook.None;
        var track = ScrubberTrack(content, row.Max.Y, scale, SideInset * scale, (SideInset + ValueWidth) * scale);
        var strength = session.Edit.LookStrength;
        var updated = DrawScrubber(track, strength, false, ui.Accent, scale, hasLook ? 1f : DimmedAlpha,
            interactive && hasLook);
        if (hasLook && updated != strength)
        {
            session.SetLookStrength(updated);
        }

        var label = session.ValueLabel(PhotoAdjustment.Vignette, hasLook ? strength : 0f, Loc.Culture);
        Typography.DrawCentered(ImGui.GetWindowDrawList(),
            new Vector2(track.Max.X + (ValueWidth * 0.5f * scale), track.Center.Y), label,
            hasLook ? White : WhiteMuted, TextStyles.SubheadlineEmphasized);
    }

    private static void DrawCrop(PhotoEditSession session, Rect content, AppSkin ui, float scale, bool interactive)
    {
        var aspects = PhotoCropAspects.All;
        for (var index = 0; index < aspects.Length; index++)
        {
            session.AspectLabels[index] = Loc.T(AspectLabel(aspects[index]));
            session.AspectActive[index] = aspects[index] == session.Aspect;
        }

        var row = ChipRow(content, scale);
        var picked = session.AspectRail.Draw(row, ui, session.AspectLabels, session.AspectActive, true,
            centered: true, interactive: interactive);
        if (picked >= 0)
        {
            session.Aspect = aspects[picked];
        }

        var rowCenterY = row.Max.Y + (ScrubberGap * scale);
        var rotateCenter = new Vector2(content.Min.X + (SideInset + IconRadius) * scale, rowCenterY);
        var flipCenter = new Vector2(rotateCenter.X + (IconGap * scale), rowCenterY);
        if (ui.IconButton(rotateCenter, IconRadius * scale, IconGlyph.Of(FontAwesomeIcon.Redo), White,
                IconBackground, 0.85f, Loc.T(L.Photos.Rotate)) && interactive)
        {
            session.Rotate();
        }

        if (ui.IconButton(flipCenter, IconRadius * scale, IconGlyph.Of(FontAwesomeIcon.ArrowsAltH), White,
                IconBackground, 0.85f, Loc.T(L.Photos.Flip)) && interactive)
        {
            session.Flip();
        }

        var size = session.StageTextureSize;
        if (size.X <= 0f)
        {
            return;
        }

        var leftInset = flipCenter.X + ((IconRadius + SideInset) * scale) - content.Min.X;
        var track = ScrubberTrack(content, row.Max.Y, scale, leftInset, SideInset * scale);
        var ratio = session.CropRatio;
        var fraction = session.Crop.ZoomFraction(size, ratio, false);
        var updated = DrawScrubber(track, fraction, false, ui.Accent, scale, 1f, interactive);
        if (updated != fraction)
        {
            session.Crop.SetZoomFraction(updated, size, ratio, false);
        }
    }

    private static void DrawTabs(PhotoEditSession session, Rect tabs, AppSkin ui, float scale, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = TabWidth * scale;
        var left = tabs.Center.X - (width * 1.5f);
        for (var index = 0; index < 3; index++)
        {
            var tool = (PhotoEditTool)index;
            var rect = new Rect(new Vector2(left + (index * width), tabs.Min.Y),
                new Vector2(left + ((index + 1) * width), tabs.Max.Y));
            var active = tool == session.Tool;
            var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
            var color = active ? ui.Accent : hovered ? White : WhiteMuted;
            var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + (TabIconOffset * scale));
            AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(TabIcon(tool)), color, 0.95f);
            Typography.DrawCentered(drawList, new Vector2(rect.Center.X, rect.Min.Y + (TabLabelOffset * scale)),
                Loc.T(TabLabel(tool)), color, TextStyles.Caption1);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                session.Tool = tool;
            }
        }
    }

    private static float DrawScrubber(Rect track, float value, bool bipolar, Vector4 accent, float scale, float alpha,
        bool interactive)
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
        drawList.AddRectFilled(railMin, railMax, ImGui.GetColorU32(Rail with { W = Rail.W * alpha }),
            thickness * 0.5f);
        var knobX = left + (width * result);
        var fillStart = bipolar ? left + (width * 0.5f) : left;
        var fillMin = new Vector2(MathF.Min(fillStart, knobX), railMin.Y);
        var fillMax = new Vector2(MathF.Max(fillStart, knobX), railMax.Y);
        drawList.AddRectFilled(fillMin, fillMax, ImGui.GetColorU32(accent with { W = accent.W * alpha }),
            thickness * 0.5f);
        if (bipolar)
        {
            var tickHalf = thickness * CenterTickHeight * 0.5f;
            drawList.AddLine(new Vector2(fillStart, midY - tickHalf), new Vector2(fillStart, midY + tickHalf),
                ImGui.GetColorU32(White with { W = 0.5f * alpha }), Metrics.Stroke.Thin * scale);
        }

        drawList.AddCircleFilled(new Vector2(knobX, midY), (thickness * 0.5f) + (KnobExtra * scale),
            ImGui.GetColorU32(White with { W = alpha }), 24);
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

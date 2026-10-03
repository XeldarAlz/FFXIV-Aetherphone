using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float NudgeHoldDelay = 0.35f;
    private const float NudgeRepeatInterval = 0.07f;
    private const float AngleRange = MathF.PI;
    private const float TurnRange = 2f * MathF.PI;
    private const float SliderLabelColumn = 62f;
    private const float SliderValueColumn = 44f;
    private const float StudioResetColumn = 26f;
    private const float StudioResetTolerance = 0.005f;
    private const float StudioRowHeight = 44f;
    private const double SliderSaveDelaySeconds = 0.6;

    private const float StageHeight = 236f;
    private const float StageInset = 22f;
    private const int StageRings = 3;
    private const float MinStageView = 5f;
    private const float StageViewMargin = 1.5f;
    private const float StageZoomTime = 0.22f;
    private const float StageBeamShare = 0.5f;
    private const float StageBeamSpread = 0.3f;
    private const int StageArcSegments = 12;
    private const float StageArcThickness = 5f;
    private const float StageArcGlow = 13f;
    private const float StageGripRadius = 8f;
    private const float StageKnobRadius = 6f;
    private const float StageKnobReach = 34f;
    private const float StageHandleHit = 15f;
    private const float StageTurnDeadZone = 6f;
    private const float StageToggleRadius = 16f;
    private const float GhostDistance = 2f;
    private const float GhostStrength = 0.38f;

    private const float ActionTileHeight = 76f;
    private const float FeatureTile = 36f;
    private const float FeatureToggleWidth = 46f;
    private const float FeatureToggleHeight = 27f;
    private const float NudgePadRadius = 60f;
    private const float NudgeArrowRadius = 17f;
    private const float NudgeRockerWidth = 46f;
    private const float NudgeRockerHeight = 100f;
    private const float NudgeClusterGap = 30f;
    private const float PresetChipHeight = 34f;
    private const float PresetNameWidth = 140f;
    private const float ChannelTileHeight = 38f;
    private const float FalloffHeight = 76f;
    private const float FalloffHeadroom = 20f;
    private const int FalloffSamples = 80;

    private const int NudgeLeft = 0;
    private const int NudgeRight = 1;
    private const int NudgeUp = 2;
    private const int NudgeDown = 3;
    private const int NudgeCloser = 4;
    private const int NudgeFarther = 5;

    private static readonly float[] NudgeSteps = [0.01f, 0.05f, 0.25f];
    private static readonly Vector4 StageTop = new(0.118f, 0.094f, 0.180f, 1f);
    private static readonly Vector4 StageBottom = new(0.055f, 0.043f, 0.094f, 1f);
    private static readonly Vector4 StageRing = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 SayInk = new(0.97f, 0.97f, 0.97f, 1f);
    private static readonly Vector4 PartyInk = new(0.40f, 0.90f, 1f, 1f);
    private static readonly Vector4 FreeCompanyInk = new(0.67f, 0.86f, 0.55f, 1f);
    private static readonly Vector4 ShoutInk = new(1f, 0.65f, 0.40f, 1f);

    private enum StageDrag : byte
    {
        None,
        Move,
        Turn,
    }

    private readonly struct StagePlan
    {
        private readonly Vector2 center;
        private readonly Vector3 origin;
        private readonly float pixelsPerYalm;
        private readonly float forwardX;
        private readonly float forwardZ;

        public StagePlan(Vector2 center, Vector3 origin, float heading, float pixelsPerYalm)
        {
            this.center = center;
            this.origin = origin;
            this.pixelsPerYalm = pixelsPerYalm;
            forwardX = MathF.Sin(heading);
            forwardZ = MathF.Cos(heading);
        }

        public Vector2 Project(Vector3 world)
        {
            var deltaX = world.X - origin.X;
            var deltaZ = world.Z - origin.Z;
            var across = deltaZ * forwardX - deltaX * forwardZ;
            var ahead = deltaX * forwardX + deltaZ * forwardZ;
            return new Vector2(center.X + across * pixelsPerYalm, center.Y - ahead * pixelsPerYalm);
        }

        public Vector3 Unproject(Vector2 point, float height)
        {
            var across = (point.X - center.X) / pixelsPerYalm;
            var ahead = (center.Y - point.Y) / pixelsPerYalm;
            return new Vector3(origin.X - forwardZ * across + forwardX * ahead, height,
                origin.Z + forwardX * across + forwardZ * ahead);
        }
    }

    private readonly string[] nudgeStepLabels = new string[3];
    private readonly TextCache[] sliderValueTexts = new TextCache[6];
    private readonly PanRail presetRail = new();
    private TextCache volumeHereText;
    private Spring stageZoom = new(MinStageView);
    private StageDrag stageDrag;
    private Vector2 stageGrabOffset;
    private string screenPresetName = string.Empty;
    private int nudgeStepIndex = 1;
    private int nudgeHeldKey = -1;
    private float nudgeHeldTime;
    private float nudgeRepeatTime;
    private double saveDueAtTime;

    private void DrawScreenEditor(Rect area, float scale)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.AetherStream.Screen), Ink, back, ScreenTitleStyle);
        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var engine = screen.Engine;
        using (var surface = AppSurface.BeginEdgeToEdge(content))
        {
            Gap(Metrics.Space.Xs);
            var live = engine.IsActive && engine.ScreenVisible;
            DrawStage(engine, live, scale);
            if (stageDrag != StageDrag.None)
            {
                surface.CancelDrag();
            }

            DrawStageActions(engine, live, scale);
            if (live)
            {
                DrawShapeCard(engine, scale);
                DrawNudgePad(engine, scale);
            }

            DrawPlacementMemory(engine, live, scale);
            DrawFollowHost(scale);
            DrawChatCard(scale);
            DrawSoundCard(engine, live, scale);
            Gap(Metrics.Space.Lg);
        }
    }

    private void DrawStage(VideoEngine engine, bool live, float scale)
    {
        var card = BeginBlock(StageHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(StageTop),
            ImGui.GetColorU32(StageBottom));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(Ink.ChipStroke), 1f);

        var caption = engine.IsActive ? string.Empty : Loc.T(L.AetherStream.CastingScreenPositionHint);
        var captionWidth = card.Width - Metrics.Space.Xl * 2f * scale;
        var captionHeight = caption.Length > 0
            ? Typography.MeasureWrappedBlock(caption, TextStyles.Footnote, captionWidth).Y + Metrics.Space.Lg * scale
            : 0f;
        var floor = new Rect(card.Min, new Vector2(card.Max.X, card.Max.Y - captionHeight));
        var center = floor.Center;
        var reach = MathF.Min(floor.Width, floor.Height) * 0.5f - StageInset * scale;

        drawList.PushClipRect(card.Min, card.Max, true);
        var ringInk = ImGui.GetColorU32(StageRing);
        for (var ring = 1; ring <= StageRings; ring++)
        {
            drawList.AddCircle(center, reach * ring / StageRings, ringInk, 64, 1f * scale);
        }

        if (Plugin.ObjectTable.LocalPlayer is { } player)
        {
            DrawStageScene(drawList, engine, live, player.Position, player.Rotation, center, reach, scale);
        }
        else
        {
            stageDrag = StageDrag.None;
        }

        drawList.PopClipRect();
        DrawStagePlayer(drawList, center, scale);
        if (caption.Length > 0)
        {
            Typography.DrawWrappedCentered(drawList,
                new Vector2(card.Center.X, card.Max.Y - captionHeight * 0.5f - Metrics.Space.Xs * scale), caption,
                Ink.MutedInk, TextStyles.Footnote, captionWidth);
        }

        DrawStageToggles(drawList, engine, card, scale);
        EndBlock();
    }

    private void DrawStageScene(ImDrawListPtr drawList, VideoEngine engine, bool live, Vector3 playerPosition,
        float heading, Vector2 center, float reach, float scale)
    {
        var pose = engine.IsActive
            ? engine.ScreenPose
            : new ScreenPose(
                playerPosition + new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)) * GhostDistance,
                heading + MathF.PI, 0f, 0f, 1f);
        var halfWidth = ScreenGeometry.HalfWidth * pose.Scale;
        var planar = new Vector2(pose.Position.X - playerPosition.X, pose.Position.Z - playerPosition.Z).Length();
        if (stageDrag == StageDrag.None)
        {
            stageZoom.Step(MathF.Max(MinStageView, planar + halfWidth + StageViewMargin), StageZoomTime,
                MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        }

        var plan = new StagePlan(center, playerPosition, heading, reach / MathF.Max(stageZoom.Value, MinStageView));
        var screenPoint = plan.Project(pose.Position);
        var strength = live ? 1f : GhostStrength;
        if (configuration.VideoSpatialAudio)
        {
            var soundRadius = configuration.VideoSpatialRange * reach / MathF.Max(stageZoom.Value, MinStageView);
            drawList.AddCircleFilled(screenPoint, soundRadius,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.045f * strength)), 64);
            drawList.AddCircleFilled(screenPoint, soundRadius * SpatialVolume.FullVolumeShare,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.07f * strength)), 48);
            drawList.AddCircle(screenPoint, soundRadius,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f * strength)), 64, 1.2f * scale);
        }

        var curveDepth = engine.ScreenCurveDepth;
        var left = plan.Project(ScreenGeometry.Corner(pose, -1f, 0f, curveDepth));
        var right = plan.Project(ScreenGeometry.Corner(pose, 1f, 0f, curveDepth));
        var facing = plan.Project(pose.Position + ScreenGeometry.Facing(pose)) - screenPoint;
        var facingLength = facing.Length();
        facing = facingLength > 0.001f ? facing / facingLength : new Vector2(0f, 1f);
        var span = right - left;
        var beam = facing * reach * StageBeamShare;
        drawList.AddQuadFilled(left, right, right + beam + span * StageBeamSpread, left + beam - span * StageBeamSpread,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.11f * strength)));

        var glow = ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.26f * strength));
        var core = ImGui.GetColorU32(Palette.WithAlpha(live ? WhiteInk : Ink.MutedInk, live ? 1f : 0.7f));
        StrokeStageArc(drawList, plan, pose, curveDepth, left, glow, StageArcGlow * scale);
        StrokeStageArc(drawList, plan, pose, curveDepth, left, core, StageArcThickness * scale);
        drawList.AddCircleFilled(left, StageArcThickness * 0.5f * scale, core, 12);
        drawList.AddCircleFilled(right, StageArcThickness * 0.5f * scale, core, 12);

        if (!live)
        {
            stageDrag = StageDrag.None;
            return;
        }

        DriveStageHandles(drawList, engine, pose, plan, screenPoint, facing, center, reach, scale);
    }

    private static void StrokeStageArc(ImDrawListPtr drawList, in StagePlan plan, in ScreenPose pose, float curveDepth,
        Vector2 start, uint color, float thickness)
    {
        var previous = start;
        for (var segment = 1; segment <= StageArcSegments; segment++)
        {
            var along = -1f + 2f * segment / StageArcSegments;
            var point = plan.Project(ScreenGeometry.Corner(pose, along, 0f, curveDepth));
            drawList.AddLine(previous, point, color, thickness);
            previous = point;
        }
    }

    private void DriveStageHandles(ImDrawListPtr drawList, VideoEngine engine, in ScreenPose pose, in StagePlan plan,
        Vector2 screenPoint, Vector2 facing, Vector2 center, float reach, float scale)
    {
        var knobPoint = screenPoint + facing * StageKnobReach * scale;
        var hit = new Vector2(StageHandleHit * scale, StageHandleHit * scale);
        var pointer = ImGui.GetMousePos();
        var overKnob = UiInteract.Hover(knobPoint - hit, knobPoint + hit);
        var overGrip = !overKnob && UiInteract.Hover(screenPoint - hit, screenPoint + hit);
        if (stageDrag == StageDrag.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (overKnob)
            {
                stageDrag = StageDrag.Turn;
            }
            else if (overGrip)
            {
                stageDrag = StageDrag.Move;
                stageGrabOffset = screenPoint - pointer;
            }
        }

        if (stageDrag != StageDrag.None && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            stageDrag = StageDrag.None;
        }

        if (stageDrag == StageDrag.Move)
        {
            var target = pointer + stageGrabOffset;
            var offset = target - center;
            var length = offset.Length();
            if (length > reach)
            {
                target = center + offset / length * reach;
            }

            engine.SetScreenTransform(plan.Unproject(target, pose.Position.Y), pose.Yaw, pose.Pitch, pose.Roll,
                pose.Scale);
        }
        else if (stageDrag == StageDrag.Turn && Vector2.Distance(pointer, screenPoint) > StageTurnDeadZone * scale)
        {
            var aim = plan.Unproject(pointer, pose.Position.Y);
            engine.SetScreenTransform(pose.Position, ScreenGeometry.YawFacing(pose.Position, aim), pose.Pitch,
                pose.Roll, pose.Scale);
        }

        var dragging = stageDrag != StageDrag.None;
        if (dragging || overKnob || overGrip)
        {
            UiInteract.ReportGestureSurface();
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (dragging)
        {
            UiInteract.CancelPendingTap();
        }

        var knobRadius = StageKnobRadius * scale * (overKnob || stageDrag == StageDrag.Turn ? 1.25f : 1f);
        var gripRadius = StageGripRadius * scale * (overGrip || stageDrag == StageDrag.Move ? 1.2f : 1f);
        drawList.AddLine(screenPoint, knobPoint, ImGui.GetColorU32(Palette.WithAlpha(Ink.AccentLink, 0.7f)),
            1.5f * scale);
        drawList.AddCircleFilled(knobPoint, knobRadius, ImGui.GetColorU32(ui.Accent), 24);
        drawList.AddCircle(knobPoint, knobRadius, ImGui.GetColorU32(WhiteInk), 24, 1.5f * scale);
        drawList.AddCircleFilled(screenPoint, gripRadius, ImGui.GetColorU32(WhiteInk), 24);
        drawList.AddCircle(screenPoint, gripRadius, ImGui.GetColorU32(ui.Accent), 24, 2.2f * scale);
    }

    private void DrawStagePlayer(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        drawList.AddCircleFilled(center, 11f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.24f)), 24);
        drawList.AddTriangleFilled(new Vector2(center.X, center.Y - 16f * scale),
            new Vector2(center.X - 4.5f * scale, center.Y - 8.5f * scale),
            new Vector2(center.X + 4.5f * scale, center.Y - 8.5f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(WhiteInk, 0.85f)));
        drawList.AddCircleFilled(center, 5.5f * scale, ImGui.GetColorU32(WhiteInk), 20);
    }

    private void DrawStageToggles(ImDrawListPtr drawList, VideoEngine engine, Rect card, float scale)
    {
        var radius = StageToggleRadius * scale;
        var inset = Metrics.Space.Md * scale + radius;
        var delta = ImGui.GetIO().DeltaTime;
        var windowCenter = new Vector2(card.Max.X - inset, card.Min.Y + inset);
        var worldCenter = new Vector2(windowCenter.X - radius * 2f - Metrics.Space.Sm * scale, windowCenter.Y);
        var visible = configuration.VideoScreenVisible;
        if (HoverButton.Circle(drawList, "aetherstream.stage.world", worldCenter, radius,
                visible ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash, visible ? ui.Accent : Ink.ButtonFill,
                visible ? WhiteInk : Ink.MutedInk, delta, 1f, true, Loc.T(L.AetherStream.InGameScreen)))
        {
            configuration.VideoScreenVisible = !visible;
            configuration.Save();
            engine.ScreenVisible = !visible;
            if (visible)
            {
                suite.PlacingScreen = false;
            }
        }

        var windowOpen = screenWindow.IsOpen;
        if (HoverButton.Circle(drawList, "aetherstream.stage.window", windowCenter, radius,
                FontAwesomeIcon.WindowRestore, windowOpen ? ui.Accent : Ink.ButtonFill,
                windowOpen ? WhiteInk : Ink.MutedInk, delta, 1f, true, Loc.T(L.AetherStream.OpenScreenWindow)))
        {
            screenWindow.IsOpen = !windowOpen;
        }
    }

    private void DrawStageActions(VideoEngine engine, bool live, float scale)
    {
        Gap(Metrics.Space.Md);
        var row = BeginBlock(ActionTileHeight * scale);
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (row.Width - gap * 2f) / 3f;
        var placing = suite.PlacingScreen;
        if (ActionTile(new Rect(row.Min, new Vector2(row.Min.X + tileWidth, row.Max.Y)), FontAwesomeIcon.ArrowsAlt,
                Loc.T(placing ? L.AetherStream.PlaceDone : L.AetherStream.PlaceInWorld), placing, live, scale))
        {
            suite.PlacingScreen = !placing;
        }

        var middleLeft = row.Min.X + tileWidth + gap;
        if (ActionTile(new Rect(new Vector2(middleLeft, row.Min.Y), new Vector2(middleLeft + tileWidth, row.Max.Y)),
                FontAwesomeIcon.Crosshairs, Loc.T(L.AetherStream.CastingRecenter), false, live, scale))
        {
            engine.RecenterScreen();
        }

        if (ActionTile(new Rect(new Vector2(row.Max.X - tileWidth, row.Min.Y), row.Max), FontAwesomeIcon.UserCircle,
                Loc.T(L.AetherStream.FaceMe), false, live, scale))
        {
            engine.FaceLocalPlayer();
        }

        EndBlock();
        if (placing)
        {
            Gap(Metrics.Space.Sm);
            DrawNote(Loc.T(L.AetherStream.PlaceInWorldHint), scale);
        }
    }

    private bool ActionTile(Rect rect, FontAwesomeIcon icon, string label, bool active, bool enabled, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Md * scale;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        if (active)
        {
            IconTile.FillShaded(drawList, rect.Min, rect.Max, rounding, IconTile.Surface(ui.Accent));
        }
        else
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(hovered ? Ink.ButtonHover : Ink.FieldFill));
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Ink.ChipStroke), 1f);
        }

        var strength = enabled ? 1f : 0.38f;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var iconInk = active ? WhiteInk : Palette.WithAlpha(Ink.AccentLink, strength);
        var labelInk = active ? WhiteInk : Palette.WithAlpha(Ink.TitleInk, strength);
        ProgressRing.CenterIcon(drawList, new Vector2(rect.Center.X, rect.Min.Y + (rect.Height - labelHeight) * 0.5f),
            icon, iconInk, 20f * scale);
        Typography.DrawCentered(drawList,
            new Vector2(rect.Center.X, rect.Max.Y - Metrics.Space.Md * scale - labelHeight * 0.5f),
            Typography.FitText(label, rect.Width - Metrics.Space.Md * scale, TextStyles.FootnoteEmphasized), labelInk,
            TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawNote(string text, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var width = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f;
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width).Y;
        var card = BeginBlock(height + pad * 2f);
        TintedCard(ImGui.GetWindowDrawList(), card, ui.Accent);
        Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad, card.Min.Y + pad), text, Ink.BodyInk,
            TextStyles.Footnote, width);
        EndBlock();
    }

    private static Rect StudioRow(Rect card, float top, int index, float rowHeight, float pad) =>
        new(new Vector2(card.Min.X + pad, top + index * rowHeight),
            new Vector2(card.Max.X - pad, top + (index + 1) * rowHeight));

    private void DrawShapeCard(VideoEngine engine, float scale)
    {
        Gap(Metrics.Space.Md);
        SectionLabel(Loc.T(L.AetherStream.SizeAndAngle));
        var rowHeight = StudioRowHeight * scale;
        var pad = Metrics.Space.Lg * scale;
        var inset = Metrics.Space.Sm * scale;
        var card = BeginBlock(rowHeight * 5f + inset * 2f);
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);
        var top = card.Min.Y + inset;
        var divider = top + rowHeight * 2f;
        drawList.AddLine(new Vector2(card.Min.X + pad, divider), new Vector2(card.Max.X - pad, divider),
            ImGui.GetColorU32(Ink.Hairline), 1f);

        var pose = engine.ScreenPose;
        var size = pose.Scale;
        var yaw = pose.Yaw;
        var pitch = pose.Pitch;
        var roll = pose.Roll;
        var curve = engine.ScreenCurve;
        var changed = StudioSlider(StudioRow(card, top, 0, rowHeight, pad), 0, "aetherstream.screen.scale",
            Loc.T(L.AetherStream.CastingScale), ref size, VideoEngine.MinScreenScale, VideoEngine.MaxScreenScale,
            "{0}%", 100f, 1f, scale);
        var curveChanged = StudioSlider(StudioRow(card, top, 1, rowHeight, pad), 4, "aetherstream.screen.curve",
            Loc.T(L.AetherStream.CastingCurve), ref curve, 0f, VideoEngine.MaxScreenCurve, "{0}%", 100f, 1f, scale);
        changed |= StudioSlider(StudioRow(card, top, 2, rowHeight, pad), 1, "aetherstream.screen.yaw",
            Loc.T(L.AetherStream.CastingRotate), ref yaw, -TurnRange, TurnRange, "{0}°", 180f / MathF.PI, float.NaN,
            scale);
        changed |= StudioSlider(StudioRow(card, top, 3, rowHeight, pad), 2, "aetherstream.screen.pitch",
            Loc.T(L.AetherStream.CastingTilt), ref pitch, -AngleRange, AngleRange, "{0}°", 180f / MathF.PI, 0f, scale);
        changed |= StudioSlider(StudioRow(card, top, 4, rowHeight, pad), 3, "aetherstream.screen.roll",
            Loc.T(L.AetherStream.CastingRoll), ref roll, -AngleRange, AngleRange, "{0}°", 180f / MathF.PI, 0f, scale);
        EndBlock();
        if (changed)
        {
            engine.SetScreenTransform(pose.Position, yaw, pitch, roll, size);
        }

        if (curveChanged)
        {
            engine.ScreenCurve = curve;
            configuration.VideoScreenCurve = engine.ScreenCurve;
            SaveSoon();
        }
    }

    private bool StudioSlider(Rect row, int cacheIndex, string id, string label, ref float value, float min, float max,
        string valueFormat, float valueFactor, float resetValue, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var labelColumn = SliderLabelColumn * scale;
        var valueColumn = SliderValueColumn * scale;
        var resetColumn = StudioResetColumn * scale;
        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, labelColumn - Metrics.Space.Xs * scale, TextStyles.SubheadlineEmphasized),
            Ink.TitleInk, TextStyles.SubheadlineEmphasized);

        var span = MathF.Max(max - min, 0.001f);
        var result = Slider.Draw(id, row, Math.Clamp((value - min) / span, 0f, 1f), accentedTheme, labelColumn,
            valueColumn + resetColumn);
        var engaged = result.Dragging || result.Released;
        var shown = engaged ? min + result.Value * span : value;
        var valueText = sliderValueTexts[cacheIndex].Format(valueFormat, (int)MathF.Round(shown * valueFactor));
        var valueSize = Typography.Measure(valueText, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList,
            new Vector2(row.Max.X - resetColumn - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), valueText,
            Ink.BodyInk, TextStyles.FootnoteEmphasized);

        var changed = false;
        if (engaged && MathF.Abs(shown - value) > 0.0001f)
        {
            value = shown;
            changed = true;
        }

        if (float.IsNaN(resetValue) || MathF.Abs(value - resetValue) <= StudioResetTolerance)
        {
            return changed;
        }

        var resetCenter = new Vector2(row.Max.X - resetColumn * 0.5f + 4f * scale, row.Center.Y);
        if (ui.IconButton(resetCenter, resetColumn * 0.5f, IconGlyph.Of(FontAwesomeIcon.UndoAlt), Ink.MutedInk,
                AppSkin.Transparent, 0.5f))
        {
            value = resetValue;
            return true;
        }

        return changed;
    }

    private void SaveSoon() => saveDueAtTime = ImGui.GetTime() + SliderSaveDelaySeconds;

    private void SaveWhenDue()
    {
        if (saveDueAtTime <= 0d || ImGui.GetTime() < saveDueAtTime)
        {
            return;
        }

        saveDueAtTime = 0d;
        configuration.Save();
    }

    private void FlushScreenSave()
    {
        if (saveDueAtTime <= 0d)
        {
            return;
        }

        saveDueAtTime = 0d;
        configuration.Save();
    }

    private void DrawNudgePad(VideoEngine engine, float scale)
    {
        Gap(Metrics.Space.Md);
        SectionLabel(Loc.T(L.AetherStream.FineTune));
        var pad = Metrics.Space.Lg * scale;
        var plate = NudgePadRadius * scale;
        var captionHeight = Typography.LineHeight(TextStyles.Caption1);
        var zoneHeight = plate * 2f + Metrics.Space.Sm * scale + captionHeight;
        var card = BeginBlock(pad + SegmentHeight * scale + Metrics.Space.Lg * scale + zoneHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);

        nudgeStepLabels[0] = Loc.T(L.AetherStream.StepSmall);
        nudgeStepLabels[1] = Loc.T(L.AetherStream.StepMedium);
        nudgeStepLabels[2] = Loc.T(L.AetherStream.StepLarge);
        var stripTop = card.Min.Y + pad;
        nudgeStepIndex = SegmentStrip.Draw("aetherstream.nudgeStep",
            new Rect(new Vector2(card.Min.X + pad, stripTop),
                new Vector2(card.Max.X - pad, stripTop + SegmentHeight * scale)), nudgeStepLabels, nudgeStepIndex,
            ui.Palette);

        var zoneTop = stripTop + SegmentHeight * scale + Metrics.Space.Lg * scale;
        var rockerWidth = NudgeRockerWidth * scale;
        var clusterWidth = plate * 2f + NudgeClusterGap * scale + rockerWidth;
        var padCenter = new Vector2(card.Center.X - clusterWidth * 0.5f + plate, zoneTop + plate);
        drawList.AddCircleFilled(padCenter, plate, ImGui.GetColorU32(Ink.ChipFill), 64);
        drawList.AddCircle(padCenter, plate, ImGui.GetColorU32(Ink.ChipStroke), 64, 1f);
        drawList.AddCircleFilled(padCenter, 3f * scale, ImGui.GetColorU32(Ink.FaintInk), 12);
        var arrow = NudgeArrowRadius * scale;
        var throwDistance = plate - arrow - 5f * scale;
        var side = 0;
        var height = 0;
        var depth = 0;
        if (NudgeButton(drawList, NudgeLeft, new Vector2(padCenter.X - throwDistance, padCenter.Y), arrow,
                FontAwesomeIcon.ArrowLeft))
        {
            side--;
        }

        if (NudgeButton(drawList, NudgeRight, new Vector2(padCenter.X + throwDistance, padCenter.Y), arrow,
                FontAwesomeIcon.ArrowRight))
        {
            side++;
        }

        if (NudgeButton(drawList, NudgeUp, new Vector2(padCenter.X, padCenter.Y - throwDistance), arrow,
                FontAwesomeIcon.ArrowUp))
        {
            height++;
        }

        if (NudgeButton(drawList, NudgeDown, new Vector2(padCenter.X, padCenter.Y + throwDistance), arrow,
                FontAwesomeIcon.ArrowDown))
        {
            height--;
        }

        var rockerHeight = NudgeRockerHeight * scale;
        var rockerCenter = new Vector2(padCenter.X + plate + NudgeClusterGap * scale + rockerWidth * 0.5f,
            padCenter.Y);
        var rockerMin = new Vector2(rockerCenter.X - rockerWidth * 0.5f, rockerCenter.Y - rockerHeight * 0.5f);
        var rockerMax = new Vector2(rockerCenter.X + rockerWidth * 0.5f, rockerCenter.Y + rockerHeight * 0.5f);
        drawList.AddRectFilled(rockerMin, rockerMax, ImGui.GetColorU32(Ink.ChipFill), rockerWidth * 0.5f);
        drawList.AddRect(rockerMin, rockerMax, ImGui.GetColorU32(Ink.ChipStroke), rockerWidth * 0.5f);
        var rockerThrow = rockerHeight * 0.5f - arrow - 6f * scale;
        if (NudgeButton(drawList, NudgeCloser, new Vector2(rockerCenter.X, rockerCenter.Y - rockerThrow), arrow,
                FontAwesomeIcon.Plus))
        {
            depth++;
        }

        if (NudgeButton(drawList, NudgeFarther, new Vector2(rockerCenter.X, rockerCenter.Y + rockerThrow), arrow,
                FontAwesomeIcon.Minus))
        {
            depth--;
        }

        var captionWidth = (card.Max.X - pad - rockerCenter.X) * 2f;
        Typography.DrawCentered(drawList,
            new Vector2(rockerCenter.X, zoneTop + plate * 2f + Metrics.Space.Sm * scale + captionHeight * 0.5f),
            Typography.FitText(Loc.T(L.AetherStream.PlaceHandleDepth), captionWidth, TextStyles.Caption1),
            Ink.MutedInk, TextStyles.Caption1);
        EndBlock();

        if (side != 0 || height != 0 || depth != 0)
        {
            var step = NudgeSteps[Math.Clamp(nudgeStepIndex, 0, NudgeSteps.Length - 1)];
            engine.NudgeScreen(side * step, height * step, depth * step);
        }
    }

    private bool NudgeButton(ImDrawListPtr drawList, int key, Vector2 center, float radius, FontAwesomeIcon icon)
    {
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(down ? Ink.ButtonHover : Ink.ButtonFill), 28);
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), Ink.TitleInk, 0.62f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!down)
        {
            if (nudgeHeldKey == key)
            {
                nudgeHeldKey = -1;
            }

            return false;
        }

        if (nudgeHeldKey != key)
        {
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                return false;
            }

            nudgeHeldKey = key;
            nudgeHeldTime = 0f;
            nudgeRepeatTime = 0f;
            return true;
        }

        var delta = ImGui.GetIO().DeltaTime;
        nudgeHeldTime += delta;
        if (nudgeHeldTime < NudgeHoldDelay)
        {
            return false;
        }

        nudgeRepeatTime += delta;
        if (nudgeRepeatTime < NudgeRepeatInterval)
        {
            return false;
        }

        nudgeRepeatTime = 0f;
        return true;
    }

    private bool FeatureCard(string id, FontAwesomeIcon icon, string title, string hint, bool value, float bodyHeight,
        out Rect body)
    {
        var scale = UiScale.Current;
        var pad = Metrics.Space.Lg * scale;
        var tile = FeatureTile * scale;
        var toggleWidth = FeatureToggleWidth * scale;
        var toggleHeight = FeatureToggleHeight * scale;
        var textInset = pad + tile + Metrics.Space.Md * scale;
        var textWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - textInset - pad - toggleWidth
            - Metrics.Space.Md * scale;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var hintHeight = hint.Length > 0
            ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y
            : 0f;
        var textHeight = titleHeight + hintHeight;
        var headerHeight = MathF.Max(tile, textHeight) + pad * 2f;
        var card = BeginBlock(headerHeight + (bodyHeight > 0f ? bodyHeight + pad : 0f));
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);

        var tileMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent), value ? 1f : 0.4f);
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), icon,
            value ? AccentRing.Ink : Ink.MutedInk, tile * 0.48f);

        var textTop = card.Min.Y + pad + MathF.Max(0f, (tile - textHeight) * 0.5f);
        Typography.Draw(drawList, new Vector2(card.Min.X + textInset, textTop),
            Typography.FitText(title, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk, TextStyles.BodyEmphasized);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(card.Min.X + textInset, textTop + titleHeight), hint,
                Ink.MutedInk, TextStyles.Footnote, textWidth);
        }

        var toggleCenterY = card.Min.Y + pad + tile * 0.5f;
        var toggled = Toggle.Draw(id,
            new Rect(new Vector2(card.Max.X - pad - toggleWidth, toggleCenterY - toggleHeight * 0.5f),
                new Vector2(card.Max.X - pad, toggleCenterY + toggleHeight * 0.5f)), value, accentedTheme);
        body = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + headerHeight),
            new Vector2(card.Max.X - pad, card.Min.Y + headerHeight + bodyHeight));
        return toggled;
    }

    private void DrawPlacementMemory(VideoEngine engine, bool live, float scale)
    {
        Gap(Metrics.Space.Md);
        SectionLabel(Loc.T(L.AetherStream.CastingSavedPresets));
        var remembered = FeatureCard("aetherstream.remember", FontAwesomeIcon.MapMarkerAlt,
            Loc.T(L.AetherStream.RememberPlacement), Loc.T(L.AetherStream.RememberPlacementHint),
            configuration.VideoRememberPlacement, 0f, out _);
        EndBlock();
        if (remembered != configuration.VideoRememberPlacement)
        {
            configuration.VideoRememberPlacement = remembered;
            configuration.Save();
        }

        if (!live)
        {
            return;
        }

        Gap(Metrics.Space.Sm);
        var row = BeginBlock(FieldRowHeight * scale);
        var saveRadius = 17f * scale;
        var submitted = SubmitField.Draw(
            new Rect(row.Min, new Vector2(row.Max.X - saveRadius * 2f - Metrics.Space.Sm * scale, row.Max.Y)),
            "##aetherstreamScreenPresetName", Loc.T(L.AetherStream.CastingPresetNameHint), ref screenPresetName,
            accentedTheme, 50, FontAwesomeIcon.Bookmark);
        var canSave = !string.IsNullOrWhiteSpace(screenPresetName);
        if (ui.IconButton(new Vector2(row.Max.X - saveRadius, row.Center.Y), saveRadius,
                IconGlyph.Of(FontAwesomeIcon.Plus), canSave ? WhiteInk : Palette.WithAlpha(WhiteInk, 0.6f),
                canSave ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.35f), 0.6f,
                Loc.T(L.AetherStream.CastingSavePreset)) && canSave)
        {
            submitted = true;
        }

        EndBlock();
        if (submitted && canSave)
        {
            engine.SaveScreenPreset(screenPresetName);
            screenPresetName = string.Empty;
        }

        DrawPresetRail(engine, scale);
    }

    private static float PresetChipWidth(string name, float scale, out string fitted)
    {
        fitted = Typography.FitText(name, PresetNameWidth * scale, TextStyles.SubheadlineEmphasized);
        return Metrics.Space.Md * scale + 18f * scale
            + Typography.Measure(fitted, TextStyles.SubheadlineEmphasized).X + 26f * scale;
    }

    private void DrawPresetRail(VideoEngine engine, float scale)
    {
        var presets = configuration.ScreenPresets;
        if (presets.Count == 0)
        {
            return;
        }

        Gap(Metrics.Space.Sm);
        var chipHeight = PresetChipHeight * scale;
        var pad = PadX * scale;
        var gap = Metrics.Space.Sm * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var rail = new Rect(origin, origin + new Vector2(width, chipHeight));
        var contentWidth = pad * 2f - gap;
        for (var index = 0; index < presets.Count; index++)
        {
            contentWidth += PresetChipWidth(presets[index].Name, scale, out _) + gap;
        }

        var drawList = ImGui.GetWindowDrawList();
        var nameHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        ScreenPositionPreset? applied = null;
        ScreenPositionPreset? removed = null;
        presetRail.Begin(rail, contentWidth);
        var left = rail.Min.X + pad - presetRail.Offset;
        for (var index = 0; index < presets.Count; index++)
        {
            var preset = presets[index];
            var chipWidth = PresetChipWidth(preset.Name, scale, out var name);
            var min = new Vector2(left, rail.Min.Y);
            var max = new Vector2(left + chipWidth, rail.Max.Y);
            left += chipWidth + gap;
            if (max.X < rail.Min.X || min.X > rail.Max.X)
            {
                continue;
            }

            var removeCenter = new Vector2(max.X - 15f * scale, rail.Center.Y);
            var removeExtent = new Vector2(12f * scale, chipHeight * 0.5f);
            var hovered = presetRail.Hover(min, max);
            var overRemove = hovered && presetRail.Hover(removeCenter - removeExtent, removeCenter + removeExtent);
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.ChipFill),
                chipHeight * 0.5f);
            drawList.AddRect(min, max, ImGui.GetColorU32(Ink.ChipStroke), chipHeight * 0.5f);
            AppSkin.Icon(drawList, new Vector2(min.X + Metrics.Space.Md * scale + 6f * scale, rail.Center.Y),
                IconGlyph.Of(FontAwesomeIcon.Bookmark), Ink.AccentLink, 0.55f);
            Typography.Draw(drawList,
                new Vector2(min.X + Metrics.Space.Md * scale + 18f * scale, rail.Center.Y - nameHeight * 0.5f), name,
                Ink.TitleInk, TextStyles.SubheadlineEmphasized);
            AppSkin.Icon(drawList, removeCenter, IconGlyph.Of(FontAwesomeIcon.Times),
                overRemove ? Ink.Danger : Ink.FaintInk, 0.5f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!presetRail.Tapped(min, max, hovered))
            {
                continue;
            }

            if (overRemove)
            {
                removed = preset;
            }
            else
            {
                applied = preset;
            }
        }

        presetRail.End();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, chipHeight));
        if (removed is not null)
        {
            engine.RemoveScreenPreset(removed.Name);
        }

        if (applied is not null)
        {
            engine.ApplyScreenPreset(applied);
            configuration.VideoScreenCurve = engine.ScreenCurve;
            configuration.Save();
        }
    }

    private void DrawFollowHost(float scale)
    {
        if (!watchAlong.IsViewing)
        {
            return;
        }

        Gap(Metrics.Space.Md);
        var following = FeatureCard("aetherstream.follow", FontAwesomeIcon.Link,
            Loc.T(L.AetherStream.FollowHostScreen), Loc.T(L.AetherStream.FollowHostScreenHint),
            configuration.VideoFollowHostScreen, 0f, out _);
        EndBlock();
        if (following != configuration.VideoFollowHostScreen)
        {
            configuration.VideoFollowHostScreen = following;
            configuration.Save();
            if (following)
            {
                watchAlong.ResyncNow();
            }
        }

        if (watchAlong.HostScreenOutOfReach)
        {
            Gap(Metrics.Space.Sm);
            DrawNote(Loc.T(L.AetherStream.HostScreenFarAway), scale);
        }
    }

    private void DrawChatCard(float scale)
    {
        Gap(Metrics.Space.Md);
        var bubbles = configuration.VideoChatBubbles;
        var tileHeight = ChannelTileHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var bubblesOn = FeatureCard("aetherstream.bubbles", FontAwesomeIcon.CommentDots,
            Loc.T(L.AetherStream.ChatBubblesShow), Loc.T(L.AetherStream.ChatBubblesHint), bubbles,
            bubbles ? tileHeight * 2f + gap : 0f, out var body);
        var channels = configuration.VideoChatBubbleChannels;
        if (bubbles)
        {
            var tileWidth = (body.Width - gap) * 0.5f;
            var secondLeft = body.Max.X - tileWidth;
            var secondTop = body.Min.Y + tileHeight + gap;
            channels = ChannelTile(new Rect(body.Min, new Vector2(body.Min.X + tileWidth, body.Min.Y + tileHeight)),
                L.AetherStream.ChannelSay, SayInk, channels, ScreenChatChannels.Say, scale);
            channels = ChannelTile(
                new Rect(new Vector2(secondLeft, body.Min.Y), new Vector2(body.Max.X, body.Min.Y + tileHeight)),
                L.AetherStream.ChannelParty, PartyInk, channels, ScreenChatChannels.Party, scale);
            channels = ChannelTile(
                new Rect(new Vector2(body.Min.X, secondTop), new Vector2(body.Min.X + tileWidth, body.Max.Y)),
                L.AetherStream.ChannelFreeCompany, FreeCompanyInk, channels, ScreenChatChannels.FreeCompany, scale);
            channels = ChannelTile(new Rect(new Vector2(secondLeft, secondTop), body.Max),
                L.AetherStream.ChannelShout, ShoutInk, channels, ScreenChatChannels.Shout, scale);
        }

        EndBlock();
        if (bubblesOn != configuration.VideoChatBubbles || channels != configuration.VideoChatBubbleChannels)
        {
            configuration.VideoChatBubbles = bubblesOn;
            configuration.VideoChatBubbleChannels = channels;
            configuration.Save();
        }
    }

    private static int ChannelTile(Rect rect, LocString label, Vector4 tint, int channels, int channel, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var on = (channels & channel) != 0;
        var rounding = Metrics.Radius.Sm * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var fill = on ? Palette.WithAlpha(tint, hovered ? 0.20f : 0.14f) : hovered ? Ink.ChipHover : Ink.ChipFill;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(on ? Palette.WithAlpha(tint, 0.5f) : Ink.ChipStroke), 1f);
        var pad = Metrics.Space.Md * scale;
        var dotCenter = new Vector2(rect.Min.X + pad + 4f * scale, rect.Center.Y);
        drawList.AddCircleFilled(dotCenter, 4f * scale, ImGui.GetColorU32(Palette.WithAlpha(tint, on ? 1f : 0.4f)),
            16);
        var checkSlot = 18f * scale;
        var textLeft = dotCenter.X + 4f * scale + Metrics.Space.Sm * scale;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - labelHeight * 0.5f),
            Typography.FitText(Loc.T(label), rect.Max.X - pad - checkSlot - textLeft, TextStyles.FootnoteEmphasized),
            on ? Ink.TitleInk : Ink.MutedInk, TextStyles.FootnoteEmphasized);
        if (on)
        {
            AppSkin.Icon(drawList, new Vector2(rect.Max.X - pad - checkSlot * 0.5f + 4f * scale, rect.Center.Y),
                IconGlyph.Of(FontAwesomeIcon.Check), tint, 0.5f);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return channels;
        }

        return on ? channels & ~channel : channels | channel;
    }

    private void DrawSoundCard(VideoEngine engine, bool live, float scale)
    {
        Gap(Metrics.Space.Md);
        var spatial = configuration.VideoSpatialAudio;
        var graphHeight = FalloffHeight * scale;
        var rowHeight = StudioRowHeight * scale;
        var spatialOn = FeatureCard("aetherstream.spatial", FontAwesomeIcon.VolumeUp,
            Loc.T(L.AetherStream.SpatialAudio), Loc.T(L.AetherStream.SpatialAudioHint), spatial,
            spatial ? graphHeight + rowHeight : 0f, out var body);
        var range = configuration.VideoSpatialRange;
        var rangeChanged = false;
        if (spatial)
        {
            DrawFalloff(ImGui.GetWindowDrawList(), engine, live,
                new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + graphHeight)), range, scale);
            rangeChanged = StudioSlider(new Rect(new Vector2(body.Min.X, body.Min.Y + graphHeight), body.Max), 5,
                "aetherstream.screen.range", Loc.T(L.AetherStream.SpatialRange), ref range, SpatialVolume.MinRange,
                SpatialVolume.MaxRange, "{0}", 1f, SpatialVolume.DefaultRange, scale);
        }

        EndBlock();
        if (spatialOn != spatial)
        {
            configuration.VideoSpatialAudio = spatialOn;
            configuration.Save();
            video.SpatialAudio = spatialOn;
        }

        if (rangeChanged)
        {
            configuration.VideoSpatialRange = range;
            video.SpatialRange = range;
            SaveSoon();
        }
    }

    private void DrawFalloff(ImDrawListPtr drawList, VideoEngine engine, bool live, Rect graph, float range,
        float scale)
    {
        var baseline = graph.Max.Y - Metrics.Space.Sm * scale;
        var ceiling = graph.Min.Y + FalloffHeadroom * scale;
        var rise = baseline - ceiling;
        var fill = ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.20f));
        var line = ImGui.GetColorU32(Ink.AccentLink);
        drawList.AddLine(new Vector2(graph.Min.X, baseline), new Vector2(graph.Max.X, baseline),
            ImGui.GetColorU32(Ink.Hairline), 1f);
        var previous = new Vector2(graph.Min.X, ceiling);
        for (var sample = 1; sample <= FalloffSamples; sample++)
        {
            var share = (float)sample / FalloffSamples;
            var point = new Vector2(graph.Min.X + graph.Width * share,
                baseline - rise * SpatialVolume.Gain(SpatialVolume.MaxRange * share, range));
            if (previous.Y < baseline || point.Y < baseline)
            {
                drawList.AddQuadFilled(previous, point, new Vector2(point.X, baseline),
                    new Vector2(previous.X, baseline), fill);
                drawList.AddLine(previous, point, line, 2f * scale);
            }

            previous = point;
        }

        if (!live || Plugin.ObjectTable.LocalPlayer is not { } player)
        {
            return;
        }

        var distance = Vector3.Distance(player.Position, engine.ScreenPose.Position);
        var gain = SpatialVolume.Gain(distance, range);
        var marker = new Vector2(
            graph.Min.X + graph.Width * MathF.Min(distance, SpatialVolume.MaxRange) / SpatialVolume.MaxRange,
            baseline - rise * gain);
        drawList.AddLine(marker, new Vector2(marker.X, baseline),
            ImGui.GetColorU32(Palette.WithAlpha(WhiteInk, 0.35f)), 1f * scale);
        drawList.AddCircleFilled(marker, 4.5f * scale, ImGui.GetColorU32(WhiteInk), 16);
        drawList.AddCircle(marker, 4.5f * scale, ImGui.GetColorU32(ui.Accent), 16, 2f * scale);
        var label = volumeHereText.Format("{0}%", (int)MathF.Round(gain * 100f));
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var labelCenterX = Math.Clamp(marker.X, graph.Min.X + labelSize.X * 0.5f, graph.Max.X - labelSize.X * 0.5f);
        Typography.DrawCentered(drawList,
            new Vector2(labelCenterX, marker.Y - 8f * scale - labelSize.Y * 0.5f), label, Ink.TitleInk,
            TextStyles.FootnoteEmphasized);
    }
}

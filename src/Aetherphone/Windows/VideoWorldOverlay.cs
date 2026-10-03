using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows;

internal sealed class VideoWorldOverlay
{
    private const ImGuiWindowFlags GripFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoBringToFrontOnFocus;

    private const float ArmLength = 0.7f;
    private const float KnobRadius = 11f;
    private const float KnobHitRadius = 16f;
    private const float ArmThickness = 3f;
    private const float OutlineThickness = 2f;
    private const float TurnPerPixel = 0.008f;
    private const float SizePerPixel = 0.006f;
    private const float DonePillWidth = 96f;
    private const float DonePillHeight = 34f;
    private const float DonePillGap = 22f;
    private const float DonePillMargin = 16f;
    private const float DonePillFallbackLift = 140f;
    private const float ViewerEyeHeight = 1.4f;

    private static readonly Vector4 RightInk = new(1f, 0.40f, 0.40f, 1f);
    private static readonly Vector4 UpInk = new(0.42f, 0.90f, 0.52f, 1f);
    private static readonly Vector4 TowardInk = new(0.42f, 0.66f, 1f, 1f);
    private static readonly Vector4 TurnInk = new(1f, 0.80f, 0.36f, 1f);
    private static readonly Vector4 SizeInk = new(0.86f, 0.58f, 1f, 1f);
    private static readonly Vector4 OutlineInk = new(1f, 1f, 1f, 0.55f);
    private static readonly Vector4 KnobRing = new(1f, 1f, 1f, 0.95f);
    private static readonly Vector4 DoneFill = new(0.07f, 0.07f, 0.09f, 0.88f);
    private static readonly Vector4 DoneInk = new(1f, 1f, 1f, 1f);

    private readonly VideoSuite suite;
    private readonly Configuration configuration;

    internal VideoWorldOverlay(VideoSuite suite, Configuration configuration)
    {
        this.suite = suite;
        this.configuration = configuration;
    }

    public void Draw()
    {
        var engine = suite.Screen.Engine;
        if (!engine.IsActive || !engine.ScreenVisible || Plugin.ObjectTable.LocalPlayer is not { } localPlayer)
        {
            return;
        }

        var pose = engine.ScreenPose;
        var projected = TryProjectStage(pose, engine.ScreenCurveDepth, out var corners, out var stage);
        if (!projected && !suite.PlacingScreen)
        {
            return;
        }

        var scale = UiScale.Global;
        var drawList = ImGui.GetBackgroundDrawList();
        var eye = localPlayer.Position + new Vector3(0f, ViewerEyeHeight, 0f);
        var facesViewer = Vector3.Dot(ScreenGeometry.Facing(pose), eye - pose.Position) > 0f;
        if (projected && facesViewer)
        {
            if (configuration.VideoChatBubbles)
            {
                VideoStageOverlay.DrawBubbles(drawList, stage, suite.ChatFeed, scale);
            }

            VideoStageOverlay.DrawReactions(drawList, stage, suite.WatchAlong.Reactions, scale);
        }

        if (suite.PlacingScreen)
        {
            DrawPlacement(drawList, engine, pose, projected, corners, stage, scale);
        }
    }

    private static bool TryProjectStage(in ScreenPose pose, float curveDepth, out StageCorners corners,
        out Rect stage)
    {
        corners = default;
        stage = default;
        if (!Plugin.GameGui.WorldToScreen(ScreenGeometry.Corner(pose, -1f, 1f, curveDepth), out var topLeft, out _)
            || !Plugin.GameGui.WorldToScreen(ScreenGeometry.Corner(pose, 1f, 1f, curveDepth), out var topRight,
                out _)
            || !Plugin.GameGui.WorldToScreen(ScreenGeometry.Corner(pose, 1f, -1f, curveDepth), out var bottomRight,
                out _)
            || !Plugin.GameGui.WorldToScreen(ScreenGeometry.Corner(pose, -1f, -1f, curveDepth), out var bottomLeft,
                out _))
        {
            return false;
        }

        corners = new StageCorners(topLeft, topRight, bottomRight, bottomLeft);
        var min = Vector2.Min(Vector2.Min(topLeft, topRight), Vector2.Min(bottomRight, bottomLeft));
        var max = Vector2.Max(Vector2.Max(topLeft, topRight), Vector2.Max(bottomRight, bottomLeft));
        stage = new Rect(min, max);
        return true;
    }

    private void DrawPlacement(ImDrawListPtr drawList, VideoEngine engine, in ScreenPose pose, bool projected,
        in StageCorners corners, Rect stage, float scale)
    {
        var viewport = ImGui.GetMainViewport();
        var bounds = new Rect(viewport.Pos, viewport.Pos + viewport.Size);
        DrawDone(drawList, projected, stage, bounds, scale);
        if (projected)
        {
            var outline = ImGui.GetColorU32(OutlineInk);
            var thickness = OutlineThickness * scale;
            drawList.AddLine(corners.TopLeft, corners.TopRight, outline, thickness);
            drawList.AddLine(corners.TopRight, corners.BottomRight, outline, thickness);
            drawList.AddLine(corners.BottomRight, corners.BottomLeft, outline, thickness);
            drawList.AddLine(corners.BottomLeft, corners.TopLeft, outline, thickness);
        }

        var pointerDelta = ImGui.GetIO().MouseDelta;
        if (projected && DragKnob(drawList, "##aepScreenTurn", corners.TopRight, TurnInk,
                Loc.T(L.AetherStream.PlaceHandleTurn), bounds, scale))
        {
            engine.SetScreenTransform(pose.Position, pose.Yaw + pointerDelta.X * TurnPerPixel, pose.Pitch, pose.Roll,
                pose.Scale);
        }

        if (projected && DragKnob(drawList, "##aepScreenSize", corners.BottomRight, SizeInk,
                Loc.T(L.AetherStream.PlaceHandleSize), bounds, scale))
        {
            engine.SetScreenTransform(pose.Position, pose.Yaw, pose.Pitch, pose.Roll,
                pose.Scale * (1f + pointerDelta.X * SizePerPixel));
        }

        if (!Plugin.GameGui.WorldToScreen(pose.Position, out var center, out _))
        {
            return;
        }

        var arm = ArmLength * MathF.Max(1f, pose.Scale * 0.5f);

        if (DragArm(drawList, "##aepScreenRight", center, pose.Position + ScreenGeometry.Right(pose) * arm, RightInk,
                Loc.T(L.AetherStream.PlaceHandleSide), bounds, scale, out var rightTip))
        {
            engine.NudgeScreen(ScreenGeometry.AlongAxis(pointerDelta, center, rightTip, arm), 0f, 0f);
        }

        if (DragArm(drawList, "##aepScreenUp", center, pose.Position + ScreenGeometry.Up(pose) * arm, UpInk,
                Loc.T(L.AetherStream.PlaceHandleHeight), bounds, scale, out var upTip))
        {
            engine.NudgeScreen(0f, ScreenGeometry.AlongAxis(pointerDelta, center, upTip, arm), 0f);
        }

        if (DragArm(drawList, "##aepScreenToward", center, pose.Position + ScreenGeometry.Facing(pose) * arm,
                TowardInk, Loc.T(L.AetherStream.PlaceHandleDepth), bounds, scale, out var towardTip))
        {
            engine.NudgeScreen(0f, 0f, ScreenGeometry.AlongAxis(pointerDelta, center, towardTip, arm));
        }

    }

    private static bool DragArm(ImDrawListPtr drawList, string id, Vector2 center, Vector3 worldTip, Vector4 ink,
        string tooltip, in Rect bounds, float scale, out Vector2 tip)
    {
        if (!Plugin.GameGui.WorldToScreen(worldTip, out tip, out _))
        {
            return false;
        }

        drawList.AddLine(center, tip, ImGui.GetColorU32(ink), ArmThickness * scale);
        return DragKnob(drawList, id, tip, ink, tooltip, bounds, scale);
    }

    private static bool DragKnob(ImDrawListPtr drawList, string id, Vector2 center, Vector4 ink, string tooltip,
        in Rect bounds, float scale)
    {
        var hitRadius = KnobHitRadius * scale;
        if (center.X - hitRadius < bounds.Min.X || center.Y - hitRadius < bounds.Min.Y
            || center.X + hitRadius > bounds.Max.X || center.Y + hitRadius > bounds.Max.Y)
        {
            return false;
        }

        var size = new Vector2(hitRadius * 2f, hitRadius * 2f);
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(center - new Vector2(hitRadius, hitRadius));
        ImGui.SetNextWindowSize(size);
        bool hovered;
        bool active;
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowMinSize, Vector2.One))
        {
            ImGui.Begin(id, GripFlags);
            ImGui.InvisibleButton("##grip", size);
            hovered = ImGui.IsItemHovered();
            active = ImGui.IsItemActive();
            ImGui.End();
        }

        var radius = KnobRadius * scale * (active ? 1.2f : hovered ? 1.1f : 1f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(KnobRing), 32, (hovered || active ? 2.4f : 1.4f) * scale);
        if (hovered || active)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (hovered && !active)
        {
            ImGui.SetTooltip(tooltip);
        }

        return active;
    }

    private void DrawDone(ImDrawListPtr drawList, bool projected, Rect stage, in Rect bounds, float scale)
    {
        var size = new Vector2(DonePillWidth * scale, DonePillHeight * scale);
        var margin = new Vector2(DonePillMargin * scale, DonePillMargin * scale);
        var anchor = projected
            ? new Vector2(stage.Center.X - size.X * 0.5f, stage.Max.Y + DonePillGap * scale)
            : new Vector2(bounds.Center.X - size.X * 0.5f, bounds.Max.Y - size.Y - DonePillFallbackLift * scale);
        var min = Vector2.Clamp(anchor, bounds.Min + margin, bounds.Max - size - margin);
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(min);
        ImGui.SetNextWindowSize(size);
        bool hovered;
        bool clicked;
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowMinSize, Vector2.One))
        {
            ImGui.Begin("##aepScreenDone", GripFlags);
            clicked = ImGui.InvisibleButton("##done", size);
            hovered = ImGui.IsItemHovered();
            ImGui.End();
        }

        var fill = hovered ? new Vector4(DoneFill.X + 0.08f, DoneFill.Y + 0.08f, DoneFill.Z + 0.08f, DoneFill.W) : DoneFill;
        Squircle.Fill(drawList, min, min + size, size.Y * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, min + size, size.Y * 0.5f, ImGui.GetColorU32(OutlineInk), 1f);
        Typography.DrawCentered(drawList, min + size * 0.5f, Loc.T(L.AetherStream.PlaceDone), DoneInk,
            TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (clicked)
        {
            suite.PlacingScreen = false;
        }
    }

    private readonly record struct StageCorners(Vector2 TopLeft, Vector2 TopRight, Vector2 BottomRight,
        Vector2 BottomLeft);
}

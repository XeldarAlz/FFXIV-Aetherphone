using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Camera;

internal sealed partial class CameraApp
{
    private const ImGuiWindowFlags CropWindowFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollWithMouse;

    private const float CropGripHalf = 16f;
    private const float CropMinimumSide = 96f;
    private const float CropActionHeight = 34f;
    private const float CropActionPadding = 18f;
    private const float CropActionGap = 10f;
    private const float CropActionMargin = 14f;
    private const int CropStaleFrames = 2;
    private static readonly Rect DefaultCropShare = new(new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f));
    private static readonly string[] CropCornerIds = { "##nw", "##ne", "##se", "##sw" };

    private Rect cropShare = DefaultCropShare;
    private Rect cropPressBox;
    private Vector2 cropPressMouse;
    private bool cropping;
    private int drawnFrame;

    private void BeginCrop()
    {
        if (cropping)
        {
            return;
        }

        cropping = true;
        PhoneAside.Request(Id);
        UiFeedback.Play(UiSound.Tap);
        Plugin.PluginInterface.UiBuilder.Draw += DrawCrop;
    }

    private void StopCrop()
    {
        if (!cropping)
        {
            return;
        }

        cropping = false;
        Plugin.PluginInterface.UiBuilder.Draw -= DrawCrop;
    }

    private void CancelCrop()
    {
        StopCrop();
        PhoneAside.Release(Id);
        UiFeedback.Play(UiSound.ToggleOff);
    }

    private void DrawCrop()
    {
        if (ImGui.GetFrameCount() - drawnFrame > CropStaleFrames)
        {
            CancelCrop();
            return;
        }

        PhoneAside.Request(Id);
        var scale = UiScale.Global;
        var whole = WholeScreen();
        var box = FromShare(cropShare, whole);
        if (DragCrop(box, whole, scale, out var dragged))
        {
            box = dragged;
            cropShare = ToShare(box, whole);
        }

        CameraChrome.CropFrame(ImGui.GetBackgroundDrawList(), whole, box, configuration.CameraGrid, scale);
        DrawCropActions(whole, box, scale);
    }

    private bool DragCrop(Rect box, Rect whole, float scale, out Rect dragged)
    {
        var extent = new Vector2(CropGripHalf * scale, CropGripHalf * scale);
        dragged = box;
        var moved = false;
        BeginCropWindow("##camera.crop", box.Min - extent, box.Size + extent * 2f);
        for (var corner = 0; corner < CropCornerIds.Length; corner++)
        {
            var point = CropCorner(box, corner);
            var cursor = corner % 2 == 0 ? ImGuiMouseCursor.ResizeNwse : ImGuiMouseCursor.ResizeNesw;
            if (CropGrip(CropCornerIds[corner], new Rect(point - extent, point + extent), cursor, box))
            {
                dragged = Resize(cropPressBox, ImGui.GetMousePos() - cropPressMouse, corner, whole,
                    CropMinimumSide * scale);
                moved = true;
            }
        }

        if (CropGrip("##body", box, ImGuiMouseCursor.ResizeAll, box))
        {
            dragged = Move(cropPressBox, ImGui.GetMousePos() - cropPressMouse, whole);
            moved = true;
        }

        EndCropWindow();
        return moved;
    }

    private bool CropGrip(string id, Rect hit, ImGuiMouseCursor cursor, Rect box)
    {
        ImGui.SetCursorScreenPos(hit.Min);
        ImGui.InvisibleButton(id, hit.Size);
        if (ImGui.IsItemActivated())
        {
            cropPressBox = box;
            cropPressMouse = ImGui.GetMousePos();
        }

        var active = ImGui.IsItemActive();
        if (active || ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(cursor);
        }

        return active;
    }

    private void DrawCropActions(Rect whole, Rect box, float scale)
    {
        var cancel = Loc.T(L.Common.Cancel);
        var take = Loc.T(L.Camera.CropTake);
        var padding = CropActionPadding * scale * 2f;
        var cancelWidth = Typography.Measure(cancel, TextStyles.SubheadlineEmphasized).X + padding;
        var takeWidth = Typography.Measure(take, TextStyles.SubheadlineEmphasized).X + padding;
        var gap = CropActionGap * scale;
        var margin = CropActionMargin * scale;
        var size = new Vector2(cancelWidth + gap + takeWidth, CropActionHeight * scale);
        var top = box.Max.Y + margin;
        if (top + size.Y > whole.Max.Y - margin)
        {
            top = box.Min.Y - margin - size.Y;
        }

        if (top < whole.Min.Y + margin)
        {
            top = box.Max.Y - margin - size.Y;
        }

        var centered = box.Center.X - size.X * 0.5f;
        var min = new Vector2(MathF.Max(whole.Min.X + margin, MathF.Min(centered, whole.Max.X - margin - size.X)), top);
        BeginCropWindow("##camera.cropActions", min, size);
        var drawList = ImGui.GetWindowDrawList();
        var cancelRect = new Rect(min, min + new Vector2(cancelWidth, size.Y));
        var takeRect = new Rect(min + new Vector2(cancelWidth + gap, 0f), min + size);
        var cancelled = CropAction(drawList, "##cancel", cancelRect, cancel, false);
        var taken = CropAction(drawList, "##take", takeRect, take, true);
        EndCropWindow();
        if (cancelled)
        {
            CancelCrop();
            return;
        }

        if (taken)
        {
            Shoot(box);
        }
    }

    private static bool CropAction(ImDrawListPtr drawList, string id, Rect pill, string label, bool primary)
    {
        ImGui.SetCursorScreenPos(pill.Min);
        var clicked = ImGui.InvisibleButton(id, pill.Size);
        var hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        CameraChrome.CropAction(drawList, pill, label, primary, hovered);
        return clicked;
    }

    private static void BeginCropWindow(string id, Vector2 position, Vector2 size)
    {
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(position);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.Begin(id, CropWindowFlags);
        ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindowRead());
    }

    private static void EndCropWindow()
    {
        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    private Rect CropBox() => FromShare(cropShare, WholeScreen());

    private static Rect FromShare(Rect share, Rect whole) =>
        new(whole.Min + share.Min * whole.Size, whole.Min + share.Max * whole.Size);

    private static Rect ToShare(Rect box, Rect whole) =>
        new((box.Min - whole.Min) / whole.Size, (box.Max - whole.Min) / whole.Size);

    private static Vector2 CropCorner(Rect box, int corner) => corner switch
    {
        0 => box.Min,
        1 => new Vector2(box.Max.X, box.Min.Y),
        2 => box.Max,
        _ => new Vector2(box.Min.X, box.Max.Y),
    };

    private static Rect Move(Rect press, Vector2 delta, Rect whole)
    {
        var min = Vector2.Min(Vector2.Max(press.Min + delta, whole.Min), whole.Max - press.Size);
        return new Rect(min, min + press.Size);
    }

    private static Rect Resize(Rect press, Vector2 delta, int corner, Rect whole, float minimum)
    {
        var min = press.Min;
        var max = press.Max;
        if (corner is 0 or 3)
        {
            min.X = MathF.Max(whole.Min.X, MathF.Min(press.Min.X + delta.X, max.X - minimum));
        }
        else
        {
            max.X = MathF.Min(whole.Max.X, MathF.Max(press.Max.X + delta.X, min.X + minimum));
        }

        if (corner < 2)
        {
            min.Y = MathF.Max(whole.Min.Y, MathF.Min(press.Min.Y + delta.Y, max.Y - minimum));
        }
        else
        {
            max.Y = MathF.Min(whole.Max.Y, MathF.Max(press.Max.Y + delta.Y, min.Y + minimum));
        }

        return new Rect(min, max);
    }
}

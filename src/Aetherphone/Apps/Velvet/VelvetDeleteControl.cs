using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetDeleteControl
{
    private readonly VelvetStore store;

    private string? targetId;
    private string status = string.Empty;
    private volatile bool submitting;

    public VelvetDeleteControl(VelvetStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
        targetId = null;
        status = string.Empty;
    }

    public bool Toggle(VelvetUi ui, Vector2 center, float radius, string postId, string tooltip = "")
    {
        var active = targetId == postId;
        var background = Palette.WithAlpha(ui.Theme.Danger, active ? 0.32f : 0.16f);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Trash.ToIconString(), ui.Theme.Danger, background, 0.9f, tooltip))
        {
            if (active)
            {
                Reset();
                active = false;
            }
            else
            {
                targetId = postId;
                status = string.Empty;
                active = true;
            }
        }

        return active;
    }

    public void Composer(VelvetUi ui, float left, float width, Action onDeleted)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();

        using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.BodyInk))
        {
            ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
            ImGui.PushTextWrapPos(left + width);
            ImGui.TextWrapped(Loc.T(L.Velvet.DeleteConfirmMessage));
            ImGui.PopTextWrapPos();
        }

        var rowY = ImGui.GetCursorScreenPos().Y + 6f * scale;
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;

        var cancelRect = new Rect(new Vector2(left + width - buttonWidth * 2f - 8f * scale, rowY), new Vector2(left + width - buttonWidth - 8f * scale, rowY + buttonHeight));
        if (ui.PillButton(cancelRect, Loc.T(L.Velvet.DeleteCancel), false) && !submitting)
        {
            Reset();
        }

        var deleteRect = new Rect(new Vector2(left + width - buttonWidth, rowY), new Vector2(left + width, rowY + buttonHeight));
        var canSubmit = !submitting;
        if (DrawDangerPillButton(ui, deleteRect, submitting ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.DeleteConfirm)) && canSubmit)
        {
            Submit(onDeleted);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, rowY + buttonHeight + 2f * scale));
        if (status.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.MutedInk))
            {
                ImGui.TextUnformatted(status);
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
        }
    }

    private static bool DrawDangerPillButton(VelvetUi ui, Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = hovered ? Palette.Mix(ui.Theme.Danger, VelvetUi.TitleInk, 0.12f) : ui.Theme.Danger;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));

        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, new Vector4(1f, 1f, 1f, 1f), 0.9f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void Submit(Action onDeleted)
    {
        if (submitting || targetId is not { } postId)
        {
            return;
        }

        submitting = true;
        store.DeletePost(postId, ok =>
        {
            submitting = false;
            if (ok)
            {
                Reset();
                onDeleted();
            }
            else
            {
                status = Loc.T(L.Velvet.DeleteFailed);
            }
        });
    }
}

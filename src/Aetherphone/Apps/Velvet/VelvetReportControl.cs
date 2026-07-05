using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetReportControl
{
    private const int ReasonMaxLength = 200;
    private readonly VelvetStore store;
    private string? targetType;
    private string? targetId;
    private string reasonDraft = string.Empty;
    private string status = string.Empty;
    private volatile bool submitting;

    public VelvetReportControl(VelvetStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
        targetType = null;
        targetId = null;
        reasonDraft = string.Empty;
        status = string.Empty;
    }

    public bool Toggle(VelvetUi ui, Vector2 center, float radius, string type, string id, string tooltip = "")
    {
        var active = targetType == type && targetId == id;
        var background = Palette.WithAlpha(ui.Theme.Danger, active ? 0.32f : 0.16f);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Flag.ToIconString(), ui.Theme.Danger, background, 0.9f,
                tooltip))
        {
            if (active)
            {
                Reset();
                active = false;
            }
            else
            {
                targetType = type;
                targetId = id;
                reasonDraft = string.Empty;
                status = string.Empty;
                active = true;
            }
        }

        return active;
    }

    public void Composer(VelvetUi ui, float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
        ImGui.SetNextItemWidth(width - buttonWidth - 8f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(1f, 1f, 1f, 0.10f)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.Theme.TextStrong))
        {
            ImGui.InputTextWithHint("##velvetReportReason", Loc.T(L.Velvet.ReportReasonHint), ref reasonDraft,
                ReasonMaxLength);
        }

        var buttonRect = new Rect(new Vector2(left + width - buttonWidth, origin.Y - 2f * scale),
            new Vector2(left + width, origin.Y - 2f * scale + buttonHeight));
        var canSubmit = !submitting;
        if (ui.PillButton(buttonRect, submitting ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.ReportSubmit), canSubmit) &&
            canSubmit)
        {
            Submit();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y + buttonHeight + 2f * scale));
        if (status.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.MutedInk))
            {
                ImGui.TextUnformatted(status);
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
        }
    }

    private void Submit()
    {
        if (submitting || targetType is not { } type || targetId is not { } id)
        {
            return;
        }

        submitting = true;
        var reason = reasonDraft.Trim();
        store.Report(type, id, reason.Length > 0 ? reason : null, ok =>
        {
            submitting = false;
            status = Loc.T(ok ? L.Velvet.ReportSent : L.Velvet.ReportFailed);
            if (ok)
            {
                targetType = null;
                targetId = null;
            }
        });
    }
}

using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class MessageReportControl
{
    private const int ReasonMaxLength = 200;
    private string? messageId;
    private string reasonDraft = string.Empty;
    private string status = string.Empty;
    private volatile bool submitting;

    public bool Armed => messageId is not null;

    public void Arm(string id)
    {
        if (messageId == id)
        {
            return;
        }

        messageId = id;
        reasonDraft = string.Empty;
        status = string.Empty;
    }

    public void Reset()
    {
        messageId = null;
        reasonDraft = string.Empty;
        status = string.Empty;
    }

    public float Height(float scale)
    {
        return Armed ? 96f * scale : 0f;
    }

    public void Draw(Rect area, PhoneTheme theme, Vector4 mutedInk,
        Action<string, string?, Action<bool>> submit)
    {
        if (messageId is null)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.10f)));
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

        var padding = 10f * scale;
        var cursor = new Vector2(area.Min.X + padding, area.Min.Y + 8f * scale);
        var wrapWidth = area.Max.X - area.Min.X - padding * 2f;
        ImGui.SetCursorScreenPos(cursor);
        using (ImRaii.PushColor(ImGuiCol.Text, mutedInk))
        {
            ImGui.PushTextWrapPos(cursor.X + wrapWidth);
            ImGui.TextWrapped(status.Length > 0 ? status : Loc.T(L.Encryption.ReportDisclosure));
            ImGui.PopTextWrapPos();
        }

        var rowY = area.Max.Y - 36f * scale;
        var buttonWidth = 76f * scale;
        var buttonHeight = 26f * scale;
        var inputWidth = wrapWidth - buttonWidth * 2f - 12f * scale;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + padding, rowY));
        ImGui.SetNextItemWidth(inputWidth);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(1f, 1f, 1f, 0.10f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextWithHint("##reportReason", Loc.T(L.Velvet.ReportReasonHint), ref reasonDraft, ReasonMaxLength);
        }

        ImGui.SameLine(0f, 6f * scale);
        using (ImRaii.PushColor(ImGuiCol.Button, Palette.WithAlpha(theme.TextStrong, 0f))
                   .Push(ImGuiCol.ButtonHovered, Palette.WithAlpha(theme.TextStrong, 0.08f))
                   .Push(ImGuiCol.ButtonActive, Palette.WithAlpha(theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.Text, mutedInk))
        {
            if (ImGui.Button(Loc.T(L.Common.Cancel), new Vector2(buttonWidth, buttonHeight)))
            {
                Reset();
                return;
            }
        }

        ImGui.SameLine(0f, 6f * scale);
        var canSubmit = !submitting;
        using (ImRaii.PushColor(ImGuiCol.Button, theme.Danger)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.Danger, theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.ButtonActive, Palette.Mix(theme.Danger, new Vector4(0f, 0f, 0f, 1f), 0.18f))
                   .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            if (ImGui.Button(submitting ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.ReportSubmit),
                    new Vector2(buttonWidth, buttonHeight)) && canSubmit)
            {
                Submit(submit);
            }
        }
    }

    private void Submit(Action<string, string?, Action<bool>> submit)
    {
        if (submitting || messageId is not { } id)
        {
            return;
        }

        submitting = true;
        var reason = reasonDraft.Trim();
        submit(id, reason.Length > 0 ? reason : null, ok =>
        {
            submitting = false;
            status = Loc.T(ok ? L.Velvet.ReportSent : L.Velvet.ReportFailed);
            if (ok)
            {
                messageId = null;
                reasonDraft = string.Empty;
            }
        });
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NamePage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Account.NameTitle);

    public string Summary =>
        session.CurrentUser is { Handle.Length: > 0 } user ? $"@{user.Handle}" : string.Empty;

    public FontAwesomeIcon Icon => FontAwesomeIcon.UserTag;
    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);
    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly ISettingsNavigator navigator;
    private readonly CancellationTokenSource cancellation = new();
    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editStatus = string.Empty;
    private string? loadedFor;
    private volatile bool busy;
    private volatile int outcome;

    public NamePage(AethernetSession session, AccountClient account, ISettingsNavigator navigator)
    {
        this.session = session;
        this.account = account;
        this.navigator = navigator;
    }

    public void ResetEdit()
    {
        loadedFor = null;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var user = session.CurrentUser;
        if (!session.IsSignedIn || user is null)
        {
            account.EnsureCurrentUser();
            using (AppSurface.Begin(body))
            {
                Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), theme.TextMuted);
            }

            return;
        }

        if (outcome == 1)
        {
            outcome = 0;
            navigator.Back();
            return;
        }

        if (outcome == 2)
        {
            outcome = 0;
            editStatus = Loc.T(L.Account.HandleTaken);
        }

        if (outcome == 3)
        {
            outcome = 0;
            editStatus = Loc.T(L.Account.CannotReach);
        }

        if (loadedFor != user.Id)
        {
            loadedFor = user.Id;
            editDisplay = user.DisplayName;
            editHandle = user.Handle;
            editStatus = string.Empty;
        }

        using (AppSurface.Begin(body))
        {
            var scale = ImGuiHelpers.GlobalScale;
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawField(theme, Loc.T(L.Account.DisplayNameLabel), "##accountDisplayName", ref editDisplay,
                SocialProfilePages.DisplayNameMax);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
            DrawHandleField(theme);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            var canSave = !busy && editDisplay.Trim().Length > 0 && SocialProfilePages.IsHandleValid(editHandle);
            if (PrimaryButton(busy ? Loc.T(L.Account.Saving) : Loc.T(L.Account.Save), theme, canSave))
            {
                Save();
            }

            if (editStatus.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    Typography.Wrapped(editStatus);
                }
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            SettingsSection.Hint(Loc.T(L.Account.NameHint), theme);
        }
    }

    private void DrawField(PhoneTheme theme, string label, string id, ref string value, int maxLength)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Plain(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)).Push(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputText(id, ref value, maxLength);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawHandleField(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Plain(Loc.T(L.Account.HandleLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        Typography.Draw(new Vector2(origin.X + 12f * scale, origin.Y + height * 0.5f - 8f * scale), "@",
            theme.TextMuted, 1f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 26f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 38f * scale);
        var valid = SocialProfilePages.IsHandleValid(editHandle);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.Text, valid ? theme.TextStrong : theme.Danger))
        {
            if (ImGui.InputText("##accountHandle", ref editHandle, SocialProfilePages.HandleMax,
                    ImGuiInputTextFlags.CharsNoBlank))
            {
                editHandle = editHandle.ToLowerInvariant();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale),
            Typography.FitText(Loc.T(L.Account.HandleRules), width - 4f * scale, 0.78f, FontWeight.Regular),
            theme.TextMuted, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
    }

    private void Save()
    {
        if (busy || session.CurrentUser is null)
        {
            return;
        }

        if (editDisplay.Trim().Length == 0 || !SocialProfilePages.IsHandleValid(editHandle))
        {
            editStatus = Loc.T(L.Account.HandleRules);
            return;
        }

        busy = true;
        editStatus = string.Empty;
        var request = new UpdateProfileRequest(editDisplay.Trim(), editHandle.Trim(), null);
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var updated = await account.UpdateProfileAsync(request, token).ConfigureAwait(false);
                busy = false;
                if (updated is not null)
                {
                    session.SetUser(updated);
                    outcome = 1;
                    return;
                }

                outcome = 2;
            }
            catch (OperationCanceledException)
            {
                busy = false;
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet name update failed: {exception.Message}");
                busy = false;
                outcome = 3;
            }
        });
    }

    private static bool PrimaryButton(string label, PhoneTheme theme, bool enabled)
    {
        var accent = enabled ? theme.Accent : Palette.WithAlpha(theme.Accent, 0.4f);
        using (ImRaii.PushColor(ImGuiCol.Button, accent)
                   .Push(ImGuiCol.ButtonHovered, enabled ? Palette.Mix(theme.Accent, theme.TextStrong, 0.14f) : accent)
                   .Push(ImGuiCol.ButtonActive, accent)
                   .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, enabled ? 1f : 0.72f)))
        {
            var clicked = ImGui.Button(label, new Vector2(-1f, 38f * ImGuiHelpers.GlobalScale));
            return clicked && enabled;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

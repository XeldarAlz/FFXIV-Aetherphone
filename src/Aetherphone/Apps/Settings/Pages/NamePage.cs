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

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NamePage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Account.NameTitle);

    public string Summary =>
        session.CurrentUser is { Handle.Length: > 0 } user ? $"@{user.Handle}" : string.Empty;

    public FontAwesomeIcon Icon => FontAwesomeIcon.UserTag;
    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);
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
            SettingsForm.Gap(Metrics.Space.Xs);
            SettingsSection.Header(Loc.T(L.Account.DisplayNameLabel), theme);
            SettingsForm.TextField("##accountDisplayName", string.Empty, ref editDisplay, theme,
                SocialProfilePages.DisplayNameMax);
            SettingsForm.Gap(Metrics.Space.Lg);
            DrawHandleField(theme);
            SettingsForm.Gap(Metrics.Space.Xl);
            var canSave = !busy && editDisplay.Trim().Length > 0 && SocialProfilePages.IsHandleValid(editHandle);
            if (SettingsForm.Button(busy ? Loc.T(L.Account.Saving) : Loc.T(L.Account.Save), theme,
                    ButtonStyle.Prominent, enabled: canSave))
            {
                Save();
            }

            if (editStatus.Length > 0)
            {
                SettingsForm.Gap(Metrics.Space.Md);
                SettingsForm.Text(editStatus, theme.Danger, TextStyles.Subheadline);
            }

            SettingsForm.Gap(Metrics.Space.Lg);
            SettingsSection.Hint(Loc.T(L.Account.NameHint), theme);
        }
    }

    private void DrawHandleField(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        SettingsSection.Header(Loc.T(L.Account.HandleLabel), theme);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        var drawList = ImGui.GetWindowDrawList();
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        var prefixLeft = origin.X + Metrics.Space.Lg * scale;
        var prefixSize = Typography.Measure("@", TextStyles.Body);
        Typography.Draw(drawList, new Vector2(prefixLeft, field.Center.Y - prefixSize.Y * 0.5f), "@", theme.TextMuted,
            TextStyles.Body);
        var inputLeft = prefixLeft + prefixSize.X + Metrics.Space.Xxs * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, field.Max.X - Metrics.Space.Lg * scale - inputLeft));
        var valid = SocialProfilePages.IsHandleValid(editHandle);
        using (UiScope.PushColor(ImGuiCol.FrameBg, Transparent)
                   .Push(ImGuiCol.FrameBgHovered, Transparent)
                   .Push(ImGuiCol.FrameBgActive, Transparent)
                   .Push(ImGuiCol.Text, valid ? theme.TextStrong : theme.Danger))
        {
            if (ImGui.InputText("##accountHandle", ref editHandle, SocialProfilePages.HandleMax,
                    ImGuiInputTextFlags.CharsNoBlank))
            {
                editHandle = editHandle.ToLowerInvariant();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, field.Height));
        SettingsForm.Gap(Metrics.Space.Xxs);
        SettingsSection.Hint(Loc.T(L.Account.HandleRules), theme);
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
                AepLog.Warning(exception, "Aethernet name update failed");
                busy = false;
                outcome = 3;
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

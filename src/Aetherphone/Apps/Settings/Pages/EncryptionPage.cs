using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class EncryptionPage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Encryption.Title);

    public string Summary => vault.State switch
    {
        KeyVaultState.Unlocked => Loc.T(L.Encryption.StateActive),
        KeyVaultState.Provisioning => Loc.T(L.Encryption.StateSettingUp),
        _ => Loc.T(L.Encryption.StateUnavailable),
    };

    public FontAwesomeIcon Icon => FontAwesomeIcon.Lock;
    public Vector4 Tint => new(0.38f, 0.66f, 0.42f, 1f);

    private readonly AethernetSession session;
    private readonly KeyVault vault;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile bool refreshRequested;

    public EncryptionPage(AethernetSession session, KeyVault vault)
    {
        this.session = session;
        this.vault = vault;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            EnsureRefreshed();
            switch (vault.State)
            {
                case KeyVaultState.Unavailable:
                    DrawUnavailable(theme);
                    break;
                case KeyVaultState.Provisioning:
                    DrawProvisioning(theme);
                    break;
                default:
                    DrawActive(theme);
                    break;
            }

            DrawStatus(theme);
        }
    }

    private void EnsureRefreshed()
    {
        if (refreshRequested || !session.IsSignedIn || vault.IsRefreshing)
        {
            if (!session.IsSignedIn)
            {
                refreshRequested = false;
            }

            return;
        }

        refreshRequested = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await vault.RefreshAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Encryption key refresh failed: {exception.Message}");
            }
        });
    }

    private void DrawUnavailable(PhoneTheme theme)
    {
        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.NotSignedIn));
        }
    }

    private void DrawProvisioning(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.Intro));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            Typography.Wrapped(Loc.T(L.Encryption.SettingUp));
        }
    }

    private void DrawActive(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.Intro));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            Typography.Wrapped(Loc.T(L.Encryption.ActiveHint));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            Typography.Wrapped(Loc.T(L.Encryption.NewDeviceHint));
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            Typography.Plain(Loc.T(L.Encryption.KeyVersion, vault.KeyVersion));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (Button(Loc.T(L.Encryption.ResetButton), theme) && !busy)
        {
            AskReset();
        }
    }

    private void AskReset()
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Encryption.ForgotBody),
            ConfirmLabel = Loc.T(L.Encryption.ForgotConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done =>
            {
                Reset();
                done(true);
            },
        });
    }

    private void Reset()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var succeeded = await vault.ResetAsync(cancellation.Token).ConfigureAwait(false);
                status = succeeded ? string.Empty : Loc.T(L.Encryption.Failed);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Encryption reset failed: {exception.Message}");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void DrawStatus(PhoneTheme theme)
    {
        var message = status;
        if (message.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(message);
        }
    }

    private static bool Button(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.GroupedCard)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.GroupedCard, theme.Accent, 0.35f))
                   .Push(ImGuiCol.ButtonActive, theme.Accent).Push(ImGuiCol.Text, theme.TextStrong))
        {
            return ImGui.Button(label, new Vector2(-1f, 34f * ImGuiHelpers.GlobalScale));
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

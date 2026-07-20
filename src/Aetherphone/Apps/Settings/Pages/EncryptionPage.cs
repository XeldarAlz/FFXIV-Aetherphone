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
        KeyVaultState.Locked => Loc.T(L.Encryption.StateLocked),
        KeyVaultState.Unsupported => Loc.T(L.Encryption.StateUnsupported),
        _ => Loc.T(L.Encryption.StateUnavailable),
    };

    public FontAwesomeIcon Icon => FontAwesomeIcon.Lock;
    public Vector4 Tint => new(0.38f, 0.66f, 0.42f, 1f);

    private readonly AethernetSession session;
    private readonly KeyVault vault;
    private readonly ConfirmService confirm;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile bool refreshRequested;
    private volatile string generatedCode = string.Empty;
    private string codeEntry = string.Empty;

    public EncryptionPage(AethernetSession session, KeyVault vault, ConfirmService confirm)
    {
        this.session = session;
        this.vault = vault;
        this.confirm = confirm;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            EnsureRefreshed();
            if (generatedCode.Length > 0)
            {
                DrawGeneratedCode(theme);
            }
            else
            {
                switch (vault.State)
                {
                    case KeyVaultState.Unavailable:
                        DrawUnavailable(theme);
                        break;
                    case KeyVaultState.Provisioning:
                        DrawProvisioning(theme);
                        break;
                    case KeyVaultState.Unsupported:
                        DrawUnsupported(theme);
                        break;
                    case KeyVaultState.Locked:
                        DrawLocked(theme);
                        break;
                    default:
                        DrawActive(theme);
                        break;
                }
            }

            DrawStatus(theme);
        }
    }

    private void EnsureRefreshed()
    {
        if (refreshRequested || !session.IsSignedIn || session.CurrentUser is null || vault.IsRefreshing)
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

    private void DrawUnsupported(PhoneTheme theme)
    {
        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.UnsupportedBody));
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

    private void DrawLocked(PhoneTheme theme)
    {
        if (vault.RecoveryConfigured)
        {
            DrawLockedRecover(theme);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.LockedBody));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (Button(Loc.T(L.Encryption.NewKeyButton), theme) && !busy)
        {
            AskReset();
        }
    }

    private void DrawLockedRecover(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.LockedRecoverBody));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        DrawCodeInput(theme);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (Button(Loc.T(L.Encryption.RecoveryUnlockButton), theme)
            && !busy && RecoveryKey.Canonicalize(codeEntry).Length > 0)
        {
            BeginRecover();
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (Button(Loc.T(L.Encryption.NewKeyButton), theme) && !busy)
        {
            AskReset();
        }
    }

    private void DrawCodeInput(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Plain(Loc.T(L.Encryption.RecoveryCodeLabel));
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
            ImGui.InputText("##recoveryCode", ref codeEntry, 64);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawGeneratedCode(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Wrapped(Loc.T(L.Encryption.RecoverySaveTitle));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 46f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 10f * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), generatedCode,
            theme.TextStrong, 1.15f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (Button(Loc.T(L.Encryption.RecoveryCopy), theme))
        {
            ImGui.SetClipboardText(generatedCode);
            status = Loc.T(L.Friends.Copied);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.RecoverySaveBody));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (Button(Loc.T(L.Encryption.RecoverySavedButton), theme))
        {
            generatedCode = string.Empty;
            status = string.Empty;
        }
    }

    private void DrawRecoverySection(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Plain(Loc.T(L.Encryption.RecoverySectionTitle));
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(vault.RecoveryConfigured
                ? Loc.T(L.Encryption.RecoveryConfiguredBody)
                : Loc.T(L.Encryption.RecoveryNotSetBody));
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var label = vault.RecoveryConfigured
            ? Loc.T(L.Encryption.RecoveryRegenerateButton)
            : Loc.T(L.Encryption.RecoverySetupButton);
        if (Button(label, theme) && !busy)
        {
            BeginCreateRecoveryCode();
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

        if (vault.LocalCacheUnavailable)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
            {
                Typography.Wrapped(Loc.T(L.Encryption.LocalStoreUnavailable));
            }
        }

        DrawRecoverySection(theme);

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        if (Button(Loc.T(L.Encryption.ResetButton), theme) && !busy)
        {
            AskReset();
        }
    }

    private void AskReset()
    {
        confirm.Ask(new ConfirmRequest
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

    private void BeginCreateRecoveryCode()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var code = await vault.CreateRecoveryCodeAsync(cancellation.Token).ConfigureAwait(false);
                if (code is not null)
                {
                    generatedCode = code;
                    status = string.Empty;
                }
                else
                {
                    status = Loc.T(L.Encryption.Failed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Recovery code setup failed: {exception.Message}");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void BeginRecover()
    {
        var code = codeEntry;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var recovered = await vault.RecoverWithCodeAsync(code, cancellation.Token).ConfigureAwait(false);
                if (recovered)
                {
                    codeEntry = string.Empty;
                    status = string.Empty;
                }
                else
                {
                    status = Loc.T(L.Encryption.RecoveryWrongCode);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Recovery failed: {exception.Message}");
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

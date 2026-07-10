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
    private const int PassphraseMinLength = 8;
    private const int PassphraseMaxLength = 256;

    public string Title => Loc.T(L.Encryption.Title);

    public string Summary => vault.State switch
    {
        KeyVaultState.Unlocked => Loc.T(L.Encryption.StateUnlocked),
        KeyVaultState.Locked => Loc.T(L.Encryption.StateLocked),
        KeyVaultState.NeedsSetup => Loc.T(L.Encryption.StateSetup),
        _ => Loc.T(L.Encryption.StateUnavailable),
    };

    public FontAwesomeIcon Icon => FontAwesomeIcon.Lock;
    public Vector4 Tint => new(0.38f, 0.66f, 0.42f, 1f);

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly KeyVault vault;
    private readonly ConversationKeyStore conversationKeys;
    private readonly CancellationTokenSource cancellation = new();
    private string passphrase = string.Empty;
    private string passphraseConfirm = string.Empty;
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile bool refreshRequested;
    private bool changeMode;
    private bool rekeyMode;

    public EncryptionPage(Configuration configuration, AethernetSession session, KeyVault vault,
        ConversationKeyStore conversationKeys)
    {
        this.configuration = configuration;
        this.session = session;
        this.vault = vault;
        this.conversationKeys = conversationKeys;
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
                case KeyVaultState.NeedsSetup:
                    DrawSetup(theme);
                    break;
                case KeyVaultState.Locked:
                    DrawLocked(theme);
                    break;
                default:
                    DrawUnlocked(theme);
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
            ImGui.TextWrapped(Loc.T(L.Encryption.NotSignedIn));
        }
    }

    private void DrawSetup(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (Plugin.Fonts.Push(1.3f, FontWeight.SemiBold))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(Loc.T(L.Encryption.SetupTitle));
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Encryption.Intro));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            ImGui.TextWrapped(Loc.T(L.Encryption.SetupHint));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        DrawPassphraseInputs();
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (PrimaryButton(Loc.T(L.Encryption.SetupButton), theme) && !busy)
        {
            if (ValidateNewPassphrase())
            {
                RunVaultAction(entered => vault.SetupAsync(entered, cancellation.Token));
            }
        }
    }

    private void DrawLocked(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (rekeyMode)
        {
            DrawRekey(theme, scale);
            return;
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (Plugin.Fonts.Push(1.3f, FontWeight.SemiBold))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(Loc.T(L.Encryption.UnlockTitle));
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Encryption.UnlockHint));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##encPassphrase", Loc.T(L.Encryption.PassphraseHint), ref passphrase,
            PassphraseMaxLength, ImGuiInputTextFlags.Password);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (PrimaryButton(Loc.T(L.Encryption.UnlockButton), theme) && !busy && passphrase.Length > 0)
        {
            RunVaultAction(async entered =>
            {
                var unlocked = await vault.UnlockAsync(entered, cancellation.Token).ConfigureAwait(false);
                if (unlocked)
                {
                    await conversationKeys.HydrateAsync(cancellation.Token).ConfigureAwait(false);
                }
                else
                {
                    status = Loc.T(L.Encryption.WrongPassphrase);
                }

                return unlocked;
            });
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        if (GhostButton(Loc.T(L.Encryption.ForgotButton), theme) && !busy)
        {
            AskRekey();
        }
    }

    private void DrawUnlocked(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Encryption.UnlockedHint));
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            ImGui.TextUnformatted(Loc.T(L.Encryption.KeyVersion, vault.KeyVersion));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var card = GroupCard.Begin(theme, 1);
        var require = SettingsRow.Bool(card.NextRow(), Loc.T(L.Encryption.RequireEachSession),
            configuration.EncryptionRequirePassphraseEachSession, theme);
        card.End();
        if (require != configuration.EncryptionRequirePassphraseEachSession)
        {
            configuration.EncryptionRequirePassphraseEachSession = require;
            if (require)
            {
                configuration.EncryptionKeyCache = string.Empty;
                configuration.EncryptionKeyCacheUserId = string.Empty;
            }

            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (!changeMode)
        {
            if (Button(Loc.T(L.Encryption.ChangeButton), theme) && !busy)
            {
                changeMode = true;
                passphrase = string.Empty;
                passphraseConfirm = string.Empty;
            }
        }
        else
        {
            DrawPassphraseInputs();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            if (PrimaryButton(Loc.T(L.Encryption.ChangeButton), theme) && !busy)
            {
                if (ValidateNewPassphrase())
                {
                    RunVaultAction(async entered =>
                    {
                        var changed = await vault.ChangePassphraseAsync(entered, cancellation.Token).ConfigureAwait(false);
                        if (changed)
                        {
                            changeMode = false;
                        }

                        return changed;
                    });
                }
            }

            ImGui.Dummy(new Vector2(0f, 2f * scale));
            if (GhostButton(Loc.T(L.Common.Cancel), theme))
            {
                changeMode = false;
                passphrase = string.Empty;
                passphraseConfirm = string.Empty;
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        if (Button(Loc.T(L.Encryption.LockNow), theme) && !busy)
        {
            vault.Lock();
        }
    }

    private void DrawPassphraseInputs()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##encPassphrase", Loc.T(L.Encryption.PassphraseHint), ref passphrase,
            PassphraseMaxLength, ImGuiInputTextFlags.Password);
        ImGui.Dummy(new Vector2(0f, 4f * ImGuiHelpers.GlobalScale));
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##encPassphraseConfirm", Loc.T(L.Encryption.PassphraseConfirmHint),
            ref passphraseConfirm, PassphraseMaxLength, ImGuiInputTextFlags.Password);
    }

    private bool ValidateNewPassphrase()
    {
        if (passphrase.Length < PassphraseMinLength)
        {
            status = Loc.T(L.Encryption.PassphraseTooShort);
            return false;
        }

        if (!string.Equals(passphrase, passphraseConfirm, StringComparison.Ordinal))
        {
            status = Loc.T(L.Encryption.PassphraseMismatch);
            return false;
        }

        return true;
    }

    private void DrawRekey(PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (Plugin.Fonts.Push(1.3f, FontWeight.SemiBold))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(Loc.T(L.Encryption.ForgotTitle));
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Encryption.ForgotBody));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        DrawPassphraseInputs();
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (PrimaryButton(Loc.T(L.Encryption.ForgotConfirm), theme) && !busy)
        {
            if (ValidateNewPassphrase())
            {
                RunVaultAction(async entered =>
                {
                    var rekeyed = await vault.RekeyAsync(entered, cancellation.Token).ConfigureAwait(false);
                    if (rekeyed)
                    {
                        rekeyMode = false;
                    }

                    return rekeyed;
                });
            }
        }

        ImGui.Dummy(new Vector2(0f, 2f * scale));
        if (GhostButton(Loc.T(L.Common.Cancel), theme))
        {
            rekeyMode = false;
            passphrase = string.Empty;
            passphraseConfirm = string.Empty;
        }
    }

    private void AskRekey()
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Encryption.ForgotBody),
            ConfirmLabel = Loc.T(L.Encryption.ForgotConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done =>
            {
                changeMode = false;
                passphrase = string.Empty;
                passphraseConfirm = string.Empty;
                rekeyMode = true;
                done(true);
            },
        });
    }

    private void RunVaultAction(Func<string, Task<bool>> action)
    {
        var entered = passphrase;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var succeeded = await action(entered).ConfigureAwait(false);
                if (succeeded)
                {
                    status = string.Empty;
                    passphrase = string.Empty;
                    passphraseConfirm = string.Empty;
                }
                else if (status == Loc.T(L.Encryption.Working))
                {
                    status = Loc.T(L.Encryption.Failed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Encryption action failed: {exception.Message}");
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
            ImGui.TextWrapped(message);
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

    private static bool PrimaryButton(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.Accent)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.Accent, theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.ButtonActive, Palette.Mix(theme.Accent, new Vector4(0f, 0f, 0f, 1f), 0.18f))
                   .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            return ImGui.Button(label, new Vector2(-1f, 38f * ImGuiHelpers.GlobalScale));
        }
    }

    private static bool GhostButton(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, Palette.WithAlpha(theme.TextStrong, 0f))
                   .Push(ImGuiCol.ButtonHovered, Palette.WithAlpha(theme.TextStrong, 0.08f))
                   .Push(ImGuiCol.ButtonActive, Palette.WithAlpha(theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.Text, theme.TextMuted))
        {
            return ImGui.Button(label, new Vector2(-1f, 32f * ImGuiHelpers.GlobalScale));
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

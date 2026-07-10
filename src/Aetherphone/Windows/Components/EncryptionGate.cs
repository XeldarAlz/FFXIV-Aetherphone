using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class EncryptionGate : IDisposable
{
    private const int PassphraseMinLength = 8;
    private const int PassphraseMaxLength = 256;

    private readonly KeyVault vault;
    private readonly ConversationKeyStore keys;
    private readonly Configuration configuration;
    private readonly CancellationTokenSource cancellation = new();
    private string passphrase = string.Empty;
    private string passphraseConfirm = string.Empty;
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile bool refreshRequested;
    private bool sessionBypassed;

    public EncryptionGate(KeyVault vault, ConversationKeyStore keys, Configuration configuration)
    {
        this.vault = vault;
        this.keys = keys;
        this.configuration = configuration;
    }

    public bool ShouldBlock
    {
        get
        {
            EnsureRefreshed();
            return vault.State switch
            {
                KeyVaultState.NeedsSetup => !configuration.EncryptionSetupPromptShown,
                KeyVaultState.Locked => !sessionBypassed,
                _ => false,
            };
        }
    }

    public void Draw(Rect area, PhoneTheme theme, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var locked = vault.State == KeyVaultState.Locked;
        using (AppSurface.Begin(area))
        {
            ImGui.Dummy(new Vector2(0f, 26f * scale));
            DrawHero(theme, accent, locked, scale);
            ImGui.Dummy(new Vector2(0f, 18f * scale));

            using (Plugin.Fonts.Push(1.35f, FontWeight.SemiBold))
            {
                CenterText(locked ? Loc.T(L.Encryption.UnlockTitle) : Loc.T(L.Encryption.GateIntroTitle),
                    theme.TextStrong);
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            var side = 26f * scale;
            var bodyWidth = MathF.Min(360f * scale, area.Max.X - area.Min.X - side * 2f);
            var bodyLeft = area.Min.X + (area.Max.X - area.Min.X - bodyWidth) * 0.5f;
            ImGui.SetCursorScreenPos(new Vector2(bodyLeft, ImGui.GetCursorScreenPos().Y));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.PushTextWrapPos(bodyLeft + bodyWidth);
                ImGui.TextWrapped(locked ? Loc.T(L.Encryption.UnlockHint) : Loc.T(L.Encryption.GateIntroBody));
                ImGui.PopTextWrapPos();
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            ImGui.SetCursorScreenPos(new Vector2(bodyLeft, ImGui.GetCursorScreenPos().Y));
            ImGui.SetNextItemWidth(bodyWidth);
            ImGui.InputTextWithHint("##gatePassphrase", Loc.T(L.Encryption.PassphraseHint), ref passphrase,
                PassphraseMaxLength, ImGuiInputTextFlags.Password);

            if (!locked)
            {
                ImGui.Dummy(new Vector2(0f, 5f * scale));
                ImGui.SetCursorScreenPos(new Vector2(bodyLeft, ImGui.GetCursorScreenPos().Y));
                ImGui.SetNextItemWidth(bodyWidth);
                ImGui.InputTextWithHint("##gatePassphraseConfirm", Loc.T(L.Encryption.PassphraseConfirmHint),
                    ref passphraseConfirm, PassphraseMaxLength, ImGuiInputTextFlags.Password);
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            ImGui.SetCursorScreenPos(new Vector2(bodyLeft, ImGui.GetCursorScreenPos().Y));
            var primaryLabel = locked ? Loc.T(L.Encryption.UnlockButton) : Loc.T(L.Encryption.SetupButton);
            if (PrimaryButton(primaryLabel, accent, bodyWidth) && !busy)
            {
                if (locked)
                {
                    Unlock();
                }
                else
                {
                    Setup();
                }
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            ImGui.SetCursorScreenPos(new Vector2(bodyLeft, ImGui.GetCursorScreenPos().Y));
            if (GhostButton(locked ? Loc.T(L.Encryption.ContinueWithout) : Loc.T(L.Encryption.NotNow), theme, bodyWidth))
            {
                Dismiss(locked);
            }

            if (status.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                ImGui.SetCursorScreenPos(new Vector2(bodyLeft, ImGui.GetCursorScreenPos().Y));
                using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
                {
                    ImGui.PushTextWrapPos(bodyLeft + bodyWidth);
                    ImGui.TextWrapped(status);
                    ImGui.PopTextWrapPos();
                }
            }
        }
    }

    private static void DrawHero(PhoneTheme theme, Vector4 accent, bool locked, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var available = ImGui.GetContentRegionAvail().X;
        var radius = 34f * scale;
        var center = new Vector2(ImGui.GetCursorScreenPos().X + available * 0.5f, ImGui.GetCursorScreenPos().Y + radius);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)), 48);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.5f)), 48, 1.4f * scale);
        AppSkin.Icon(drawList, center, (locked ? FontAwesomeIcon.Lock : FontAwesomeIcon.ShieldAlt).ToIconString(),
            accent, 1.5f);
        ImGui.Dummy(new Vector2(available, radius * 2f));
    }

    private static void CenterText(string text, Vector4 color)
    {
        var available = ImGui.GetContentRegionAvail().X;
        var size = Typography.Measure(text, 1.35f, FontWeight.SemiBold);
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(origin.X + (available - size.X) * 0.5f, origin.Y), text, color, 1.35f,
            FontWeight.SemiBold);
        ImGui.Dummy(new Vector2(available, size.Y));
    }

    private void Dismiss(bool locked)
    {
        if (locked)
        {
            sessionBypassed = true;
        }
        else if (!configuration.EncryptionSetupPromptShown)
        {
            configuration.EncryptionSetupPromptShown = true;
            configuration.Save();
        }

        passphrase = string.Empty;
        passphraseConfirm = string.Empty;
        status = string.Empty;
    }

    private void Setup()
    {
        if (passphrase.Length < PassphraseMinLength)
        {
            status = Loc.T(L.Encryption.PassphraseTooShort);
            return;
        }

        if (!string.Equals(passphrase, passphraseConfirm, StringComparison.Ordinal))
        {
            status = Loc.T(L.Encryption.PassphraseMismatch);
            return;
        }

        Run(async entered =>
        {
            var ok = await vault.SetupAsync(entered, cancellation.Token).ConfigureAwait(false);
            if (ok)
            {
                configuration.EncryptionSetupPromptShown = true;
                configuration.Save();
            }

            return ok;
        });
    }

    private void Unlock()
    {
        if (passphrase.Length == 0)
        {
            return;
        }

        Run(async entered =>
        {
            var ok = await vault.UnlockAsync(entered, cancellation.Token).ConfigureAwait(false);
            if (ok)
            {
                await keys.HydrateAsync(cancellation.Token).ConfigureAwait(false);
                await keys.HydrateVelvetAsync(cancellation.Token).ConfigureAwait(false);
            }
            else
            {
                status = Loc.T(L.Encryption.WrongPassphrase);
            }

            return ok;
        });
    }

    private void Run(Func<string, Task<bool>> action)
    {
        var entered = passphrase;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var ok = await action(entered).ConfigureAwait(false);
                if (ok)
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
                AepLog.Warning($"Encryption gate action failed: {exception.Message}");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void EnsureRefreshed()
    {
        if (refreshRequested || vault.IsRefreshing)
        {
            return;
        }

        refreshRequested = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await vault.RefreshAsync(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Encryption gate refresh failed: {exception.Message}");
            }
        });
    }

    private static bool PrimaryButton(string label, Vector4 accent, float width)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, accent)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(accent, new Vector4(1f, 1f, 1f, 1f), 0.14f))
                   .Push(ImGuiCol.ButtonActive, Palette.Mix(accent, new Vector4(0f, 0f, 0f, 1f), 0.18f))
                   .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            return ImGui.Button(label, new Vector2(width, 38f * ImGuiHelpers.GlobalScale));
        }
    }

    private static bool GhostButton(string label, PhoneTheme theme, float width)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, Palette.WithAlpha(theme.TextStrong, 0f))
                   .Push(ImGuiCol.ButtonHovered, Palette.WithAlpha(theme.TextStrong, 0.08f))
                   .Push(ImGuiCol.ButtonActive, Palette.WithAlpha(theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.Text, theme.TextMuted))
        {
            return ImGui.Button(label, new Vector2(width, 32f * ImGuiHelpers.GlobalScale));
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

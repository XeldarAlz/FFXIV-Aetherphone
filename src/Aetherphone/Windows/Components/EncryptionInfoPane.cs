using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal sealed class EncryptionInfoPane : IDisposable
{
    private readonly EncryptionVaultActions actions;

    public EncryptionInfoPane(KeyVault vault, ConfirmService confirm)
    {
        actions = new EncryptionVaultActions(vault, confirm);
    }

    public void DrawBody(AppSkin ui, PhoneTheme theme, bool signedIn, bool encrypted)
    {
        if (actions.GeneratedCode.Length > 0)
        {
            DrawGeneratedCode(ui, theme);
            DrawStatus(ui);
            return;
        }

        DrawHero(ui, theme, signedIn, encrypted);
        DrawSummary(ui, signedIn, encrypted);
        DrawVaultSection(ui, theme);
        DrawStatus(ui);
    }

    public void DrawEmbedded(AppSkin ui, PhoneTheme theme)
    {
        if (actions.GeneratedCode.Length > 0)
        {
            DrawGeneratedCode(ui, theme);
        }
        else
        {
            DrawVaultSection(ui, theme);
        }

        DrawStatus(ui);
    }

    private void DrawHero(AppSkin ui, PhoneTheme theme, bool signedIn, bool encrypted)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 34f * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + 16f * scale + radius);
        var vault = actions.Vault;
        var active = encrypted && vault.State == KeyVaultState.Unlocked;
        var closedGlyph = active || vault.State == KeyVaultState.Locked;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 48);
        AppSkin.Icon(center, (closedGlyph ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen).ToIconString(),
            active ? ui.Accent : ui.MutedInk, 1.7f);
        Typography.DrawCentered(new Vector2(center.X, center.Y + radius + 20f * scale), Headline(signedIn, encrypted),
            theme.TextStrong, 1.05f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 16f * scale + radius * 2f + 40f * scale));
    }

    private string Headline(bool signedIn, bool encrypted)
    {
        var vault = actions.Vault;
        if (!signedIn || vault.State == KeyVaultState.Unavailable)
        {
            return Loc.T(L.Encryption.StateUnavailable);
        }

        return vault.State switch
        {
            KeyVaultState.Locked => Loc.T(L.Encryption.StateLocked),
            KeyVaultState.Provisioning => Loc.T(L.Encryption.StateSettingUp),
            KeyVaultState.Unsupported => Loc.T(L.Encryption.PlaintextIndicator),
            _ => encrypted ? Loc.T(L.Encryption.EncryptedIndicator) : Loc.T(L.Encryption.PlaintextIndicator),
        };
    }

    private void DrawSummary(AppSkin ui, bool signedIn, bool encrypted)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var maxWidth = width - 40f * scale;
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y),
            SummaryText(signedIn, encrypted), ui.MutedInk, TextStyles.Subheadline, maxWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 22f * scale));
    }

    private string SummaryText(bool signedIn, bool encrypted)
    {
        var vault = actions.Vault;
        if (!signedIn || vault.State == KeyVaultState.Unavailable)
        {
            return Loc.T(L.Encryption.NotSignedIn);
        }

        return vault.State switch
        {
            KeyVaultState.Locked => vault.RecoveryConfigured
                ? Loc.T(L.Encryption.LockedRecoverBody)
                : Loc.T(L.Encryption.LockedBody),
            KeyVaultState.Provisioning => Loc.T(L.Encryption.SettingUp),
            KeyVaultState.Unsupported => Loc.T(L.Encryption.UnsupportedSummary),
            _ => encrypted ? Loc.T(L.Encryption.Intro) : Loc.T(L.Encryption.SettingUp),
        };
    }

    private void DrawVaultSection(AppSkin ui, PhoneTheme theme)
    {
        switch (actions.Vault.State)
        {
            case KeyVaultState.Locked:
                DrawLockedSection(ui, theme);
                break;
            case KeyVaultState.Unlocked:
                DrawRecoverySection(ui);
                break;
        }
    }

    private void DrawLockedSection(AppSkin ui, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        if (actions.Vault.RecoveryConfigured)
        {
            DrawSectionLabel(ui, Loc.T(L.Encryption.RecoveryCodeLabel));
            DrawCodeInput(ui, theme);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            if (DrawButton(ui, Loc.T(L.Encryption.RecoveryUnlockButton), true) && !actions.Busy
                && RecoveryKey.Canonicalize(actions.CodeEntry).Length > 0)
            {
                actions.BeginRecover();
            }
        }

        if (DrawButton(ui, Loc.T(L.Encryption.NewKeyButton), false) && !actions.Busy)
        {
            actions.AskReset();
        }
    }

    private void DrawRecoverySection(AppSkin ui)
    {
        var scale = UiScale.Current;
        var configured = actions.Vault.RecoveryConfigured;
        DrawSectionLabel(ui, Loc.T(L.Encryption.RecoverySectionTitle));
        DrawWrapped(ui, configured
            ? Loc.T(L.Encryption.RecoveryConfiguredBody)
            : Loc.T(L.Encryption.RecoveryNotSetBody));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var label = configured
            ? Loc.T(L.Encryption.RecoveryRegenerateButton)
            : Loc.T(L.Encryption.RecoverySetupButton);
        if (DrawButton(ui, label, !configured) && !actions.Busy)
        {
            actions.BeginCreateRecoveryCode();
        }
    }

    private void DrawGeneratedCode(AppSkin ui, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        DrawWrapped(ui, Loc.T(L.Encryption.RecoverySaveTitle), theme.TextStrong);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 46f * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 10f * scale);
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f),
            actions.GeneratedCode, theme.TextStrong, 1.15f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 10f * scale));
        if (DrawButton(ui, Loc.T(L.Encryption.RecoveryCopy), false))
        {
            ImGui.SetClipboardText(actions.GeneratedCode);
            actions.Status = Loc.T(L.Friends.Copied);
        }

        DrawWrapped(ui, Loc.T(L.Encryption.RecoverySaveBody));
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (DrawButton(ui, Loc.T(L.Encryption.RecoverySavedButton), true))
        {
            actions.AcknowledgeGeneratedCode();
        }
    }

    private void DrawCodeInput(AppSkin ui, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 38f * scale;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 24f * scale);
        using (Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputText("##encryptionRecoveryCode", ref actions.CodeEntry, 64);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawStatus(AppSkin ui)
    {
        var message = actions.Status;
        if (message.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 8f * UiScale.Current));
        DrawWrapped(ui, message);
    }

    private static void DrawSectionLabel(AppSkin ui, string label)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y), label, ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 24f * scale));
    }

    private static void DrawWrapped(AppSkin ui, string text)
    {
        DrawWrapped(ui, text, ui.MutedInk);
    }

    private static void DrawWrapped(AppSkin ui, string text, Vector4 color)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.DrawWrappedLeft(origin, text, color, TextStyles.Footnote, width - 4f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    private static bool DrawButton(AppSkin ui, string label, bool filled)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 40f * scale;
        var clicked = ui.PillButton(new Rect(origin, new Vector2(origin.X + width, origin.Y + height)), label, filled);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 10f * scale));
        return clicked;
    }

    public void Dispose()
    {
        actions.Dispose();
    }
}

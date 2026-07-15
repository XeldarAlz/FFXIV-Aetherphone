using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Apps.DirectMessages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private string? encryptionPeerRequestedFor;
    private string securityCode = string.Empty;
    private string securityCodeKey = string.Empty;

    private void DrawEncryptionInfo(Rect area, string conversationId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Encryption.InfoTitle), back);
        var conversation = store.Conversation;
        if (conversation is null || conversation.Id != conversationId)
        {
            return;
        }

        if (!conversation.IsGroup && encryptionPeerRequestedFor != conversationId)
        {
            encryptionPeerRequestedFor = conversationId;
            store.RequestPeerKeys(new[] { conversation.OtherUserId });
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var encrypted = store.EncryptingCurrent;
            DrawEncryptionHero(encrypted, scale);
            DrawEncryptionSummary(encrypted, scale);
            if (conversation.IsGroup)
            {
                DrawEncryptionMembers(scale);
            }
            else
            {
                DrawSecurityCode(conversation, encrypted, scale);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawEncryptionHero(bool encrypted, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 34f * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + 16f * scale + radius);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 48);
        AppSkin.Icon(center, (encrypted ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen).ToIconString(),
            encrypted ? ui.Accent : ui.MutedInk, 1.7f);
        var headline = encrypted ? Loc.T(L.Encryption.EncryptedIndicator) : Loc.T(L.Encryption.PlaintextIndicator);
        Typography.DrawCentered(new Vector2(center.X, center.Y + radius + 20f * scale), headline,
            theme.TextStrong, 1.05f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 16f * scale + radius * 2f + 40f * scale));
    }

    private void DrawEncryptionSummary(bool encrypted, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var maxWidth = width - 40f * scale;
        var text = encrypted ? Loc.T(L.Encryption.Intro) : NotEncryptedSummary();
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y), text,
            ui.MutedInk, TextStyles.Subheadline, maxWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 22f * scale));
    }

    private string NotEncryptedSummary()
    {
        if (!store.IsSignedIn || store.VaultState == KeyVaultState.Unavailable)
        {
            return Loc.T(L.Encryption.NotSignedIn);
        }

        if (store.VaultState == KeyVaultState.Unsupported)
        {
            return Loc.T(L.Encryption.UnsupportedSummary);
        }

        if (store.VaultState == KeyVaultState.Provisioning)
        {
            return Loc.T(L.Encryption.SettingUp);
        }

        var waiting = store.CurrentKeyStatus.MembersWithoutKeys;
        if (waiting.Length > 0)
        {
            return Loc.T(L.Encryption.WaitingMembers, WaitingNames(waiting));
        }

        return Loc.T(L.Encryption.SettingUp);
    }

    private string WaitingNames(string[] userIds)
    {
        var members = store.Members;
        var builder = new StringBuilder(64);
        for (var index = 0; index < userIds.Length; index++)
        {
            var name = userIds[index];
            for (var memberIndex = 0; memberIndex < members.Length; memberIndex++)
            {
                if (members[memberIndex].UserId == userIds[index])
                {
                    name = members[memberIndex].DisplayName.Length > 0
                        ? members[memberIndex].DisplayName
                        : members[memberIndex].Handle;
                    break;
                }
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(name);
        }

        return builder.ToString();
    }

    private void DrawSecurityCode(ConversationDto conversation, bool encrypted, float scale)
    {
        DrawSectionLabel(Loc.T(L.Encryption.SecurityCode), scale);
        var code = SecurityCodeFor(conversation.OtherUserId);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (code.Length == 0 || !encrypted)
        {
            var maxWidth = width - 40f * scale;
            var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y),
                Loc.T(L.Encryption.SecurityCodeUnavailable), ui.MutedInk, TextStyles.Footnote, maxWidth);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + 16f * scale));
            return;
        }

        var lineHeight = Typography.Measure("0", 1.02f, FontWeight.Medium).Y;
        var pad = 16f * scale;
        var lineGap = 7f * scale;
        var cardHeight = pad * 2f + lineHeight * 4f + lineGap * 3f;
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        ui.Card(drawList, origin, cardMax, 18f * scale);
        var centerX = (origin.X + cardMax.X) * 0.5f;
        var lineTop = origin.Y + pad;
        var remaining = code.AsSpan();
        while (remaining.Length > 0)
        {
            var breakIndex = remaining.IndexOf('\n');
            var line = breakIndex >= 0 ? remaining[..breakIndex] : remaining;
            Typography.DrawCentered(new Vector2(centerX, lineTop + lineHeight * 0.5f), line.ToString(),
                theme.TextStrong, 1.02f, FontWeight.Medium);
            lineTop += lineHeight + lineGap;
            remaining = breakIndex >= 0 ? remaining[(breakIndex + 1)..] : ReadOnlySpan<char>.Empty;
        }

        HoverTooltip.Show(new Rect(origin, cardMax), Loc.T(L.Encryption.CopyCode), HoverLabelSide.Above);
        if (UiInteract.HoverClick(origin, cardMax))
        {
            ImGui.SetClipboardText(code.Replace('\n', ' '));
            copiedTimer = CopiedSeconds;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 12f * scale));
        var hintOrigin = ImGui.GetCursorScreenPos();
        var hint = copiedTimer > 0f
            ? Loc.T(L.Friends.Copied)
            : Loc.T(L.Encryption.SecurityCodeHint, DirectMessagesStore.DisplayTitle(conversation));
        var hintHeight = Typography.DrawWrappedCentered(new Vector2(hintOrigin.X + width * 0.5f, hintOrigin.Y), hint,
            copiedTimer > 0f ? ui.Accent : ui.MutedInk, TextStyles.Footnote, width - 40f * scale);
        ImGui.SetCursorScreenPos(hintOrigin);
        ImGui.Dummy(new Vector2(width, hintHeight + 14f * scale));
    }

    private string SecurityCodeFor(string otherUserId)
    {
        var myKey = store.MyPublicKey;
        var otherKey = store.PeerKey(otherUserId);
        if (myKey is null || otherKey is null)
        {
            return string.Empty;
        }

        var cacheKey = string.Concat(otherUserId, "|", otherKey.KeyVersion.ToString(CultureInfo.InvariantCulture),
            "|", store.MyKeyVersion.ToString(CultureInfo.InvariantCulture));
        if (securityCodeKey == cacheKey)
        {
            return securityCode;
        }

        securityCode = ComputeSecurityCode(store.MyUserId, myKey, otherUserId, otherKey.PublicKey);
        securityCodeKey = cacheKey;
        return securityCode;
    }

    private static string ComputeSecurityCode(string myUserId, string myKey, string otherUserId, string otherKey)
    {
        var mineFirst = string.CompareOrdinal(myUserId, otherUserId) <= 0;
        var lowId = mineFirst ? myUserId : otherUserId;
        var lowKey = mineFirst ? myKey : otherKey;
        var highId = mineFirst ? otherUserId : myUserId;
        var highKey = mineFirst ? otherKey : myKey;
        var payload = Encoding.UTF8.GetBytes($"aetherphone-safety-v1|{lowId}|{lowKey}|{highId}|{highKey}");
        var hash = SHA512.HashData(payload);
        var builder = new StringBuilder(72);
        for (var group = 0; group < 12; group++)
        {
            ulong value = 0;
            for (var byteIndex = 0; byteIndex < 5; byteIndex++)
            {
                value = (value << 8) | hash[group * 5 + byteIndex];
            }

            if (group > 0)
            {
                builder.Append(group % 3 == 0 ? '\n' : ' ');
            }

            builder.Append((value % 100000).ToString("D5", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private void DrawEncryptionMembers(float scale)
    {
        DrawSectionLabel(Loc.T(L.DirectMessages.Members), scale);
        var members = store.Members;
        var waiting = store.CurrentKeyStatus.MembersWithoutKeys;
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!member.IsActive || member.UserId == store.MyUserId)
            {
                continue;
            }

            var hasKey = true;
            for (var waitingIndex = 0; waitingIndex < waiting.Length; waitingIndex++)
            {
                if (waiting[waitingIndex] == member.UserId)
                {
                    hasKey = false;
                    break;
                }
            }

            DrawEncryptionMemberRow(member, hasKey, scale);
        }
    }

    private void DrawEncryptionMemberRow(ConversationMemberDto member, bool hasKey, float scale)
    {
        var rowHeight = 56f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 14f * scale);
        var pad = 12f * scale;
        var radius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = member.DisplayName.Length > 0 ? member.DisplayName : member.Handle;
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, member.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 10f * scale), label, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale),
            Loc.T(hasKey ? L.Encryption.MemberReady : L.Encryption.MemberNoKey), ui.MutedInk, TextStyles.Footnote);
        AppSkin.Icon(new Vector2(origin.X + width - pad - 8f * scale, origin.Y + rowHeight * 0.5f),
            (hasKey ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen).ToIconString(),
            hasKey ? ui.Accent : ui.MutedInk, 0.95f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }
}

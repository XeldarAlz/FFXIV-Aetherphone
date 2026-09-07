using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float EncryptionHeroRadius = 34f;
    private const float EncryptionHeroGlyph = 34f;
    private const float EncryptionMemberRowHeight = 56f;
    private const float EncryptionMemberGlyph = 18f;
    private const float EncryptionSidePadding = 32f;

    private static readonly TextStyle SecurityCodeStyle = new(1.02f, FontWeight.Medium);

    private string? encryptionPeerRequestedFor;
    private string securityCode = string.Empty;
    private string securityCodeKey = string.Empty;

    private void DrawEncryptionInfo(Rect area, string conversationId)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Encryption.InfoTitle));
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
        using (AppSurface.Begin(body, EncryptionSidePadding))
        {
            var drawList = ImGui.GetWindowDrawList();
            var encrypted = store.EncryptingCurrent;
            DrawEncryptionHero(drawList, encrypted, scale);
            DrawEncryptionSummary(encrypted, scale);
            threadView.DrawEncryptionEmbedded();
            if (conversation.IsGroup)
            {
                DrawEncryptionMembers(drawList, scale);
            }
            else
            {
                DrawSecurityCode(drawList, conversation, encrypted, scale);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawEncryptionHero(ImDrawListPtr drawList, bool encrypted, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var radius = EncryptionHeroRadius * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + 16f * scale + radius);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 48);
        PhoneIcon.Draw(drawList, center, encrypted ? PhoneIcons.Lock : PhoneIcons.LockOpen,
            encrypted ? ink.AccentLink : ink.MutedInk, EncryptionHeroGlyph * scale);
        var headline = encrypted ? Loc.T(L.Encryption.EncryptedIndicator) : Loc.T(L.Encryption.PlaintextIndicator);
        var headlineHeight = Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + radius + 14f * scale),
            headline, ink.TitleInk, TextStyles.Headline, width - 24f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 16f * scale + radius * 2f + 14f * scale + headlineHeight + 12f * scale));
    }

    private void DrawEncryptionSummary(bool encrypted, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var maxWidth = width - 24f * scale;
        var text = encrypted ? Loc.T(L.Encryption.Intro) : NotEncryptedSummary();
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y), text,
            ink.MutedInk, TextStyles.Subheadline, maxWidth);
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

        if (store.VaultState == KeyVaultState.Locked)
        {
            return store.Vault.RecoveryConfigured
                ? Loc.T(L.Encryption.LockedRecoverBody)
                : Loc.T(L.Encryption.LockedNoRecoveryBody);
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
                    name = DirectMessagesStore.MemberLabel(members[memberIndex]);
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

    private void DrawSecurityCode(ImDrawListPtr drawList, ConversationDto conversation, bool encrypted, float scale)
    {
        DrawInsetSectionLabel(Loc.T(L.Encryption.SecurityCode));
        var code = SecurityCodeFor(conversation.OtherUserId);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        if (code.Length == 0 || !encrypted)
        {
            var maxWidth = width - 24f * scale;
            var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y),
                Loc.T(L.Encryption.SecurityCodeUnavailable), ink.MutedInk, TextStyles.Footnote, maxWidth);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + 16f * scale));
            return;
        }

        var lineHeight = Typography.LineHeight(SecurityCodeStyle);
        var pad = 16f * scale;
        var lineGap = 7f * scale;
        var cardHeight = pad * 2f + lineHeight * 4f + lineGap * 3f;
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Md * scale);
        var centerX = (origin.X + cardMax.X) * 0.5f;
        var lineTop = origin.Y + pad;
        var remaining = code.AsSpan();
        while (remaining.Length > 0)
        {
            var breakIndex = remaining.IndexOf('\n');
            var line = breakIndex >= 0 ? remaining[..breakIndex] : remaining;
            Typography.DrawCentered(drawList, new Vector2(centerX, lineTop + lineHeight * 0.5f), line.ToString(),
                ink.TitleInk, SecurityCodeStyle);
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
            copiedTimer > 0f ? ink.AccentLink : ink.MutedInk, TextStyles.Footnote, width - 24f * scale);
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

    private void DrawEncryptionMembers(ImDrawListPtr drawList, float scale)
    {
        DrawInsetSectionLabel(Loc.T(L.DirectMessages.Members));
        var members = store.Members;
        var waiting = store.CurrentKeyStatus.MembersWithoutKeys;
        var rowCount = 0;
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index].IsActive && members[index].UserId != store.MyUserId)
            {
                rowCount++;
            }
        }

        if (rowCount == 0)
        {
            return;
        }

        var card = GroupCard.Begin(ui, rowCount, EncryptionMemberRowHeight);
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

            DrawEncryptionMemberRow(drawList, card.NextRow(), member, hasKey, scale);
        }

        card.End();
        DrawCardGap();
    }

    private void DrawEncryptionMemberRow(ImDrawListPtr drawList, Rect row, ConversationMemberDto member, bool hasKey,
        float scale)
    {
        var radius = ReceiptAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        DrawMemberAvatar(drawList, member, avatarCenter, radius);
        var textLeft = avatarCenter.X + radius + RowTextGap * scale;
        var textMaxWidth = MathF.Max(1f, row.Max.X - EncryptionMemberGlyph * scale - RowTrailingGap * scale - textLeft);
        var band = RowBand(row, scale);
        var rowHovering = UiInteract.Hover(band.Min, band.Max);
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var subHeight = Typography.LineHeight(RowSubStyle);
        var top = row.Center.Y - (titleHeight + RowLineGap * scale + subHeight) * 0.5f;
        UserName.Draw(drawList, "messageapp.encryption.member." + member.UserId, DirectMessagesStore.MemberLabel(member), member.Badges,
            member.BadgeIds, textLeft, top, textMaxWidth, RowTitleStyle, ink.TitleInk, rowHovering, theme);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + RowLineGap * scale),
            Typography.FitText(Loc.T(hasKey ? L.Encryption.MemberReady : L.Encryption.MemberNoKey), textMaxWidth,
                RowSubStyle), ink.MutedInk, RowSubStyle);
        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - EncryptionMemberGlyph * 0.5f * scale, row.Center.Y),
            hasKey ? PhoneIcons.Lock : PhoneIcons.LockOpen, hasKey ? ink.AccentLink : ink.MutedInk,
            EncryptionMemberGlyph * scale);
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Message;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float CellPadX = ChatListChrome.CellPadX;
    private const float ActionRowHeight = ChatListChrome.ActionRowHeight;
    private const float ActionTileGlyph = ChatListChrome.ActionTileGlyph;
    private const float SettingRowHeight = ChatListChrome.SettingRowHeight;
    private const float SettingTileSize = ChatListChrome.SettingTileSize;
    private const float SettingTileGlyph = ChatListChrome.SettingTileGlyph;
    private const float SettingTileRounding = ChatListChrome.SettingTileRounding;
    private const float RowTextGap = ChatListChrome.RowTextGap;
    private const float RowLineGap = ChatListChrome.RowLineGap;
    private const float ChevronSize = ChatListChrome.ChevronSize;
    private const float RowTrailingGap = ChatListChrome.RowTrailingGap;
    private const float RowAvatarGap = ChatListChrome.RowAvatarGap;

    private static readonly TextStyle ScreenTitleStyle = ChatListChrome.ScreenTitleStyle;
    private static readonly TextStyle TabTitleStyle = ChatListChrome.TabTitleStyle;
    private static readonly TextStyle RowTitleStyle = ChatListChrome.RowTitleStyle;
    private static readonly TextStyle RowSubStyle = ChatListChrome.RowSubStyle;
    private static readonly TextStyle RowMetaStyle = ChatListChrome.RowMetaStyle;
    private static readonly TextStyle SectionStyle = ChatListChrome.SectionStyle;

    private readonly ChatListChrome chrome;
    private SocialInk ink = ChatThemes.InkFor(string.Empty);
    private ChatTheme activeTheme = ChatThemes.All[0];
    private Rect screenRect;

    private void ResolveTheme()
    {
        var id = configuration.MessageChatTheme;
        activeTheme = ChatThemes.Resolve(id);
        ink = ChatThemes.InkFor(id);
        ui.Palette = ChatThemes.PaletteFor(id);
        chrome.Ink = ink;
        chrome.Theme = theme;
    }

    private float DrawScreenHeader(Rect area, string title, int trailingSlots = 0, bool showBack = true,
        bool centered = false, string subtitle = "") =>
        chrome.DrawScreenHeader(area, title, back, trailingSlots, showBack, centered, subtitle);

    private void DrawTabHeader(Rect area, string title, int trailingSlots) =>
        chrome.DrawTabHeader(area, title, back, trailingSlots);

    private bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0) =>
        chrome.DrawHeaderIcon(drawList, center, glyph, tooltip, highlighted, badge);

    private void DrawSectionLabel(string label) => chrome.DrawSectionLabel(label);

    private void DrawRowHairline(ImDrawListPtr drawList, in FeedCellScope cell, float textLeft) =>
        chrome.DrawRowHairline(drawList, cell, textLeft);

    private void DrawConversationAvatar(ImDrawListPtr drawList, ConversationDto item, Vector2 center, float radius)
    {
        ConversationAvatar.Draw(drawList, item, center, radius, theme, ui, images, lodestone);
        if (!item.IsGroup && ChatPresence.IsOnline(item.Presence))
        {
            chrome.DrawPresenceDot(drawList, center, radius);
        }
    }

    private void DrawGroupAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string title,
        string? avatarUrl) =>
        ConversationAvatar.DrawGroup(drawList, center, radius, title, avatarUrl, theme, ui, images, lodestone);

    private void DrawContactAvatar(ImDrawListPtr drawList, ContactDto contact, Vector2 center, float radius)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, ContactBook.DisplayLabel(contact), string.Empty,
            contact.AvatarUrl, images, lodestone, 0.95f, 32, 1f, Frames.Of(contact.FrameId));
        if (ChatPresence.IsOnline(contact.Presence))
        {
            chrome.DrawPresenceDot(drawList, center, radius);
        }
    }

    private void DrawMemberAvatar(ImDrawListPtr drawList, ConversationMemberDto member, Vector2 center, float radius)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, DirectMessagesStore.MemberLabel(member), string.Empty, member.AvatarUrl,
            images, lodestone, 0.9f, 32, 1f, Frames.Of(member.FrameId));
    }

    private PersonRowResult BeginPersonRow(ImDrawListPtr drawList, float height, float avatarRadius,
        float trailingReserve, bool interactive, out Vector2 avatarCenter) =>
        chrome.BeginPersonRow(drawList, height, avatarRadius, trailingReserve, interactive, out avatarCenter);

    private void EndPersonRow(ImDrawListPtr drawList, in PersonRowResult row, bool separator = true) =>
        chrome.EndPersonRow(drawList, row, separator);

    private void DrawRowTitleAndSub(ImDrawListPtr drawList, MarqueeId id, string title, string subtitle,
        float left, float right, float centerY, Vector4 titleInk, Vector4 subInk) =>
        chrome.DrawRowTitleAndSub(drawList, id, title, subtitle, left, right, centerY, titleInk, subInk);

    private bool DrawActionRow(ImDrawListPtr drawList, string glyph, string label, string subtitle = "",
        bool chevron = false, bool separator = true) =>
        chrome.DrawActionRow(drawList, "message.action.", glyph, label, subtitle, chevron, separator);

    private bool DrawSettingRow(ImDrawListPtr drawList, string glyph, Vector4 tint, string label, string value = "",
        bool chevron = true, bool separator = true, int badge = 0, Vector4? labelInk = null) =>
        chrome.DrawSettingRow(drawList, glyph, tint, label, value, chevron, separator, badge, labelInk);

    private void DrawRoleTag(ImDrawListPtr drawList, float right, float centerY, string label, out float left) =>
        chrome.DrawRoleTag(drawList, right, centerY, label, out left);

    private static string TrimmedLetter(string label) => ChatListChrome.TrimmedLetter(label);

    private void DrawLetterHeader(ImDrawListPtr drawList, string letter) => chrome.DrawLetterHeader(drawList, letter);

    private static string ChatTime(long unix) => ChatListChrome.ChatTime(unix);

    private static Rect RowBand(Rect row, float scale) => ChatListChrome.RowBand(row, scale);

    private void DrawInsetSectionLabel(string label) => chrome.DrawInsetSectionLabel(label);

    private static void DrawCardGap() => ChatListChrome.DrawCardGap();

    private bool DrawCardRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 tint, string label,
        string value = "", bool chevron = true, int badge = 0, Vector4? labelInk = null) =>
        chrome.DrawCardRow(drawList, row, glyph, tint, label, value, chevron, badge, labelInk);

    private bool DrawCardSwitchRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 tint, string label,
        bool value, string id) =>
        chrome.DrawCardSwitchRow(drawList, row, glyph, tint, label, value, id);

    private bool DrawCardDangerRow(ImDrawListPtr drawList, Rect row, string glyph, string label) =>
        chrome.DrawCardDangerRow(drawList, row, glyph, label);

    private void DrawInfoRow(ImDrawListPtr drawList, Rect row, string label, string value) =>
        chrome.DrawInfoRow(drawList, row, label, value);

    private bool DrawHeroActionButton(ImDrawListPtr drawList, Rect rect, string glyph, string label, bool enabled) =>
        chrome.DrawHeroActionButton(drawList, rect, glyph, label, enabled);

    private Rect PaintHeaderBand(Rect area) => chrome.PaintHeaderBand(area);
}

using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float ThreadAvatarRadius = 18f;
    private const float ThreadAvatarGap = 6f;
    private const float ThreadNameGap = 10f;
    private const float ThreadBackInset = 12f;
    private const int ThreadHeaderSlots = 2;
    private const float PopoutMarkRadius = 3.5f;
    private const float PopoutMarkOffset = 10f;

    private static readonly TextStyle ThreadNameStyle = TextStyles.Headline;
    private static readonly TextStyle ThreadSubStyle = new(0.76f, FontWeight.Regular);

    private readonly Action<Rect> paintThreadBackdrop;
    private string subtitleKey = string.Empty;
    private string subtitleText = string.Empty;
    private float subtitleWidth;
    private int subtitleChannels;

    private void DrawConversation(Rect area, string key)
    {
        inbox.Sync();
        var row = inbox.Find(key);
        if (row is null)
        {
            router.Reset();
            return;
        }

        inbox.Viewing = key;
        var scale = UiScale.Current;
        var header = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        OpenThread(row);
        chatThread.BubbleStyle = ChatThemes.BubbleStyleFor(activeTheme);
        chatThread.Lodestone = lodestone;
        chatThread.Backdrop = paintThreadBackdrop;
        chatThread.Draw(new Rect(new Vector2(area.Min.X, header.Max.Y), area.Max), frameTheme);
        DrawThreadHeader(header, row, scale);
        chatMenu.Draw(area, frameTheme);
    }

    private void PaintThreadBackdrop(Rect listRect) =>
        ChatWallpapers.Paint(ImGui.GetWindowDrawList(), listRect, EffectiveWallpaper(threadKey),
            configuration.LinkpearlWallpaperPattern, wallpaperImages);

    private void DrawThreadHeader(Rect header, InboxRow row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var band = chrome.PaintHeaderBand(header);
        var centerY = band.Center.Y;
        var chipRadius = SocialChrome.BackChipRadius * scale;
        var chipCenter = new Vector2(header.Min.X + ThreadBackInset * scale + chipRadius, centerY);
        if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, ink))
        {
            backToList();
            return;
        }

        var moreCenter = SocialChrome.HeaderSlot(header, 0);
        if (chrome.DrawHeaderIcon(drawList, moreCenter, PhoneIcons.DotsVertical, Loc.T(L.Linkpearl.More)))
        {
            OpenConversationSheet(row, true);
        }

        var bubbles = row.Density == ChatDensity.Bubbles;
        if (chrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(header, 1),
                bubbles ? PhoneIcons.LayoutList : PhoneIcons.MessageCircle,
                Loc.T(bubbles ? L.Linkpearl.ShowAsLog : L.Linkpearl.ShowAsBubbles)))
        {
            ToggleLayout(row);
        }

        if (popouts.IsOpen(row.Key))
        {
            drawList.AddCircleFilled(moreCenter + new Vector2(PopoutMarkOffset * scale, -PopoutMarkOffset * scale),
                PopoutMarkRadius * scale, ImGui.GetColorU32(ink.AccentLink), 12);
        }

        var avatarRadius = ThreadAvatarRadius * scale;
        var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadAvatarGap * scale + avatarRadius, centerY);
        GameChatTiles.DrawAvatar(drawList, avatarCenter, avatarRadius, row, lodestone, ink.Accent);
        var nameLeft = avatarCenter.X + avatarRadius + ThreadNameGap * scale;
        var nameRight = header.Max.X - (CellPadX + SocialChrome.HeaderReserve(ThreadHeaderSlots)) * scale;
        var nameWidth = MathF.Max(1f, nameRight - nameLeft);
        var title = Title(row);
        var subtitle = ThreadSubtitle(row, nameWidth);
        var nameHeight = Typography.LineHeight(ThreadNameStyle);
        var titleId = new MarqueeId("linkpearl.thread.title.", row.Key);
        if (subtitle.Length == 0)
        {
            var soloTop = centerY - nameHeight * 0.5f;
            var soloHovering = UiInteract.Hover(new Vector2(nameLeft, soloTop),
                new Vector2(nameRight, soloTop + nameHeight));
            Marquee.DrawLeft(drawList, titleId, title, nameLeft, soloTop, nameWidth, ThreadNameStyle, ink.TitleInk,
                soloHovering);
        }
        else
        {
            var subHeight = Typography.LineHeight(ThreadSubStyle);
            var top = centerY - (nameHeight + subHeight) * 0.5f;
            var hovering = UiInteract.Hover(new Vector2(nameLeft, top), new Vector2(nameRight, top + nameHeight));
            Marquee.DrawLeft(drawList, titleId, title, nameLeft, top, nameWidth, ThreadNameStyle, ink.TitleInk,
                hovering);
            Typography.Draw(drawList, new Vector2(nameLeft, top + nameHeight),
                Typography.FitText(subtitle, nameWidth, ThreadSubStyle), ink.MutedInk, ThreadSubStyle);
        }

        var hitMin = new Vector2(avatarCenter.X - avatarRadius, header.Min.Y);
        var hitMax = new Vector2(nameRight, header.Max.Y);
        if (UiInteract.HoverClick(hitMin, hitMax))
        {
            OpenThreadInfo(row);
        }
    }

    private string ThreadSubtitle(InboxRow row, float width)
    {
        if (row.Tab is not { } tab)
        {
            return row.World;
        }

        var count = tab.Channels.Count;
        if (string.Equals(subtitleKey, row.Key, StringComparison.Ordinal) && subtitleChannels == count
            && MathF.Abs(subtitleWidth - width) < 0.5f)
        {
            return subtitleText;
        }

        subtitleKey = row.Key;
        subtitleChannels = count;
        subtitleWidth = width;
        subtitleText = GameChatTargets.CollapsedSubtitle(tab, width, ThreadSubStyle);
        return subtitleText;
    }

    private void OpenThreadInfo(InboxRow row)
    {
        if (row.Tab is { } tab)
        {
            router.Push(LinkpearlRoute.TabInfo(tab.Id));
            return;
        }

        for (var index = 0; index < friends.Count; index++)
        {
            var friend = friends[index];
            if (string.Equals(friend.Name, row.Title, StringComparison.OrdinalIgnoreCase)
                && (row.World.Length == 0 || string.Equals(friend.WorldName, row.World, StringComparison.OrdinalIgnoreCase)))
            {
                router.Push(LinkpearlRoute.Detail(friend));
                return;
            }
        }

        router.Push(LinkpearlRoute.Character(string.Empty, row.Title, row.World));
    }

    private void OpenThread(InboxRow row)
    {
        if (string.Equals(threadKey, row.Key, StringComparison.Ordinal) && chatThread.IsOpenFor(row.Key)
            && chatThread.Density == row.Density)
        {
            return;
        }

        threadKey = row.Key;
        chatThread.Open(GameChatTargets.For(row));
    }

    private void ToggleLayout(InboxRow row) =>
        inbox.SetDensity(row, row.Density == ChatDensity.Bubbles ? ChatDensity.Log : ChatDensity.Bubbles);
}

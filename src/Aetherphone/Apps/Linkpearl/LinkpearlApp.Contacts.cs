using Aetherphone.Core;
using Aetherphone.Core.Contacts;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float IdleReadIntervalSeconds = 5f;
    private const float PostRequestReadIntervalSeconds = 0.5f;
    private const float PostRequestPollWindowSeconds = 6f;
    private const float RequestCooldownSeconds = 5f;
    private const float HeroAvatarRadius = 48f;
    private const float HeroTopPad = 18f;
    private const float HeroNameGap = 16f;
    private const float HeroLineGap = 6f;
    private const float HeroActionsGap = 18f;
    private const float HeroActionHeight = 62f;
    private const float HeroActionGap = 8f;
    private const float PresenceRingGap = 5.5f;
    private const float PresenceRingWidth = 3f;
    private const int HeroAvatarSegments = 64;
    private const float HeroMonogramScale = 2.0f;
    private const int HeroActionCount = 4;
    private const string SectionSeparator = " · ";

    private readonly List<FriendEntry> friends = new();
    private float sinceRead;
    private float sinceRequest = RequestCooldownSeconds;
    private float pollWindowRemaining;
    private string onlineLabel = string.Empty;
    private int onlineLabelCount = -1;
    private string offlineLabel = string.Empty;
    private int offlineLabelCount = -1;
    private ulong detailFriendId;
    private string detailTellKey = string.Empty;

    private void TickContacts(float delta)
    {
        sinceRead += delta;
        sinceRequest += delta;
        var readInterval = pollWindowRemaining > 0f ? PostRequestReadIntervalSeconds : IdleReadIntervalSeconds;
        if (pollWindowRemaining > 0f)
        {
            pollWindowRemaining -= delta;
        }

        if (sinceRead >= readInterval)
        {
            ReadFriends();
        }
    }

    private void RequestRefresh()
    {
        if (gameData.LocalPlayer != null && sinceRequest >= RequestCooldownSeconds &&
            FriendListReader.RequestServerData())
        {
            sinceRequest = 0f;
            pollWindowRemaining = PostRequestPollWindowSeconds;
        }

        ReadFriends();
    }

    private void ReadFriends()
    {
        FriendListReader.Read(friends, gameData);
        friends.Sort(CompareFriends);
        sinceRead = 0f;
    }

    private static int CompareFriends(FriendEntry left, FriendEntry right)
    {
        if (left.Online != right.Online)
        {
            return left.Online ? -1 : 1;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawFriendSection(bool online)
    {
        var count = 0;
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Online == online && MatchesContact(friends[index]))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        chrome.DrawSectionLabel(online
            ? SectionLabel(ref onlineLabel, ref onlineLabelCount, Loc.T(L.Contacts.Online), count)
            : SectionLabel(ref offlineLabel, ref offlineLabelCount, Loc.T(L.Contacts.Offline), count));
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Online != online || !MatchesContact(friends[index]))
            {
                continue;
            }

            if (FriendRow.Draw(chrome, friends[index], lodestone))
            {
                router.Push(LinkpearlRoute.Detail(friends[index]));
            }
        }
    }

    private static string SectionLabel(ref string cached, ref int cachedCount, string title, int count)
    {
        if (cachedCount == count && cached.StartsWith(title, StringComparison.Ordinal))
        {
            return cached;
        }

        cachedCount = count;
        cached = string.Concat(title, SectionSeparator, count.ToString(Loc.Culture));
        return cached;
    }

    private bool MatchesContact(FriendEntry friend) =>
        peopleSearch.Length == 0 || friend.Name.Contains(peopleSearch, StringComparison.OrdinalIgnoreCase);

    private void DrawFriendDetail(Rect area, FriendEntry friend)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        chrome.DrawScreenHeader(area, string.Empty, backToList, 1);
        if (chrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Refresh,
                Loc.T(L.Common.Refresh)))
        {
            RequestRefresh();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawFriendHero(friend, width, scale);
            DrawFriendActions(friend, width, scale);
            DrawFriendCards(friend, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawFriendHero(FriendEntry friend, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var radius = HeroAvatarRadius * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + HeroTopPad * scale + radius);
        AvatarView.Draw(drawList, avatarCenter, radius, friend.Online ? ink.Accent : ink.FaintInk,
            Initials.Of(friend.Name), HeroMonogramScale,
            lodestone.Avatar(friend.Name, friend.WorldName, radius * 2f), HeroAvatarSegments);
        if (friend.Online)
        {
            drawList.AddCircle(avatarCenter, radius + PresenceRingGap * scale, ImGui.GetColorU32(ink.PresenceGreen),
                HeroAvatarSegments, PresenceRingWidth * scale);
        }

        var top = avatarCenter.Y + radius + HeroNameGap * scale;
        var nameHeight = Typography.DrawWrappedCentered(new Vector2(centerX, top), friend.Name, ink.TitleInk,
            TextStyles.Title2, width - Metrics.Space.Xl * scale);
        top += nameHeight + HeroLineGap * scale;
        var meta = FriendLabels.HeroMeta(friend);
        var metaHeight = Typography.DrawWrappedCentered(new Vector2(centerX, top), meta, ink.MutedInk,
            TextStyles.Subheadline, width - Metrics.Space.Xl * scale);
        top += metaHeight + HeroLineGap * scale;
        var presenceHeight = Typography.DrawWrappedCentered(new Vector2(centerX, top), FriendLabels.Presence(friend),
            friend.Online ? ink.AccentLink : ink.MutedInk, TextStyles.Footnote, width - Metrics.Space.Xl * scale);
        top += presenceHeight + HeroActionsGap * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top - origin.Y));
    }

    private void DrawFriendActions(FriendEntry friend, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gap = HeroActionGap * scale;
        var buttonWidth = (width - gap * (HeroActionCount - 1)) / HeroActionCount;
        var height = HeroActionHeight * scale;
        var canInvite = friend.Online;
        var canVisit = friend.HomeWorldId != 0 && gameData.LocalCurrentWorldId == friend.HomeWorldId;
        if (chrome.DrawHeroActionButton(drawList, ActionRect(origin, 0, buttonWidth, gap, height),
                PhoneIcons.MessageCircle, Loc.T(L.Contacts.Message), true))
        {
            OpenDirectThread(friend.Name, SendTarget(friend));
        }

        if (chrome.DrawHeroActionButton(drawList, ActionRect(origin, 1, buttonWidth, gap, height),
                PhoneIcons.UserSquareRounded, Loc.T(L.Contacts.Plate), true))
        {
            FriendActions.OpenAdventurerPlate(friend.ContentId);
        }

        if (chrome.DrawHeroActionButton(drawList, ActionRect(origin, 2, buttonWidth, gap, height),
                PhoneIcons.UserPlus, Loc.T(L.Contacts.Party), canInvite))
        {
            FriendActions.InviteToParty(friend.ContentId, friend.CurrentWorldId);
        }

        if (chrome.DrawHeroActionButton(drawList, ActionRect(origin, 3, buttonWidth, gap, height),
                PhoneIcons.Home, Loc.T(L.Contacts.Visit), canVisit))
        {
            FriendActions.VisitEstate(friend.ContentId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private static Rect ActionRect(Vector2 origin, int index, float buttonWidth, float gap, float height)
    {
        var min = new Vector2(origin.X + index * (buttonWidth + gap), origin.Y);
        return new Rect(min, min + new Vector2(buttonWidth, height));
    }

    private void DrawFriendCards(FriendEntry friend, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hasFreeCompany = friend.FreeCompany.Length > 0;
        var card = GroupCard.Begin(ui, hasFreeCompany ? 3 : 2, ChatListChrome.SettingRowHeight);
        if (chrome.DrawCardRow(drawList, card.NextRow(), PhoneIcons.InfoCircle, ChatListChrome.TintSlate,
                Loc.T(L.Contacts.SearchInfo)))
        {
            FriendActions.OpenSearchInfo(friend.ContentId);
        }

        if (chrome.DrawCardRow(drawList, card.NextRow(), PhoneIcons.World, ChatListChrome.TintAzure,
                Loc.T(L.Linkpearl.LodestoneProfile)))
        {
            router.Push(LinkpearlRoute.Character(string.Empty, friend.Name, friend.WorldName));
        }

        if (hasFreeCompany)
        {
            chrome.DrawCardRow(drawList, card.NextRow(), PhoneIcons.Shield, ChatListChrome.TintTeal,
                Loc.T(L.FindPeople.FreeCompany), friend.FreeCompany, chevron: false);
        }

        card.End();
        var tellRow = inbox.Find(TellKeyFor(friend));
        if (tellRow is null)
        {
            return;
        }

        ChatListChrome.DrawCardGap();
        chrome.DrawInsetSectionLabel(Loc.T(L.Linkpearl.FilterTells));
        var tells = GroupCard.Begin(ui, 5, ChatListChrome.SettingRowHeight);
        var pinned = chrome.DrawCardSwitchRow(drawList, tells.NextRow(), PhoneIcons.PinFilled, ChatListChrome.TintGold,
            Loc.T(L.Common.Pin), tellRow.Pinned, "linkpearl.contact.pin");
        if (pinned != tellRow.Pinned)
        {
            TogglePin(tellRow);
        }

        var muted = chrome.DrawCardSwitchRow(drawList, tells.NextRow(), PhoneIcons.BellOff, ChatListChrome.TintSlate,
            Loc.T(L.Linkpearl.Mute), tellRow.Muted, "linkpearl.contact.mute");
        if (muted != tellRow.Muted)
        {
            inbox.ToggleMuted(tellRow);
        }

        if (chrome.DrawCardRow(drawList, tells.NextRow(), PhoneIcons.Wallpaper, ChatListChrome.TintViolet,
                Loc.T(L.Message.Wallpaper)))
        {
            router.Push(LinkpearlRoute.Wallpaper(tellRow.Key));
        }

        var popoutOpen = popouts.IsOpen(tellRow.Key);
        if (chrome.DrawCardRow(drawList, tells.NextRow(), PhoneIcons.ExternalLink, ChatListChrome.TintGreen,
                Loc.T(popoutOpen ? L.Linkpearl.ClosePopout : L.Linkpearl.OpenPopout), chevron: false)
            && !popouts.Toggle(tellRow.Key))
        {
            ShellToast.Show(Loc.T(L.Linkpearl.PopoutLimit, LinkpearlPopouts.MaxWindows));
        }

        if (chrome.DrawCardDangerRow(drawList, tells.NextRow(), PhoneIcons.Trash, Loc.T(L.Linkpearl.ClearHistory)))
        {
            AskClearHistory(tellRow);
        }

        tells.End();
    }

    private string TellKeyFor(FriendEntry friend)
    {
        if (detailFriendId == friend.ContentId && detailTellKey.Length > 0)
        {
            return detailTellKey;
        }

        detailFriendId = friend.ContentId;
        detailTellKey = ChatStreams.ForTell(SendTarget(friend));
        return detailTellKey;
    }

    private void OpenDirectThread(string display, string sendTarget)
    {
        var at = sendTarget.IndexOf('@');
        var world = at >= 0 ? sendTarget[(at + 1)..] : string.Empty;
        var row = inbox.EnsureTell(display, world);
        inbox.MarkRead(row);
        activeTab = MessagesTab.Chats;
        router.Push(LinkpearlRoute.Conversation(row.Key));
    }

    private static string SendTarget(FriendEntry friend) =>
        friend.WorldName.Length > 0 ? string.Concat(friend.Name, "@", friend.WorldName) : friend.Name;
}

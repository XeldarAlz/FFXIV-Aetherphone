using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Contacts;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Contacts;

internal sealed class ContactsApp : IPhoneApp
{
    private const float IdleReadIntervalSeconds = 5f;
    private const float PostRequestReadIntervalSeconds = 0.5f;
    private const float PostRequestPollWindowSeconds = 6f;
    private const float RequestCooldownSeconds = 5f;
    private const float RowHeight = 60f;
    public string Id => "contacts";
    public string DisplayName => Loc.T(L.Apps.Contacts);
    public string Glyph => "C";
    public Vector4 Accent => new(0.45f, 0.55f, 0.95f, 1f);
    public int BadgeCount => 0;
    private readonly GameData gameData;
    private readonly MessageLauncher launcher;
    private readonly LodestoneService lodestone;
    private readonly List<FriendEntry> friends = new();
    private readonly ViewRouter<FriendEntry?> router;
    private readonly RouterDraw<FriendEntry?> drawView;
    private readonly Action backToList;
    private float sinceRead;
    private float sinceRequest = RequestCooldownSeconds;
    private float pollWindowRemaining;
    private string search = string.Empty;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public ContactsApp(GameData gameData, MessageLauncher launcher, LodestoneService lodestone)
    {
        this.gameData = gameData;
        this.launcher = launcher;
        this.lodestone = lodestone;
        router = new ViewRouter<FriendEntry?>(null);
        drawView = DrawView;
        backToList = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        RequestRefresh();
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
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

    public void Draw(in PhoneContext context)
    {
        var delta = ImGui.GetIO().DeltaTime;
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

        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, delta, drawView);
    }

    private void DrawView(FriendEntry? view, Rect area, int depth)
    {
        if (view is { } friend)
        {
            DrawDetail(area, friend);
        }
        else
        {
            DrawList(area);
        }
    }

    private void DrawList(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);
        if (DrawRefreshButton(context))
        {
            RequestRefresh();
        }

        var scale = ImGuiHelpers.GlobalScale;
        if (friends.Count == 0)
        {
            var emptyBody = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
            Typography.DrawCentered(emptyBody.Center, Loc.T(L.Contacts.Empty), frameTheme.TextMuted);
            return;
        }

        var pad = 16f * scale;
        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, searchTop),
            new Vector2(area.Max.X - pad, searchTop + 44f * scale));
        SearchField.Draw(searchBar, "##contactsSearch", Loc.T(L.Common.Search), ref search, frameTheme);
        var body = new Rect(new Vector2(area.Min.X, searchBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawSection(Loc.T(L.Contacts.Online), true);
            DrawSection(Loc.T(L.Contacts.Offline), false);
        }
    }

    private void DrawSection(string title, bool online)
    {
        var count = 0;
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Online == online && Matches(friends[index]))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        SettingsSection.Header($"{title} · {count}", frameTheme);
        var card = GroupCard.Begin(frameTheme, count, RowHeight);
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Online != online || !Matches(friends[index]))
            {
                continue;
            }

            if (ContactRow.Draw(card.NextRow(), friends[index], frameTheme, lodestone))
            {
                router.Push(friends[index]);
            }
        }

        card.End();
    }

    private bool Matches(FriendEntry friend) =>
        search.Length == 0 || friend.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

    private void DrawDetail(Rect area, FriendEntry friend)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.Contacts.Detail), backToList);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = frameTheme;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawHero(friend, theme, lodestone);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawActions(friend, theme);
            var card = GroupCard.Begin(theme, 1);
            if (SettingsRow.Link(card.NextRow(), "i", new Vector4(0.40f, 0.42f, 0.50f, 1f),
                    Loc.T(L.Contacts.SearchInfo), string.Empty, theme))
            {
                FriendActions.OpenSearchInfo(friend.ContentId);
            }

            card.End();
        }
    }

    private static void DrawHero(FriendEntry friend, PhoneTheme theme, LodestoneService lodestone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var hasLocation = friend.Online && friend.Location.Length > 0;
        var heroHeight = (hasLocation ? 184f : 164f) * scale;
        var heroMin = origin;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        var rounding = 22f * scale;
        Elevation.Card(dl, heroMin, heroMax, rounding, scale);
        Squircle.Fill(dl, heroMin, heroMax, rounding, ImGui.GetColorU32(theme.GroupedCard));
        var tint = friend.Online ? theme.Accent : theme.SurfaceMuted;
        Material.TopGlow(dl, heroMin, heroMax, rounding, tint, 0.82f, 0.15f);
        Material.EdgeSquircle(dl, heroMin, heroMax, rounding, scale);
        var centerX = heroMin.X + width * 0.5f;
        var avatarRadius = 36f * scale;
        var avatarCenter = new Vector2(centerX, heroMin.Y + 20f * scale + avatarRadius);
        if (friend.Online)
        {
            ProgressRing.Glow(avatarCenter, avatarRadius, theme.Accent, 0.5f);
        }

        var baseColor = friend.Online ? theme.Accent : theme.SurfaceMuted;
        AvatarView.Draw(dl, avatarCenter, avatarRadius, baseColor, Initials.Of(friend.Name), 2.0f,
            lodestone.Avatar(friend.Name, friend.WorldName), 64);
        Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 18f * scale), friend.Name,
            theme.TextStrong, TextStyles.Title2);
        var statusWord = friend.Online ? Loc.T(L.Contacts.Online) : Loc.T(L.Contacts.Offline);
        var status = friend.Online && friend.JobName.Length > 0
            ? $"{friend.WorldName} · {friend.JobName} · {statusWord}"
            : $"{friend.WorldName} · {statusWord}";
        Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 42f * scale), status,
            theme.TextMuted, TextStyles.Subheadline);
        if (hasLocation)
        {
            Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 63f * scale), friend.Location,
                theme.TextMuted, TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private void DrawActions(FriendEntry friend, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var canInvite = friend.Online;
        var canVisit = friend.HomeWorldId != 0 && gameData.LocalCurrentWorldId == friend.HomeWorldId;
        var count = 2 + (canInvite ? 1 : 0) + (canVisit ? 1 : 0);
        var radius = 26f * scale;
        var rowHeight = radius * 2f + 30f * scale;
        var spacing = width / count;
        var centerY = origin.Y + radius + 2f * scale;
        var slot = 0;
        if (QuickAction.Draw("contact.message", new Vector2(origin.X + (slot + 0.5f) * spacing, centerY), radius,
                FontAwesomeIcon.CommentDots, new Vector4(0.30f, 0.78f, 0.42f, 1f), Loc.T(L.Contacts.Message), theme))
        {
            launcher.Request(friend.Name, SendTarget(friend));
            frameNavigation.Open("messages");
        }

        slot++;
        if (QuickAction.Draw("contact.plate", new Vector2(origin.X + (slot + 0.5f) * spacing, centerY), radius,
                FontAwesomeIcon.IdCard, new Vector4(0.45f, 0.55f, 0.95f, 1f), Loc.T(L.Contacts.Plate), theme))
        {
            FriendActions.OpenAdventurerPlate(friend.ContentId);
        }

        slot++;
        if (canInvite)
        {
            if (QuickAction.Draw("contact.party", new Vector2(origin.X + (slot + 0.5f) * spacing, centerY), radius,
                    FontAwesomeIcon.UserPlus, theme.Accent, Loc.T(L.Contacts.Party), theme))
            {
                FriendActions.InviteToParty(friend.ContentId, friend.CurrentWorldId);
            }

            slot++;
        }

        if (canVisit && QuickAction.Draw("contact.visit", new Vector2(origin.X + (slot + 0.5f) * spacing, centerY),
                radius, FontAwesomeIcon.Home, new Vector4(0.96f, 0.65f, 0.20f, 1f), Loc.T(L.Contacts.Visit), theme))
        {
            FriendActions.VisitEstate(friend.ContentId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private bool DrawRefreshButton(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var center = new Vector2(content.Max.X - 14f * scale, content.Min.Y + AppHeader.Height * scale * 0.5f);
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(16f * scale, 16f * scale),
            center + new Vector2(16f * scale, 16f * scale));
        var glyph = FontAwesomeIcon.Sync.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, hovered ? context.Theme.TextStrong : context.Theme.Accent))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string SendTarget(FriendEntry friend) =>
        friend.WorldName.Length > 0 ? $"{friend.Name}@{friend.WorldName}" : friend.Name;

    public void Dispose()
    {
    }
}

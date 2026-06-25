using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Contacts;
using Aetherphone.Core.Game;
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

    public string Id => "contacts";

    public string DisplayName => "Contacts";

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
        RequestRefresh();
    }

    public void OnClosed() => router.Reset();

    private void RequestRefresh()
    {
        if (gameData.LocalPlayer != null && sinceRequest >= RequestCooldownSeconds && FriendListReader.RequestServerData())
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
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);

        if (friends.Count == 0)
        {
            Typography.DrawCentered(body.Center, "Open your in-game friend list once", frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawSection("Online", true);
            DrawSection("Offline", false);
        }
    }

    private void DrawSection(string title, bool online)
    {
        var count = 0;
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Online == online)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        SettingsSection.Header(title, frameTheme);
        var card = GroupCard.Begin(frameTheme, count, 56f);
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Online != online)
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

    private void DrawDetail(Rect area, FriendEntry friend)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, "Contact", backToList);

        var scale = ImGuiHelpers.GlobalScale;
        var theme = frameTheme;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);

        using (AppSurface.Begin(body))
        {
            DrawProfile(friend, theme, lodestone);

            var canInvite = friend.Online;
            var canVisit = friend.HomeWorldId != 0 && gameData.LocalCurrentWorldId == friend.HomeWorldId;
            var rowCount = 3 + (canInvite ? 1 : 0) + (canVisit ? 1 : 0);

            var card = GroupCard.Begin(theme, rowCount);

            if (SettingsRow.Link(card.NextRow(), "M", new Vector4(0.30f, 0.78f, 0.42f, 1f), "Message", string.Empty, theme))
            {
                launcher.Request(friend.Name, SendTarget(friend));
                frameNavigation.Open("messages");
            }

            if (SettingsRow.Link(card.NextRow(), "P", new Vector4(0.45f, 0.55f, 0.95f, 1f), "Adventurer Plate", string.Empty, theme))
            {
                FriendActions.OpenAdventurerPlate(friend.ContentId);
            }

            if (SettingsRow.Link(card.NextRow(), "i", new Vector4(0.40f, 0.42f, 0.50f, 1f), "Search Info", string.Empty, theme))
            {
                FriendActions.OpenSearchInfo(friend.ContentId);
            }

            if (canInvite && SettingsRow.Link(card.NextRow(), "+", theme.Accent, "Invite to Party", string.Empty, theme))
            {
                FriendActions.InviteToParty(friend.ContentId, friend.CurrentWorldId);
            }

            if (canVisit && SettingsRow.Link(card.NextRow(), "H", new Vector4(0.96f, 0.65f, 0.20f, 1f), "Visit Estate", string.Empty, theme))
            {
                FriendActions.VisitEstate(friend.ContentId);
            }

            card.End();
        }
    }

    private static void DrawProfile(FriendEntry friend, PhoneTheme theme, LodestoneService lodestone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;

        var avatarRadius = 34f * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + 10f * scale + avatarRadius);
        var baseColor = friend.Online ? theme.Accent : theme.SurfaceMuted;
        AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius, baseColor, Initials.Of(friend.Name), 1.8f, lodestone.Avatar(friend.Name, friend.WorldName), 48);

        Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 18f * scale), friend.Name, theme.TextStrong, 1.3f, FontWeight.Bold);

        var status = friend.Online
            ? (friend.JobName.Length > 0 ? $"{friend.WorldName} · {friend.JobName} · Online" : $"{friend.WorldName} · Online")
            : $"{friend.WorldName} · Offline";
        Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 42f * scale), status, theme.TextMuted, 0.9f);

        var bottomPadding = 62f * scale;
        if (friend.Online && friend.Location.Length > 0)
        {
            Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 64f * scale), friend.Location, theme.TextMuted, 0.9f);
            bottomPadding = 84f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 10f * scale + avatarRadius * 2f + bottomPadding));
    }

    private bool DrawRefreshButton(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var center = new Vector2(content.Max.X - 14f * scale, content.Min.Y + AppHeader.Height * scale * 0.5f);
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(16f * scale, 16f * scale), center + new Vector2(16f * scale, 16f * scale));

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

    private static string SendTarget(FriendEntry friend) => friend.WorldName.Length > 0 ? $"{friend.Name}@{friend.WorldName}" : friend.Name;

    public void Dispose()
    {
    }
}

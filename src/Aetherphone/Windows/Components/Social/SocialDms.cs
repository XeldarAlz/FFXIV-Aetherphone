using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum SocialDmScreen : byte
{
    Inbox,
    Archived,
    NewMessage,
    Thread,
    ChatImage,
    ImageView,
    Reactions,
    Encryption,
    Share,
    Privacy,
}

internal interface ISocialDmHost : IPhoneApp
{
    SocialInk Ink { get; }

    AppPalette SearchPalette { get; }

    PhoneTheme Theme { get; }

    INavigator Navigation { get; }

    void PushDm(SocialDmScreen screen, string? id = null, bool animate = true);

    void ReplaceDm(SocialDmScreen screen, string id);

    void PopDm();

    bool IsDmOnTop(SocialDmScreen screen, string? id = null);

    void OpenProfile(string userId);

    void OpenPost(string postId);
}

internal sealed class SocialDmLabels
{
    public required string LogTag { get; init; }
    public required string RowIdPrefix { get; init; }
    public required LocString InboxTitle { get; init; }
    public required LocString InboxEmpty { get; init; }
    public required LocString InboxEmptyHint { get; init; }
    public required LocString ThreadEmpty { get; init; }
    public required LocString MessageHint { get; init; }
    public required LocString ImportFromPc { get; init; }
    public required LocString NewMessage { get; init; }
    public required LocString NewMessageHint { get; init; }
    public required LocString NewMessageEmpty { get; init; }
    public required LocString CannotMessage { get; init; }
    public required LocString ActiveNow { get; init; }
    public required LocString Requests { get; init; }
    public required LocString RequestsCount { get; init; }
    public required LocString RequestsEmpty { get; init; }
    public required LocString RequestBanner { get; init; }
    public required LocString AcceptRequest { get; init; }
    public required LocString DeleteConversation { get; init; }
    public required LocString DeleteConversationMessage { get; init; }
    public required LocString DeleteConfirm { get; init; }
    public required LocString DeleteCancel { get; init; }
    public required LocString SendTo { get; init; }
    public required LocString SearchHint { get; init; }
    public required LocString Send { get; init; }
    public required LocString Sent { get; init; }
    public required LocString PrivacyTitle { get; init; }
    public required LocString PrivacyHint { get; init; }
    public LocString? YouRepliedToStory { get; init; }
    public LocString? RepliedToYourStory { get; init; }
}

internal sealed partial class SocialDms : IDisposable
{
    private const float PillHeight = Button.RegularHeight;
    private const float SmallButtonCeiling = 31f;
    private const float RegularButtonCeiling = 39f;
    private const float EmptyStateTop = 72f;
    private const float DisabledRowVeil = 0.45f;
    private const float UserRowHeight = 64f;
    private const float UserRowAvatarRadius = 22f;

    private static readonly TextStyle ScreenTitleStyle = TextStyles.Headline;
    private static readonly TextStyle EmptyTitleStyle = TextStyles.Title2;
    private static readonly TextStyle EmptyBodyStyle = TextStyles.Callout;

    private readonly ISocialDmHost host;
    private readonly SocialDmStore dm;
    private readonly SocialFeedStore store;
    private readonly SocialDmLabels labels;
    private readonly AppSkin ui;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ChatRowSheet rowSheet;
    private readonly ThreadView threadView;
    private readonly Action back;

    public SocialDms(ISocialDmHost host, SocialDmStore dm, SocialFeedStore store, AccountClient account,
        SocialDmLabels labels, AppSkin ui, RemoteImageCache images, LodestoneService lodestone, GameData gameData,
        HttpService http, PhotoLibrary library, Configuration configuration, ConfirmService confirm,
        ReportService report, TranslationService translation, WallpaperImageCache wallpaperImages,
        EncryptionHelpService encryptionHelp, List<string> pinned, List<string> archived, Action<string> showToast)
    {
        this.host = host;
        this.dm = dm;
        this.store = store;
        this.account = account;
        this.labels = labels;
        this.ui = ui;
        this.images = images;
        this.lodestone = lodestone;
        this.gameData = gameData;
        this.configuration = configuration;
        this.confirm = confirm;
        back = host.PopDm;
        rowSheet = new ChatRowSheet(configuration, pinned, archived, labels.DeleteConversation, AskDeleteConversation,
            showToast, () => inboxFilterDirty = true);
        threadView = new ThreadView(this, http, library, report, translation, wallpaperImages, encryptionHelp);
    }

    private SocialInk Ink => host.Ink;

    private static float CellPadX => SocialChrome.CellPadX;

    private static SocialUserRowStyle UserRowStyle(SocialInk ink) => new(UserRowHeight, UserRowAvatarRadius,
        SocialChrome.CellPadX, 12f, PillHeight, TextStyles.Headline, TextStyles.Subheadline, ink.HoverTint);

    public void Gate()
    {
        rowSheet.Gate();
        threadView.GateMenus();
    }

    public void PrefillDraft(string draft) => threadView.PrefillDraft(draft);

    public void OnAppClosed() => threadView.OnAppClosed();

    public void Draw(SocialDmScreen screen, Rect area, string? id, bool activeLayer)
    {
        switch (screen)
        {
            case SocialDmScreen.Inbox:
                DrawInbox(area);
                break;
            case SocialDmScreen.Archived:
                DrawInboxArchived(area);
                break;
            case SocialDmScreen.NewMessage:
                DrawNewMessage(area);
                break;
            case SocialDmScreen.Thread:
                threadView.Draw(ChatArea(area), id!, activeLayer);
                break;
            case SocialDmScreen.ChatImage:
                threadView.DrawImagePicker(ChatArea(area), id!);
                break;
            case SocialDmScreen.ImageView:
                threadView.DrawImageViewer(ChatArea(area), id!);
                break;
            case SocialDmScreen.Reactions:
                threadView.DrawReactions(ChatArea(area), id!);
                break;
            case SocialDmScreen.Encryption:
                threadView.DrawEncryptionScreen(ChatArea(area));
                break;
            case SocialDmScreen.Share:
                DrawShare(area, id!);
                break;
            case SocialDmScreen.Privacy:
                DrawPrivacy(area);
                break;
        }
    }

    public void DrawOverlays(Rect screen)
    {
        if (rowSheet.IsOpen && !host.IsDmOnTop(SocialDmScreen.Inbox) && !host.IsDmOnTop(SocialDmScreen.Archived))
        {
            rowSheet.Close();
        }

        rowSheet.Draw(screen, ui);
    }

    public void OpenInbox()
    {
        ResetInbox();
        host.PushDm(SocialDmScreen.Inbox);
    }

    public void ResetInbox()
    {
        inboxDraft = string.Empty;
        inboxShowRequests = false;
    }

    public void OpenThread(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        host.PushDm(SocialDmScreen.Thread, userId);
    }

    public void AskDeleteConversation(string otherId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(labels.DeleteConversation),
            Message = Loc.T(labels.DeleteConversationMessage),
            ConfirmLabel = Loc.T(labels.DeleteConfirm),
            CancelLabel = Loc.T(labels.DeleteCancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(otherId),
        });
    }

    private void DeleteConversation(string otherId)
    {
        var threadOpen = host.IsDmOnTop(SocialDmScreen.Thread, otherId);
        rowSheet.Forget(otherId);
        dm.DeleteThread(otherId);
        if (threadOpen)
        {
            host.PopDm();
        }
    }

    private Rect ChatArea(Rect area)
    {
        var sidePadding = host.Theme.SidePadding * UiScale.Current;
        return new Rect(new Vector2(area.Min.X + sidePadding, area.Min.Y),
            new Vector2(area.Max.X - sidePadding, area.Max.Y));
    }

    private void DrawScreenHeader(Rect area, string title, int trailingSlots = 0) =>
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(trailingSlots), string.Empty, true, true);

    private static bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        SocialInk ink, bool highlighted = false, float iconSize = 24f) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            iconSize, tooltip, ink, ink.TitleInk, highlighted, 0);

    private void DrawEmptyState(Rect area, string title, string body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = MathF.Max(1f, area.Width - CellPadX * 2f * scale);
        var top = area.Min.Y + EmptyStateTop * scale;
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, EmptyTitleStyle, Ink.TitleInk,
            new Vector2(area.Center.X, top), maxWidth);
        if (body.Length == 0)
        {
            return;
        }

        Typography.DrawWrappedCentered(drawList, body, EmptyBodyStyle, Ink.MutedInk,
            new Vector2(area.Center.X, titleBottom + 8f * scale), maxWidth);
    }

    private bool DrawAccentPill(Rect rect, string label, bool enabled = true) =>
        Button.Draw(SnapButton(rect), label, ui.Ink, ButtonStyle.Prominent, enabled: enabled);

    private bool DrawGrayPill(Rect rect, string label) =>
        Button.Draw(SnapButton(rect), label, ui.Ink, ButtonStyle.Gray);

    private static Rect SnapButton(Rect rect)
    {
        var scale = UiScale.Current;
        var units = rect.Height / scale;
        var height = units <= SmallButtonCeiling ? Button.SmallHeight
            : units <= RegularButtonCeiling ? Button.RegularHeight
            : Button.LargeHeight;
        var half = height * scale * 0.5f;
        return new Rect(new Vector2(rect.Min.X, rect.Center.Y - half), new Vector2(rect.Max.X, rect.Center.Y + half));
    }

    private SocialUserRowResult DrawUserRow(UserDto user, float trailingWidth)
    {
        var regionCode = SocialRegion.Resolve(user.Region, user.World, gameData);
        return SocialUserRow.Draw(labels.RowIdPrefix, user, SocialIdentity.ProfileMeta(user.Handle, regionCode),
            trailingWidth, UserRowStyle(Ink), Ink, host.Theme, images, lodestone);
    }

    private void DimRow(ImDrawListPtr drawList, Rect bounds) =>
        drawList.AddRectFilled(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(Ink.BackdropTop, DisabledRowVeil)));

    private static void DrawHairline(ImDrawListPtr drawList, float left, float right, float y, SocialInk ink) =>
        FeedCell.Hairline(drawList, left, right, y, ink.Hairline);

    public void Dispose()
    {
        privacyCancellation.Cancel();
        privacyCancellation.Dispose();
        threadView.Dispose();
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal sealed class ProfileCard
{
    public const float RowHeight = 72f;
    private const float PortraitDiameter = 56f;
    private const float TextGap = 2f;
    private const float PlaceholderMix = 0.28f;
    private const float PlaceholderIconScale = 0.9f;
    private const float MonogramScale = 1.5f;
    private const int PortraitSegments = 48;
    private const string HandlePrefix = "@";

    private readonly AethernetSession session;
    private readonly GameData gameData;
    private readonly CharacterWatch characters;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private ulong cachedContentId;
    private string characterName = string.Empty;
    private string characterWorld = string.Empty;
    private string cachedHandle = string.Empty;
    private string handleLabel = string.Empty;

    public ProfileCard(AethernetSession session, GameData gameData, CharacterWatch characters,
        RemoteImageCache images, LodestoneService lodestone)
    {
        this.session = session;
        this.gameData = gameData;
        this.characters = characters;
        this.images = images;
        this.lodestone = lodestone;
    }

    public bool Draw(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        RefreshCharacter();
        var user = session.IsSignedIn ? session.CurrentUser : null;
        var card = GroupCard.Begin(theme, 1, RowHeight);
        var row = card.NextRow();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            SettingsRow.DrawRowHighlight(row, theme);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var drawList = ImGui.GetWindowDrawList();
        var radius = PortraitDiameter * scale * 0.5f;
        var center = new Vector2(row.Min.X + radius, row.Center.Y);
        DrawPortrait(drawList, center, radius, theme, user);

        var textLeft = center.X + radius + Metrics.Space.Md * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        var textWidth = MathF.Max(1f, chevronTip.X - SettingsRow.ChevronReserve(scale) - textLeft);
        var title = Headline(user);
        var subtitle = Subheadline(user);
        var titleSize = Typography.Measure(title, TextStyles.Headline);
        var subtitleSize = Typography.Measure(subtitle, TextStyles.Subheadline);
        var gap = TextGap * scale;
        var blockTop = row.Center.Y - (titleSize.Y + gap + subtitleSize.Y) * 0.5f;
        Marquee.DrawLeftAuto(drawList, "settings.profile.title", title, textLeft, blockTop, textWidth,
            TextStyles.Headline, theme.TextStrong);
        Marquee.DrawLeftAuto(drawList, "settings.profile.subtitle", subtitle, textLeft, blockTop + titleSize.Y + gap,
            textWidth, TextStyles.Subheadline, theme.TextMuted);
        SettingsRow.DrawChevron(drawList, chevronTip, scale, theme.TextMuted);
        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        card.End();
        return clicked;
    }

    private void RefreshCharacter()
    {
        var contentId = characters.CurrentContentId;
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            characterName = string.Empty;
            characterWorld = string.Empty;
            cachedContentId = contentId;
            return;
        }

        if (contentId == cachedContentId && characterName.Length > 0)
        {
            return;
        }

        cachedContentId = contentId;
        characterName = player.Name.TextValue;
        characterWorld = gameData.WorldName(player.HomeWorld.RowId);
    }

    private string Headline(UserDto? user)
    {
        if (characterName.Length > 0)
        {
            return characterName;
        }

        if (user is not null && user.Name.Length > 0)
        {
            return user.Name;
        }

        return Loc.T(L.Account.HeroSignInTitle);
    }

    private string Subheadline(UserDto? user)
    {
        if (user is null)
        {
            return Loc.T(L.Account.NotSignedIn);
        }

        if (user.Handle.Length == 0)
        {
            return user.DisplayName;
        }

        if (!string.Equals(cachedHandle, user.Handle, StringComparison.Ordinal))
        {
            cachedHandle = user.Handle;
            handleLabel = string.Concat(HandlePrefix, user.Handle);
        }

        return handleLabel;
    }

    private void DrawPortrait(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme, UserDto? user)
    {
        if (user is not null)
        {
            AvatarView.DrawRemote(drawList, center, radius, theme, user.Name, user.World, user.AvatarUrl, images,
                lodestone, MonogramScale, PortraitSegments, 1f, Frames.Of(user.FrameId));
            return;
        }

        if (characterName.Length > 0)
        {
            AvatarView.DrawRemote(drawList, center, radius, theme, characterName, characterWorld, null, images,
                lodestone, MonogramScale, PortraitSegments);
            return;
        }

        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.Mix(theme.GroupedCard, theme.TextMuted, PlaceholderMix)), PortraitSegments);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.User, theme.TextMuted, radius * PlaceholderIconScale);
    }
}

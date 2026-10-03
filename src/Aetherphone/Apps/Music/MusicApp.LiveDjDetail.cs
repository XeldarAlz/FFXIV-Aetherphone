using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Rolladeck;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float LiveDjDetailAvatarRadius = 40f;

    private readonly List<(string Label, FontAwesomeIcon Icon, string Url)> liveDjSocialLinks = new(8);
    private static readonly string[] LiveDjSocialButtonIds =
    [
        "music.liveDj.soc.0", "music.liveDj.soc.1", "music.liveDj.soc.2", "music.liveDj.soc.3",
        "music.liveDj.soc.4", "music.liveDj.soc.5", "music.liveDj.soc.6", "music.liveDj.soc.7",
    ];
    private LiveDjEntry? selectedDj;
    private string? lastDjDetailKey;
    private string cachedDjEventLabel = string.Empty;
    private string cachedDjViewerCount = string.Empty;

    private void DrawLiveDjDetail(in PhoneContext context)
    {
        var dj = selectedDj;
        if (dj == null)
        {
            Router.Pop();
            return;
        }

        var scale = UiScale.Current;
        var frame = BeginPage(context);
        var body = frame.Body;

        var drawList = ImGui.GetWindowDrawList();
        var venueEntry = FindVenueForDj(dj);
        var venueLogoUrl = venueEntry?.LogoUrl;
        var djKey = dj.DjSlug ?? dj.DjName;
        var isNewDj = djKey != lastDjDetailKey;
        if (isNewDj)
        {
            lastDjDetailKey = djKey;
            cachedDjEventLabel = string.IsNullOrEmpty(dj.EventName) ? string.Empty : "♦ " + dj.EventName;
            cachedDjViewerCount = NumberText.Group(dj.ViewerCount);
        }

        using (AppSurface.Begin(body))
        {
            if (isNewDj)
            {
                ImGui.SetScrollY(0f);
            }

            var scrollY = ImGui.GetScrollY();

            var rawOrigin = ImGui.GetCursorScreenPos();
            var origin = isNewDj
                ? new Vector2(rawOrigin.X, rawOrigin.Y + scrollY)
                : rawOrigin;
            var width = ImGui.GetContentRegionAvail().X;
            var sidePadding = 16f * scale;
            var contentWidth = width - sidePadding * 2f;

            var avatarRadius = LiveDjDetailAvatarRadius * scale;
            var avatarCenter = new Vector2(origin.X + width * 0.5f, origin.Y + 20f * scale + avatarRadius);

            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
                dj.DjName, dj.Server ?? string.Empty, dj.AvatarUrl, images, lodestone, 1.0f, 80);

            var djHeaderName = Typography.FitText(dj.NormalizedName, contentWidth * 0.75f, TextStyles.Headline);
            var djHeaderSize = Typography.Measure(djHeaderName, TextStyles.Headline);
            Typography.Draw(drawList,
                new Vector2(origin.X + (width - djHeaderSize.X) * 0.5f, avatarCenter.Y + avatarRadius + 8f * scale),
                djHeaderName, ui.Palette.TitleInk, TextStyles.Headline);

            var djHeaderHeight = 20f * scale + avatarRadius * 2f + 8f * scale + djHeaderSize.Y + 16f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, djHeaderHeight));

            var actionRadius = StationPlayRadius * scale;
            var actionRowHeight = actionRadius * 2f;
            var actionRowY = ImGui.GetCursorScreenPos().Y;

            if (dj.CanTeleport)
            {
                var teleportLabel = Loc.T(L.Rolladeck.Teleport);
                var teleportWidth = MathF.Min(
                    Typography.Measure(teleportLabel, TextStyles.Callout).X + 34f * scale,
                    contentWidth - actionRadius * 2f - sidePadding);
                var teleportMin = new Vector2(origin.X + sidePadding, actionRowY + actionRadius - 18f * scale);
                var teleportRect = new Rect(teleportMin, teleportMin + new Vector2(teleportWidth, 36f * scale));
                if (ui.GhostButton(teleportRect, teleportLabel))
                {
                    Windows.TeleportActions.AskThenTravel(confirm, dj.VenueName ?? dj.NormalizedName,
                        $"{dj.ServerLabel} · {dj.FormattedAddress}", dj.TeleportDestination!);
                }

                if (!liveDjLifestreamAvailable)
                {
                    HoverTooltip.Show(teleportRect, Loc.T(L.Rolladeck.LifestreamNotInstalled), HoverLabelSide.Above);
                }
            }

            if (dj.TwitchUrl != null)
            {
                var watchCenter = new Vector2(origin.X + width - actionRadius - sidePadding, actionRowY + actionRadius);
                var watchMin = watchCenter - new Vector2(actionRadius, actionRadius);
                var watchMax = watchCenter + new Vector2(actionRadius, actionRadius);
                var watchHovered = UiInteract.Hover(watchMin, watchMax);
                var watchFill = watchHovered ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.92f);
                drawList.AddCircleFilled(watchCenter, actionRadius, ImGui.GetColorU32(watchFill), 32);
                AppSkin.Icon(drawList, watchCenter, IconGlyph.Of(FontAwesomeIcon.Play), ui.Palette.BackdropBottom, 1f);
                if (watchHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (watchHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Windows.UrlActions.AskThenOpen(dj.RolladeckUrl ?? dj.TwitchUrl ?? "https://xivrolladeck.com");
                }
            }

            ImGui.Dummy(new Vector2(width, actionRowHeight + sidePadding));

            var cursorY = ImGui.GetCursorScreenPos().Y;
            var infoCardHeight = OnAirCardHeight * scale;
            var infoMin = new Vector2(origin.X + sidePadding, cursorY);
            var infoMax = new Vector2(origin.X + sidePadding + contentWidth, cursorY + infoCardHeight);
            var infoHovered = venueEntry != null && UiInteract.Hover(infoMin, infoMax);
            ui.Card(drawList, infoMin, infoMax, 10f * scale, elevated: false);
            if (infoHovered)
            {
                Squircle.Fill(drawList, infoMin, infoMax, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var logoRadius = 14f * scale;
            var logoCenterX = infoMin.X + 14f * scale + logoRadius;
            var logoCenterY = infoMin.Y + infoCardHeight * 0.5f;
            var logoCenter = new Vector2(logoCenterX, logoCenterY);
            if (venueLogoUrl != null)
            {
                AvatarView.DrawRemote(drawList, logoCenter, logoRadius, theme,
                    dj.VenueName ?? string.Empty, dj.Server ?? string.Empty, venueLogoUrl,
                    images, lodestone, 0.75f, 36);
            }
            else
            {
                AvatarView.Draw(drawList, logoCenter, logoRadius, theme.Accent,
                    Initials.Of(dj.VenueName ?? string.Empty), 0.75f, AvatarHandle.Disabled, 36);
            }

            var infoTextX = logoCenterX + logoRadius + 8f * scale;
            var textMaxWidth = infoMax.X - infoTextX - 80f * scale;

            var detailAddress = dj.District != null && dj.Ward.HasValue && dj.Plot.HasValue
                ? $"{dj.District}-W{dj.Ward}-P{dj.Plot}"
                : dj.District ?? string.Empty;

            var venueText = dj.VenueName ?? Loc.T(L.Rolladeck.VenueUnknown);
            var venueFitted = Typography.FitText(venueText, textMaxWidth, TextStyles.BodyEmphasized);

            if (detailAddress.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(infoTextX, infoMin.Y + 10f * scale), detailAddress, ui.MutedInk, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(infoTextX, infoMin.Y + 26f * scale), venueFitted, ui.Palette.TitleInk, TextStyles.BodyEmphasized);
            }
            else
            {
                Typography.Draw(drawList, new Vector2(infoTextX, infoMin.Y + 16f * scale), venueFitted, ui.Palette.TitleInk, TextStyles.BodyEmphasized);
            }

            var serverText = dj.ServerLabel;
            var serverSize = Typography.Measure(serverText, TextStyles.Footnote);
            Typography.Draw(drawList,
                new Vector2(infoMax.X - 14f * scale - serverSize.X, infoMin.Y + 10f * scale),
                serverText, ui.Palette.MutedInk, TextStyles.Footnote);

            if (infoHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenVenueDetail(venueEntry!);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, djHeaderHeight + actionRowHeight + sidePadding + infoCardHeight + 12f * scale));

            if (!string.IsNullOrEmpty(dj.EventName))
            {
                var eventOrigin = ImGui.GetCursorScreenPos();
                var eventLabel = cachedDjEventLabel;
                var eventSize = Typography.Measure(eventLabel, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(eventOrigin.X + sidePadding, eventOrigin.Y), eventLabel, ui.Palette.HeaderInk, TextStyles.Footnote);
                ImGui.SetCursorScreenPos(eventOrigin);
                ImGui.Dummy(new Vector2(width, eventSize.Y + 12f * scale));
            }


            var statsOrigin = ImGui.GetCursorScreenPos();
            var viewers = string.Format(Loc.T(L.Rolladeck.Viewers), cachedDjViewerCount);
            var viewerSize = Typography.Measure(viewers, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(statsOrigin.X + sidePadding, statsOrigin.Y), viewers, ui.Palette.Accent, TextStyles.Subheadline);
            ImGui.SetCursorScreenPos(statsOrigin);
            ImGui.Dummy(new Vector2(width, viewerSize.Y + 8f * scale));

            if (!string.IsNullOrEmpty(dj.NormalizedTitle))
            {
                var titleOrigin = ImGui.GetCursorScreenPos();
                var lines = Typography.WrapText(dj.NormalizedTitle, TextStyles.Subheadline, contentWidth - 4f * scale);
                var lineHeight = Typography.Measure("A", TextStyles.Subheadline).Y;
                var lineY = titleOrigin.Y;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Typography.Draw(drawList, new Vector2(titleOrigin.X + sidePadding, lineY), lines[lineIndex], ui.Palette.MutedInk, TextStyles.Subheadline);
                    lineY += lineHeight + 2f * scale;
                }

                ImGui.SetCursorScreenPos(titleOrigin);
                ImGui.Dummy(new Vector2(width, lineY - titleOrigin.Y + 16f * scale));
            }

            if (dj.Genres.Count > 0)
            {
                var genreOrigin = ImGui.GetCursorScreenPos();
                var genreLabel = Loc.T(L.Rolladeck.SectionGenres);
                var genreLabelHeight = Typography.Measure(genreLabel, TextStyles.Caption1).Y;
                Typography.Draw(drawList, new Vector2(genreOrigin.X + sidePadding, genreOrigin.Y), genreLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(genreOrigin);
                ImGui.Dummy(new Vector2(width, genreLabelHeight + 6f * scale));

                var chipX = origin.X + sidePadding;
                var chipY = ImGui.GetCursorScreenPos().Y;
                var chipHeight = 24f * scale;
                var chipPadding = 10f * scale;
                var chipGapX = 6f * scale;
                var rowMaxX = origin.X + sidePadding + contentWidth;
                var currentChipX = chipX;
                var currentChipY = chipY;

                for (var genreIndex = 0; genreIndex < dj.Genres.Count; genreIndex++)
                {
                    var genre = dj.Genres[genreIndex];
                    var genreSize = Typography.Measure(genre, TextStyles.Footnote);
                    var chipWidth = genreSize.X + chipPadding * 2f;
                    if (currentChipX + chipWidth > rowMaxX && currentChipX > chipX)
                    {
                        currentChipX = chipX;
                        currentChipY += chipHeight + 6f * scale;
                    }

                    var chipMin = new Vector2(currentChipX, currentChipY);
                    var chipMax = new Vector2(currentChipX + chipWidth, currentChipY + chipHeight);
                    Squircle.Stroke(drawList, chipMin, chipMax, 6f * scale, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
                    Typography.Draw(drawList, new Vector2(chipMin.X + chipPadding, chipMin.Y + (chipHeight - genreSize.Y) * 0.5f),
                        genre, ui.Palette.BodyInk, TextStyles.Footnote);
                    currentChipX += chipWidth + chipGapX;
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, chipY));
                ImGui.Dummy(new Vector2(width, (currentChipY - chipY) + chipHeight + 14f * scale));
            }

            liveDjSocialLinks.Clear();
            if (!string.IsNullOrEmpty(dj.TwitchUrl))
            {
                liveDjSocialLinks.Add(("Twitch",    FontAwesomeIcon.Tv,        dj.TwitchUrl!));
            }

            if (!string.IsNullOrEmpty(dj.Twitter))
            {
                liveDjSocialLinks.Add(("Twitter",   FontAwesomeIcon.Feather,   dj.Twitter!));
            }

            if (!string.IsNullOrEmpty(dj.Bluesky))
            {
                liveDjSocialLinks.Add(("Bluesky",   FontAwesomeIcon.Cloud,     dj.Bluesky!));
            }

            if (!string.IsNullOrEmpty(dj.Instagram))
            {
                liveDjSocialLinks.Add(("Instagram", FontAwesomeIcon.Camera,    dj.Instagram!));
            }

            if (!string.IsNullOrEmpty(dj.Youtube))
            {
                liveDjSocialLinks.Add(("YouTube",   FontAwesomeIcon.Play,      dj.Youtube!));
            }

            if (!string.IsNullOrEmpty(dj.Tiktok))
            {
                liveDjSocialLinks.Add(("TikTok",    FontAwesomeIcon.Music,     dj.Tiktok!));
            }

            if (!string.IsNullOrEmpty(dj.Website))
            {
                liveDjSocialLinks.Add(("Website",   FontAwesomeIcon.Globe,     dj.Website!));
            }

            if (!string.IsNullOrEmpty(dj.MusicPlatform))
            {
                liveDjSocialLinks.Add(("Music",     FontAwesomeIcon.Headphones, dj.MusicPlatform!));
            }

            if (liveDjSocialLinks.Count > 0)
            {
                var socialOrigin = ImGui.GetCursorScreenPos();
                var socialLabel = Loc.T(L.Rolladeck.SectionLinks);
                var socialLabelHeight = Typography.Measure(socialLabel, TextStyles.Caption1).Y;
                Typography.Draw(drawList, new Vector2(socialOrigin.X + sidePadding, socialOrigin.Y), socialLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(socialOrigin);
                ImGui.Dummy(new Vector2(width, socialLabelHeight + 6f * scale));

                var iconButtonSize = 36f * scale;
                var iconGapX = 8f * scale;
                var socialRowMaxX = origin.X + sidePadding + contentWidth;
                var socialX = origin.X + sidePadding;
                var socialY = ImGui.GetCursorScreenPos().Y;
                var socialStartY = socialY;

                for (var socialIndex = 0; socialIndex < liveDjSocialLinks.Count; socialIndex++)
                {
                    var (label, icon, url) = liveDjSocialLinks[socialIndex];
                    if (socialX + iconButtonSize > socialRowMaxX && socialX > origin.X + sidePadding)
                    {
                        socialX = origin.X + sidePadding;
                        socialY += iconButtonSize + 8f * scale;
                    }

                    var buttonMin = new Vector2(socialX, socialY);
                    var buttonMax = new Vector2(socialX + iconButtonSize, socialY + iconButtonSize);

                    ImGui.SetCursorScreenPos(buttonMin);
                    var clicked = ImGui.InvisibleButton(LiveDjSocialButtonIds[socialIndex], new Vector2(iconButtonSize, iconButtonSize));
                    var buttonHovered = ImGui.IsItemHovered();

                    Squircle.Stroke(drawList, buttonMin, buttonMax, 8f * scale,
                        ImGui.GetColorU32(buttonHovered ? ui.Palette.Accent : ui.Palette.CardStroke), 1f);
                    AppSkin.Icon(drawList, new Vector2(buttonMin.X + iconButtonSize * 0.5f, buttonMin.Y + iconButtonSize * 0.5f),
                        IconGlyph.Of(icon), buttonHovered ? ui.Palette.Accent : ui.Palette.MutedInk, 0.88f);

                    if (buttonHovered)
                    {
                        ImGui.SetTooltip(label);
                    }

                    if (clicked)
                    {
                        Windows.UrlActions.AskThenOpen(url);
                    }

                    socialX += iconButtonSize + iconGapX;
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, socialStartY));
                ImGui.Dummy(new Vector2(width, (socialY - socialStartY) + iconButtonSize + 14f * scale));
            }

            if (!string.IsNullOrEmpty(dj.Bio))
            {
                var bioOrigin = ImGui.GetCursorScreenPos();
                var bioLabel = Loc.T(L.Rolladeck.SectionAbout);
                var bioLabelHeight = Typography.Measure(bioLabel, TextStyles.Caption1).Y;
                Typography.Draw(drawList, new Vector2(bioOrigin.X + sidePadding, bioOrigin.Y), bioLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(bioOrigin);
                ImGui.Dummy(new Vector2(width, bioLabelHeight + 6f * scale));

                var textOrigin = ImGui.GetCursorScreenPos();
                var lines = Typography.WrapText(dj.Bio, TextStyles.Subheadline, contentWidth);
                var lineHeight = Typography.Measure("A", TextStyles.Subheadline).Y;
                var lineY = textOrigin.Y;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Typography.Draw(drawList, new Vector2(textOrigin.X + sidePadding, lineY), lines[lineIndex], ui.Palette.BodyInk, TextStyles.Subheadline);
                    lineY += lineHeight + 2f * scale;
                }

                ImGui.SetCursorScreenPos(textOrigin);
                ImGui.Dummy(new Vector2(width, lineY - textOrigin.Y + 16f * scale));
            }
        }

        EndPage(in frame, context, dj.NormalizedName);
    }
}

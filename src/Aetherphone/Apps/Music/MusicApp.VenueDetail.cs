using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Rolladeck;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private OpenVenueEntry? selectedVenue;
    private string? lastVenueDetailKey;

    private void DrawVenueDetail(in PhoneContext context)
    {
        var venue = selectedVenue;
        if (venue == null)
        {
            Router.Pop();
            return;
        }

        var scale = UiScale.Current;
        var frame = BeginPage(context);
        var body = frame.Body;

        var venueKey = venue.Slug ?? venue.DisplayName;
        var isNewVenue = venueKey != lastVenueDetailKey;
        if (isNewVenue)
        {
            lastVenueDetailKey = venueKey;
        }

        using (AppSurface.Begin(body))
        {
            if (isNewVenue)
            {
                ImGui.SetScrollY(0f);
            }

            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var sidePadding = 16f * scale;
            var contentWidth = width - sidePadding * 2f;

            var logoRadius = 32f * scale;
            var logoCenter = new Vector2(origin.X + sidePadding + logoRadius, origin.Y + 20f * scale + logoRadius);

            AvatarView.DrawRemote(drawList, logoCenter, logoRadius, theme,
                venue.DisplayName, venue.Server ?? string.Empty, venue.LogoUrl,
                images, lodestone, 0.75f, 64);

            var nameX = logoCenter.X + logoRadius + 12f * scale;
            var nameMaxWidth = origin.X + sidePadding + contentWidth - nameX;
            var nameText = Typography.FitText(venue.DisplayName, nameMaxWidth, TextStyles.Headline);
            var nameSize = Typography.Measure(nameText, TextStyles.Headline);
            var nameY = logoCenter.Y - nameSize.Y * 0.5f;

            if (!string.IsNullOrEmpty(venue.ActiveLabel))
            {
                nameY -= nameSize.Y * 0.5f + 2f * scale;
                var activeText = Typography.FitText(venue.ActiveLabel, nameMaxWidth, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(nameX, nameY + nameSize.Y + 4f * scale),
                    activeText, ui.Palette.Accent, TextStyles.Footnote);
            }

            Typography.Draw(drawList, new Vector2(nameX, nameY), nameText, ui.Palette.TitleInk, TextStyles.Headline);

            var headerBlockHeight = 20f * scale + logoRadius * 2f + 16f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, headerBlockHeight));

            var address = venue.FormattedAddress;
            if (!string.IsNullOrEmpty(address) || !string.IsNullOrEmpty(venue.ServerLabel))
            {
                var cardMin = new Vector2(origin.X + sidePadding, ImGui.GetCursorScreenPos().Y);
                var cardMax = new Vector2(origin.X + sidePadding + contentWidth, cardMin.Y + 52f * scale);
                ui.Card(drawList, cardMin, cardMax, 10f * scale, elevated: false);

                if (!string.IsNullOrEmpty(address))
                {
                    Typography.Draw(drawList, new Vector2(cardMin.X + 14f * scale, cardMin.Y + 12f * scale),
                        address, ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);
                }

                if (!string.IsNullOrEmpty(venue.ServerLabel))
                {
                    var serverSize = Typography.Measure(venue.ServerLabel, TextStyles.Footnote);
                    Typography.Draw(drawList,
                        new Vector2(cardMax.X - 14f * scale - serverSize.X, cardMin.Y + 12f * scale),
                        venue.ServerLabel, ui.Palette.MutedInk, TextStyles.Footnote);
                }

                if (!string.IsNullOrEmpty(venue.FirstOpenedYear))
                {
                    var firstOpenedSize = Typography.Measure(venue.FirstOpenedYear, TextStyles.Caption1);
                    Typography.Draw(drawList,
                        new Vector2(cardMax.X - 14f * scale - firstOpenedSize.X, cardMax.Y - 12f * scale - firstOpenedSize.Y),
                        venue.FirstOpenedYear, ui.Palette.MutedInk, TextStyles.Caption1);
                }

                ImGui.Dummy(new Vector2(width, 52f * scale + 12f * scale));
            }

            if (!string.IsNullOrEmpty(venue.DjName))
            {
                var nowPlayingCursor = ImGui.GetCursorScreenPos();
                var nowPlayingCardHeight = 64f * scale;
                var nowPlayingMin = new Vector2(origin.X + sidePadding, nowPlayingCursor.Y);
                var nowPlayingMax = new Vector2(origin.X + sidePadding + contentWidth, nowPlayingCursor.Y + nowPlayingCardHeight);
                ui.Card(drawList, nowPlayingMin, nowPlayingMax, 10f * scale, elevated: false);

                var nowPlayingLabel = Loc.T(L.Rolladeck.LiveNow);
                var nowPlayingLabelSize = Typography.Measure(nowPlayingLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(nowPlayingMin.X + 14f * scale, nowPlayingMin.Y + 10f * scale),
                    nowPlayingLabel, ui.Palette.Accent, TextStyles.Caption1);

                var watchRadius = venue.DjTwitch != null ? 20f * scale : 0f;
                var djMaxWidth = nowPlayingMax.X - (nowPlayingMin.X + 14f * scale) - (watchRadius > 0f ? watchRadius * 2f + 24f * scale : 4f * scale);
                var djNameY = nowPlayingMin.Y + 10f * scale + nowPlayingLabelSize.Y + 3f * scale;
                var djNameFitted = Typography.FitText(venue.DjName, djMaxWidth, TextStyles.SubheadlineEmphasized);
                Typography.Draw(drawList, new Vector2(nowPlayingMin.X + 14f * scale, djNameY),
                    djNameFitted, ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);

                if (venue.DjTwitch != null)
                {
                    var watchCenter = new Vector2(nowPlayingMax.X - 14f * scale - watchRadius, nowPlayingMin.Y + nowPlayingCardHeight * 0.5f);
                    drawList.AddCircleFilled(watchCenter, watchRadius, ImGui.GetColorU32(ui.Palette.Accent), 32);
                    AppSkin.Icon(drawList, watchCenter, IconGlyph.Of(FontAwesomeIcon.Play), ui.Palette.BackdropBottom, 1f);
                    ImGui.SetCursorScreenPos(new Vector2(watchCenter.X - watchRadius, watchCenter.Y - watchRadius));
                    if (ImGui.InvisibleButton("##venueWatch", new Vector2(watchRadius * 2f, watchRadius * 2f)))
                    {
                        Windows.UrlActions.AskThenOpen(venue.DjRolladeckUrl ?? venue.DjTwitch!);
                    }
                }

                ImGui.SetCursorScreenPos(nowPlayingCursor);
                ImGui.Dummy(new Vector2(width, nowPlayingCardHeight + 10f * scale));
            }

            var eventName = venue.EventName ?? venue.DiscordEventName;
            var eventLabel = venue.EventName != null
                ? Loc.T(L.Rolladeck.EventLabel)
                : Loc.T(L.Rolladeck.DiscordEventLabel);
            if (!string.IsNullOrEmpty(eventName))
            {
                var eventCursor = ImGui.GetCursorScreenPos();
                var eventCardHeight = 64f * scale;
                var eventMin = new Vector2(origin.X + sidePadding, eventCursor.Y);
                var eventMax = new Vector2(origin.X + sidePadding + contentWidth, eventCursor.Y + eventCardHeight);
                ui.Card(drawList, eventMin, eventMax, 10f * scale, elevated: false);

                var eventLabelSize = Typography.Measure(eventLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(eventMin.X + 14f * scale, eventMin.Y + 10f * scale),
                    eventLabel, ui.Palette.Accent, TextStyles.Caption1);

                var eventFitted = Typography.FitText(eventName, contentWidth - 28f * scale, TextStyles.SubheadlineEmphasized);
                var eventY = eventMin.Y + 10f * scale + eventLabelSize.Y + 3f * scale;
                Typography.Draw(drawList, new Vector2(eventMin.X + 14f * scale, eventY),
                    eventFitted, ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);

                ImGui.SetCursorScreenPos(eventCursor);
                ImGui.Dummy(new Vector2(width, eventCardHeight + 10f * scale));
            }

            var pillOrigin = ImGui.GetCursorScreenPos();
            var buttonHeight = 36f * scale;
            var buttonGap = 8f * scale;
            var primaryUrl = venue.WebsiteUrl ?? venue.RolladeckUrl;
            var primaryLabel = venue.WebsiteUrl != null ? Loc.T(L.Rolladeck.Website) : Loc.T(L.Rolladeck.Visit);

            var buttonCount = (venue.CanTeleport ? 1 : 0) + (primaryUrl != null ? 1 : 0) + (venue.DiscordUrl != null ? 1 : 0);
            if (buttonCount > 0)
            {
                var buttonWidth = (contentWidth - buttonGap * (buttonCount - 1)) / buttonCount;
                var buttonX = pillOrigin.X + sidePadding;

                if (venue.CanTeleport)
                {
                    var teleportButtonRect = new Rect(new Vector2(buttonX, pillOrigin.Y), new Vector2(buttonX + buttonWidth, pillOrigin.Y + buttonHeight));
                    if (ui.GhostButton(teleportButtonRect, Loc.T(L.Rolladeck.Teleport)))
                    {
                        Windows.TeleportActions.AskThenTravel(confirm, venue.DisplayName,
                            $"{venue.ServerLabel} · {venue.FormattedAddress}", venue.TeleportDestination!);
                    }

                    if (!liveDjLifestreamAvailable)
                    {
                        HoverTooltip.Show(teleportButtonRect, Loc.T(L.Rolladeck.LifestreamNotInstalled), HoverLabelSide.Above);
                    }

                    buttonX += buttonWidth + buttonGap;
                }

                if (primaryUrl != null)
                {
                    var visitRect = new Rect(new Vector2(buttonX, pillOrigin.Y), new Vector2(buttonX + buttonWidth, pillOrigin.Y + buttonHeight));
                    if (ui.GhostButton(visitRect, primaryLabel))
                    {
                        Windows.UrlActions.AskThenOpen(primaryUrl);
                    }

                    buttonX += buttonWidth + buttonGap;
                }

                if (venue.DiscordUrl != null)
                {
                    var discordRect = new Rect(new Vector2(buttonX, pillOrigin.Y), new Vector2(buttonX + buttonWidth, pillOrigin.Y + buttonHeight));
                    if (ui.GhostButton(discordRect, Loc.T(L.Rolladeck.Discord)))
                    {
                        Windows.UrlActions.AskThenOpen(venue.DiscordUrl);
                    }
                }
            }

            ImGui.SetCursorScreenPos(pillOrigin);
            ImGui.Dummy(new Vector2(width, buttonHeight + 16f * scale));

            if (venue.Amenities.Count > 0)
            {
                var amenityOrigin = ImGui.GetCursorScreenPos();
                var amenityLabel = Loc.T(L.Rolladeck.SectionAmenities);
                var amenityLabelSize = Typography.Measure(amenityLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(amenityOrigin.X + sidePadding, amenityOrigin.Y),
                    amenityLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(amenityOrigin);
                ImGui.Dummy(new Vector2(width, amenityLabelSize.Y + 6f * scale));

                var chipX = origin.X + sidePadding;
                var chipY = ImGui.GetCursorScreenPos().Y;
                var chipHeight = 24f * scale;
                var chipPadding = 10f * scale;
                var chipGapX = 6f * scale;
                var chipGapY = 6f * scale;
                var rowMaxX = origin.X + sidePadding + contentWidth;
                var currentChipX = chipX;
                var currentChipY = chipY;

                for (var amenityIndex = 0; amenityIndex < venue.Amenities.Count; amenityIndex++)
                {
                    var amenity = venue.Amenities[amenityIndex];
                    var amenitySize = Typography.Measure(amenity, TextStyles.Footnote);
                    var chipWidth = amenitySize.X + chipPadding * 2f;
                    if (currentChipX + chipWidth > rowMaxX && currentChipX > chipX)
                    {
                        currentChipX = chipX;
                        currentChipY += chipHeight + chipGapY;
                    }

                    var chipMin = new Vector2(currentChipX, currentChipY);
                    var chipMax = new Vector2(currentChipX + chipWidth, currentChipY + chipHeight);
                    Squircle.Stroke(drawList, chipMin, chipMax, 6f * scale, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
                    Typography.Draw(drawList,
                        new Vector2(chipMin.X + chipPadding, chipMin.Y + (chipHeight - amenitySize.Y) * 0.5f),
                        amenity, ui.Palette.BodyInk, TextStyles.Footnote);
                    currentChipX += chipWidth + chipGapX;
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, chipY));
                ImGui.Dummy(new Vector2(width, (currentChipY - chipY) + chipHeight + 14f * scale));
            }

            if (!string.IsNullOrEmpty(venue.Description))
            {
                var descriptionOrigin = ImGui.GetCursorScreenPos();
                var descriptionLabel = Loc.T(L.Rolladeck.SectionAbout);
                var descriptionLabelSize = Typography.Measure(descriptionLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(descriptionOrigin.X + sidePadding, descriptionOrigin.Y),
                    descriptionLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(descriptionOrigin);
                ImGui.Dummy(new Vector2(width, descriptionLabelSize.Y + 6f * scale));

                var textOrigin = ImGui.GetCursorScreenPos();
                var lines = Typography.WrapText(venue.Description, TextStyles.Subheadline, contentWidth);
                var lineHeight = Typography.Measure("A", TextStyles.Subheadline).Y;
                var lineY = textOrigin.Y;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Typography.Draw(drawList, new Vector2(textOrigin.X + sidePadding, lineY),
                        lines[lineIndex], ui.Palette.BodyInk, TextStyles.Subheadline);
                    lineY += lineHeight + 2f * scale;
                }

                ImGui.SetCursorScreenPos(textOrigin);
                ImGui.Dummy(new Vector2(width, lineY - textOrigin.Y + 16f * scale));
            }
        }

        EndPage(in frame, context, venue.DisplayName);
    }
}

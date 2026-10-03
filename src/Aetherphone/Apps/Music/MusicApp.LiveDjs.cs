using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Rolladeck;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int LiveDjHomeRows = 3;
    private const float LiveDjHomeRowHeight = 68f;
    private const float LiveDjCardHeight = 72f;
    private const float LiveDjVerticalPadding = 8f;
    private const float LiveDjAvatarRadius = 18f;

    private readonly List<LiveDjEntry> sortedDjs = new();
    private readonly List<LiveDjEntry> sortedDjsAtVenue = new();
    private readonly ChipRail liveDjChipRail = new();
    private readonly List<string> sortedDjHomeKeys = new();
    private readonly List<string> sortedDjHomeTitleKeys = new();
    private readonly List<string> sortedDjViewerCounts = new();
    private readonly List<string> sortedDjCardKeys = new();
    private readonly List<string> sortedDjCardTitleKeys = new();
    private readonly List<string> sortedDjsAtVenueCardKeys = new();
    private readonly List<string> sortedDjsAtVenueCardTitleKeys = new();
    private readonly List<string> sortedDjsAtVenueViewerCounts = new();
    private IReadOnlyList<LiveDjEntry> cachedDjSource = [];
    private bool liveDjLifestreamAvailable;
    private int liveDjFilter;

    private void OnLiveDjsOpened()
    {
        liveDjLifestreamAvailable = LifestreamBridge.IsAvailable();
    }

    private void EnsureDjList()
    {
        var source = rolladeck.LiveDJs;
        if (ReferenceEquals(source, cachedDjSource))
        {
            return;
        }

        cachedDjSource = source;
        sortedDjs.Clear();
        sortedDjsAtVenue.Clear();
        for (var djIndex = 0; djIndex < source.Count; djIndex++)
        {
            var dj = source[djIndex];
            sortedDjs.Add(dj);
            if (dj.VenueName != null)
            {
                sortedDjsAtVenue.Add(dj);
            }
        }

        sortedDjs.Sort(CompareDjs);
        sortedDjsAtVenue.Sort(CompareDjs);

        sortedDjHomeKeys.Clear();
        sortedDjHomeTitleKeys.Clear();
        sortedDjViewerCounts.Clear();
        sortedDjCardKeys.Clear();
        sortedDjCardTitleKeys.Clear();
        for (var keyIndex = 0; keyIndex < sortedDjs.Count; keyIndex++)
        {
            var slug = sortedDjs[keyIndex].DjSlug ?? sortedDjs[keyIndex].DjName;
            sortedDjHomeKeys.Add("music.liveDj.home." + slug);
            sortedDjHomeTitleKeys.Add("music.liveDj.home.title." + slug);
            sortedDjViewerCounts.Add(NumberText.Group(sortedDjs[keyIndex].ViewerCount));
            sortedDjCardKeys.Add("music.djCard." + slug);
            sortedDjCardTitleKeys.Add("music.djCard.title." + slug);
        }

        sortedDjsAtVenueCardKeys.Clear();
        sortedDjsAtVenueCardTitleKeys.Clear();
        sortedDjsAtVenueViewerCounts.Clear();
        for (var keyIndex = 0; keyIndex < sortedDjsAtVenue.Count; keyIndex++)
        {
            var slug = sortedDjsAtVenue[keyIndex].DjSlug ?? sortedDjsAtVenue[keyIndex].DjName;
            sortedDjsAtVenueCardKeys.Add("music.djCard." + slug);
            sortedDjsAtVenueCardTitleKeys.Add("music.djCard.title." + slug);
            sortedDjsAtVenueViewerCounts.Add(NumberText.Group(sortedDjsAtVenue[keyIndex].ViewerCount));
        }
    }

    private int CompareDjs(LiveDjEntry first, LiveDjEntry second)
    {
        var tierDifference = ProximityTier(first.Server, first.Datacenter)
                      .CompareTo(ProximityTier(second.Server, second.Datacenter));
        if (tierDifference != 0)
        {
            return tierDifference;
        }

        return second.ViewerCount.CompareTo(first.ViewerCount);
    }

    private int ProximityTier(string? server, string? datacenter)
    {
        var localId = gameData.LocalCurrentWorldId;
        var playerWorld = localId > 0 ? gameData.WorldName(localId) : null;
        var playerDataCenter = localId > 0 ? gameData.DataCenterName(localId) : null;

        if (!string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(playerWorld) &&
            string.Equals(server, playerWorld, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!string.IsNullOrEmpty(datacenter) && !string.IsNullOrEmpty(playerDataCenter) &&
            string.Equals(datacenter, playerDataCenter, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (!string.IsNullOrEmpty(datacenter) && !string.IsNullOrEmpty(playerDataCenter))
        {
            var djRegion = HousingRegions.For(datacenter);
            if (djRegion != HousingRegions.Unknown &&
                string.Equals(djRegion, HousingRegions.For(playerDataCenter), StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
        }

        return 3;
    }

    private void OpenLiveDjs()
    {
        Push(MusicRoute.Of(MusicScreen.LiveDjs));
    }

    private void OpenLiveDjDetail(LiveDjEntry dj)
    {
        selectedDj = dj;
        Push(MusicRoute.LiveDj(dj.DjSlug ?? dj.DjName, dj.NormalizedName));
    }

    private void OpenVenueDetail(OpenVenueEntry venue)
    {
        selectedVenue = venue;
        Push(MusicRoute.Venue(venue.Slug ?? venue.DisplayName, venue.DisplayName));
    }

    private OpenVenueEntry? FindVenueForDj(LiveDjEntry dj)
    {
        if (dj.VenueName == null)
        {
            return null;
        }

        var venues = rolladeck.OpenVenues;
        for (var venueIndex = 0; venueIndex < venues.Count; venueIndex++)
        {
            if (string.Equals(venues[venueIndex].DisplayName, dj.VenueName, StringComparison.OrdinalIgnoreCase))
            {
                return venues[venueIndex];
            }
        }

        return null;
    }

    private void DrawLiveDjsSkeleton(float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleSize = Typography.Measure("Ag", TextStyles.Title3);
        var fill = Skeleton.Fill();
        var drawList = ImGui.GetWindowDrawList();

        Squircle.Fill(drawList, origin, new Vector2(origin.X + width * 0.38f, origin.Y + titleSize.Y), 4f * scale, fill);
        ImGui.Dummy(new Vector2(0f, titleSize.Y + 8f * scale));

        var rowHeight = LiveDjHomeRowHeight * scale;
        var avatarRadius = 22f * scale;
        for (var rowIndex = 0; rowIndex < LiveDjHomeRows; rowIndex++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var avatarCenter = new Vector2(rowOrigin.X + 6f * scale + avatarRadius, rowOrigin.Y + rowHeight * 0.5f);
            drawList.AddCircleFilled(avatarCenter, avatarRadius, fill, 32);

            var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var nameY = rowOrigin.Y + 9f * scale;
            var nameLineHeight = Typography.Measure("Ag", TextStyles.FootnoteEmphasized).Y;
            var captionHeight = Typography.Measure("Ag", TextStyles.Caption1).Y;

            Squircle.Fill(drawList, new Vector2(textLeft, nameY),
                new Vector2(textLeft + width * 0.42f, nameY + nameLineHeight), 4f * scale, fill);
            Squircle.Fill(drawList, new Vector2(width * 0.62f, nameY),
                new Vector2(width * 0.88f, nameY + captionHeight), 3f * scale, fill);

            var line2Y = nameY + nameLineHeight + 3f * scale;
            Squircle.Fill(drawList, new Vector2(textLeft, line2Y),
                new Vector2(textLeft + width * 0.30f, line2Y + captionHeight), 3f * scale, fill);

            ImGui.Dummy(new Vector2(width, rowHeight));
        }
    }

    private void DrawLiveDjsEmpty(float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var title = Typography.FitText(Loc.T(L.Music.LiveDjs), width, TextStyles.Title3);
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(origin, title, ui.Palette.HeadingInk, TextStyles.Title3);
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var textOrigin = ImGui.GetCursorScreenPos();
        var textHeight = Typography.DrawWrappedLeft(textOrigin, Loc.T(L.Music.LiveDjsEmpty),
            ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, textHeight + 8f * scale));
    }

    private void DrawLiveDjsSection(float scale)
    {
        EnsureDjList();
        if (sortedDjs.Count == 0)
        {
            if (rolladeck.Loading)
            {
                DrawLiveDjsSkeleton(scale);
            }
            else if (rolladeck.HasData)
            {
                DrawLiveDjsEmpty(scale);
            }

            return;
        }

        if (SectionHeader.Draw(ui, Loc.T(L.Music.LiveDjs), true, 0f))
        {
            OpenLiveDjs();
        }

        var drawList = ImGui.GetWindowDrawList();

        var withVenueCount = 0;
        for (var djIndex = 0; djIndex < sortedDjs.Count; djIndex++)
        {
            var dj = sortedDjs[djIndex];
            if (dj.VenueName != null || dj.FormattedAddress.Length > 0)
            {
                withVenueCount++;
            }
        }

        var drawn = 0;
        for (var djIndex = 0; djIndex < sortedDjs.Count; djIndex++)
        {
            if (drawn >= LiveDjHomeRows)
            {
                break;
            }

            var dj = sortedDjs[djIndex];
            if (dj.VenueName != null || dj.FormattedAddress.Length > 0)
            {
                DrawLiveDjHomeRow(drawList, scale, dj, sortedDjHomeKeys[djIndex], sortedDjHomeTitleKeys[djIndex],
                    sortedDjViewerCounts[djIndex]);
                drawn++;
            }
        }

        if (withVenueCount < 3)
        {
            for (var djIndex = 0; djIndex < sortedDjs.Count; djIndex++)
            {
                if (drawn >= LiveDjHomeRows)
                {
                    break;
                }

                var dj = sortedDjs[djIndex];
                if (dj.VenueName == null && dj.FormattedAddress.Length == 0)
                {
                    DrawLiveDjHomeRow(drawList, scale, dj, sortedDjHomeKeys[djIndex], sortedDjHomeTitleKeys[djIndex],
                        sortedDjViewerCounts[djIndex]);
                    drawn++;
                }
            }
        }
    }

    private void DrawLiveDjHomeRow(ImDrawListPtr drawList, float scale, LiveDjEntry dj, string rowKey, string titleKey,
        string viewerCount)
    {
        var rowHeight = LiveDjHomeRowHeight * scale;
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var hovered = cell.Hovered;
        var avatarRadius = 22f * scale;
        var avatarCenter = new Vector2(min.X + 6f * scale + avatarRadius, (min.Y + max.Y) * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
            dj.DjName, dj.Server ?? string.Empty, dj.AvatarUrl, images, lodestone, 0.85f, 44);

        var viewersText = string.Format(Loc.T(L.Rolladeck.Viewers), viewerCount);
        var viewerSize = Typography.Measure(viewersText, TextStyles.Caption1);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var rightReserve = viewerSize.X + 14f * scale;
        var textWidth = max.X - rightReserve - textLeft;
        var nameY = min.Y + 9f * scale;
        var nameSize = Typography.Measure("Ag", TextStyles.FootnoteEmphasized);
        var captionSize = Typography.Measure("Ag", TextStyles.Caption1);

        Marquee.DrawLeft(rowKey,
            dj.NormalizedName, textLeft, nameY, textWidth, TextStyles.FootnoteEmphasized, ui.TitleInk, hovered);

        Typography.Draw(drawList,
            new Vector2(max.X - 12f * scale - viewerSize.X, nameY + (nameSize.Y - captionSize.Y) * 0.5f),
            viewersText, ui.Accent, TextStyles.Caption1);

        var line2Y = nameY + nameSize.Y + 3f * scale;
        var venueLabel = dj.VenueName ?? dj.FormattedAddress;
        if (!string.IsNullOrEmpty(venueLabel))
        {
            var fitted = Typography.FitText(venueLabel, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, line2Y), fitted, ui.MutedInk, TextStyles.Caption1);
        }

        if (!string.IsNullOrEmpty(dj.ServerLabel))
        {
            var serverSize = Typography.Measure(dj.ServerLabel, TextStyles.Caption1);
            Typography.Draw(drawList,
                new Vector2(max.X - 12f * scale - serverSize.X, line2Y),
                dj.ServerLabel, ui.MutedInk, TextStyles.Caption1);
        }

        if (!string.IsNullOrEmpty(dj.NormalizedTitle))
        {
            var line3Y = line2Y + captionSize.Y + 2f * scale;
            Marquee.DrawLeft(titleKey,
                dj.NormalizedTitle, textLeft, line3Y, textWidth,
                TextStyles.Caption1, ui.MutedInk, hovered);
        }

        if (cell.Tapped)
        {
            OpenLiveDjDetail(dj);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawLiveDjs(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        var body = frame.Body;
        EnsureDjList();

        var djs = liveDjFilter == 0 ? sortedDjsAtVenue : sortedDjs;
        var djCardKeys = liveDjFilter == 0 ? sortedDjsAtVenueCardKeys : sortedDjCardKeys;
        var djCardTitleKeys = liveDjFilter == 0 ? sortedDjsAtVenueCardTitleKeys : sortedDjCardTitleKeys;
        var djViewerCounts = liveDjFilter == 0 ? sortedDjsAtVenueViewerCounts : sortedDjViewerCounts;

        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawLiveDjFilter(scale);
            var placeholder = new Rect(new Vector2(body.Min.X, ImGui.GetCursorScreenPos().Y), Unobstructed(body).Max);
            if (rolladeck.Loading && !rolladeck.HasData)
            {
                LoadingPulse.Draw(placeholder.Center, 13f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            }
            else if (djs.Count == 0)
            {
                Typography.DrawCentered(placeholder.Center, Loc.T(L.Rolladeck.EmptyDjsHeading), ui.MutedInk,
                    TextStyles.Callout);
            }
            else
            {
                DrawLiveDjRows(scale, djs, djCardKeys, djCardTitleKeys, djViewerCounts);
            }
        }

        EndPage(in frame, context, Loc.T(L.Music.LiveDjs));
    }

    private void DrawLiveDjFilter(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = (ChipRail.RowHeight + 8f) * scale;
        var inset = MusicUi.Inset * scale;
        var chipRect = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset, origin.Y + height));
        ReadOnlySpan<string> filterLabels = [Loc.T(L.Rolladeck.FilterAtVenue), Loc.T(L.Rolladeck.FilterAllDjs)];
        ReadOnlySpan<bool> filterActive = [liveDjFilter == 0, liveDjFilter == 1];
        var filterTapped = liveDjChipRail.Draw(chipRect, ui, filterLabels, filterActive, overlay: false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (filterTapped >= 0)
        {
            liveDjFilter = filterTapped;
        }
    }

    private void DrawLiveDjRows(float scale, List<LiveDjEntry> djs, List<string> djCardKeys,
        List<string> djCardTitleKeys, List<string> djViewerCounts)
    {
        var drawList = ImGui.GetWindowDrawList();
        ImGui.Dummy(new Vector2(1f, LiveDjVerticalPadding * scale));
        for (var djIndex = 0; djIndex < djs.Count; djIndex++)
        {
            var cell = FeedCell.Begin(drawList, LiveDjCardHeight * scale, ui.HoverWash);
            if (ImGui.IsRectVisible(cell.Bounds.Min, cell.Bounds.Max))
            {
                DrawLiveDjCard(drawList, cell.Bounds, cell.Hovered, djs[djIndex], djCardKeys[djIndex],
                    djCardTitleKeys[djIndex], djViewerCounts[djIndex], scale);
            }

            if (cell.Tapped)
            {
                OpenLiveDjDetail(djs[djIndex]);
            }

            FeedCell.End(drawList, cell, ui.Hairline);
        }

        ImGui.Dummy(new Vector2(1f, 10f * scale));
        var footerCursor = ImGui.GetCursorScreenPos();
        var footerWidth = ImGui.GetContentRegionAvail().X;
        DrawRolladeckFooter(drawList, scale, footerWidth, footerCursor);
        ImGui.Dummy(new Vector2(footerWidth, 22f * scale + 14f * scale));
    }

    private void DrawLiveDjCard(ImDrawListPtr drawList, Rect card, bool hovered, LiveDjEntry dj, string cardKey,
        string titleKey, string viewerCount, float scale)
    {
        var padding = FeedCell.PadX * scale;
        var avatarRadius = LiveDjAvatarRadius * scale;
        var avatarCenter = new Vector2(card.Min.X + padding + avatarRadius,
                                       card.Min.Y + card.Height * 0.5f);

        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
            dj.DjName, dj.Server ?? string.Empty, dj.AvatarUrl, images, lodestone, 0.85f, 44);

        var textLeft = avatarCenter.X + avatarRadius + 8f * scale;
        var topY = card.Min.Y + 9f * scale;
        var maxTextWidth = card.Max.X - textLeft - 80f * scale;

        Marquee.DrawLeft(cardKey,
            dj.NormalizedName, textLeft, topY, maxTextWidth,
            TextStyles.SubheadlineEmphasized, ui.Palette.TitleInk, hovered);

        var nameHeight = Typography.Measure("Ag", TextStyles.SubheadlineEmphasized).Y;
        var currentY = topY + nameHeight + 3f * scale;

        var subLabel = dj.VenueName ?? dj.FormattedAddress;
        if (!string.IsNullOrEmpty(subLabel))
        {
            var subLabelHeight = Typography.Measure("Ag", TextStyles.Footnote).Y;
            var fittedSubLabel = Typography.FitText(subLabel, maxTextWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(textLeft, currentY), fittedSubLabel, ui.Palette.BodyInk, TextStyles.Footnote);
            currentY += subLabelHeight + 2f * scale;
        }

        if (!string.IsNullOrEmpty(dj.NormalizedTitle))
        {
            Marquee.DrawLeft(titleKey,
                dj.NormalizedTitle, textLeft, currentY, maxTextWidth,
                TextStyles.Caption1, ui.Palette.MutedInk, hovered);
        }

        var viewersText = string.Format(Loc.T(L.Rolladeck.Viewers), viewerCount);
        var viewerSize = Typography.Measure(viewersText, TextStyles.Caption1);
        var viewersRightX = card.Max.X - padding - viewerSize.X;
        Typography.Draw(drawList,
            new Vector2(viewersRightX, card.Min.Y + 9f * scale),
            viewersText, ui.Palette.Accent, TextStyles.Caption1);

        if (!string.IsNullOrEmpty(dj.ServerLabel))
        {
            var serverSize = Typography.Measure(dj.ServerLabel, TextStyles.Caption1);
            Typography.Draw(drawList,
                new Vector2(card.Max.X - padding - serverSize.X, card.Min.Y + 9f * scale + viewerSize.Y + 3f * scale),
                dj.ServerLabel, ui.Palette.MutedInk, TextStyles.Caption1);
        }
    }

    private void DrawRolladeckFooter(ImDrawListPtr drawList, float scale, float rowWidth, Vector2 rowOrigin)
    {
        var radius = 11f * scale;
        var gap = 7f * scale;
        var label = Loc.T(L.Music.PoweredByRolladeck);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var externalIconWidth = 14f * scale;
        var totalWidth = radius * 2f + gap + labelSize.X + gap + externalIconWidth;
        var rowLeft = rowOrigin.X + (rowWidth - totalWidth) * 0.5f;
        var rowCenterY = rowOrigin.Y + radius;
        var iconCenter = new Vector2(rowLeft + radius, rowCenterY);
        var accent = AppAccents.For("rolladeck");

        if (!AppIconTile.TryDrawGlyph(drawList, "rolladeck", iconCenter, radius * 2f * AppIconTextures.GlyphFraction,
                Vector4.One))
        {
            drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(accent), 24);
        }

        var textX = rowLeft + radius * 2f + gap;
        var textY = rowCenterY - labelSize.Y * 0.5f;
        Typography.Draw(drawList, new Vector2(textX, textY), label, ui.MutedInk, TextStyles.Caption1);

        AppSkin.Icon(drawList,
            new Vector2(textX + labelSize.X + gap + externalIconWidth * 0.5f, rowCenterY),
            IconGlyph.Of(FontAwesomeIcon.Globe), accent, 0.65f);

        var rowMin = new Vector2(rowLeft, rowOrigin.Y);
        var rowMax = new Vector2(rowLeft + totalWidth, rowOrigin.Y + radius * 2f);
        var hovered = UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rowMin, rowMax, hovered))
        {
            Windows.UrlActions.AskThenOpen("https://xivrolladeck.com");
        }
    }
}

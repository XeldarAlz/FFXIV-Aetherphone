using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float HeroAspect = 0.56f;
    private const float HeroMaxHeight = 260f;
    private const float HeroScrimShare = 0.40f;
    private const float HeroInset = 14f;
    private const float DetailLogoSide = 68f;
    private const float DetailLogoGap = 12f;
    private const float ActionBarHeight = 74f;
    private const float CtaHeight = 48f;
    private const float InfoRowHeight = 50f;
    private const float InfoTileSide = 28f;
    private const float NowPlayingIconRadius = 20f;
    private const float TwitchButtonHeight = 38f;
    private const float TitleFadeDistance = 44f;
    private const int MaxLinkRows = 6;

    private static readonly TextStyle DetailTitleStyle = TextStyles.Title2;
    private static readonly TextStyle DetailMetaStyle = TextStyles.Subheadline;
    private static readonly TextStyle DetailStatusStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle FadedTitleStyle = new(1.13f, FontWeight.SemiBold);
    private static readonly TextStyle InfoLabelStyle = TextStyles.Subheadline;
    private static readonly TextStyle InfoValueStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle CaptionStyle = TextStyles.Caption1;
    private static readonly TextStyle HintStyle = TextStyles.Footnote;
    private static readonly TextStyle StreamTitleStyle = TextStyles.Subheadline;
    private static readonly Vector4 WhenTint = new(0.95f, 0.58f, 0.20f, 1f);
    private static readonly Vector4 WorldTint = new(0.35f, 0.55f, 0.95f, 1f);
    private static readonly Vector4 LocationTint = new(0.30f, 0.75f, 0.45f, 1f);
    private static readonly Vector4 HostTint = new(0.62f, 0.45f, 0.92f, 1f);

    private readonly LinkRow[] linkRows = new LinkRow[MaxLinkRows];
    private VenueEvent? detailVenue;
    private int detailVersion = -1;
    private long detailMinute = -1;
    private DetailText detailText;
    private float detailScrollY;

    private readonly record struct LinkRow(string Label, string Glyph, string Url);

    private readonly record struct DetailText(VenueStatus Status, string Meta, string Window, string Viewers,
        string Initial, string HostLine);

    private VenueEvent ResolveDetail(VenueEvent routed)
    {
        var nowUtc = DateTime.UtcNow;
        var minute = nowUtc.Ticks / TimeSpan.TicksPerMinute;
        CheckLanguage();
        if (detailVenue is not null && string.Equals(detailVenue.Id, routed.Id, StringComparison.Ordinal) &&
            detailVersion == venues.Version && detailMinute == minute)
        {
            return detailVenue;
        }

        var resolved = routed;
        var events = venues.Events;
        for (var index = 0; index < events.Count; index++)
        {
            if (string.Equals(events[index].Id, routed.Id, StringComparison.Ordinal))
            {
                resolved = events[index];
                break;
            }
        }

        detailVenue = resolved;
        CollectDetailDjs(resolved.Id);
        detailVersion = venues.Version;
        detailMinute = minute;
        detailText = new DetailText(VenueFormat.Status(resolved, nowUtc), VenueFormat.Meta(resolved),
            VenueFormat.Window(resolved),
            resolved.LiveViewers > 0 ? VenueFormat.Viewers(resolved.LiveViewers) : string.Empty,
            VenueLabelCache.InitialOf(resolved.Title),
            resolved.Host.Length > 0 ? Loc.T(L.Venues.HostedBy, resolved.Host) : string.Empty);
        return resolved;
    }

    private void DrawDetail(Rect area, VenueEvent routed)
    {
        var venue = ResolveDetail(routed);
        var scale = UiScale.Current;
        var hasBar = venue.CanTeleport;
        var top = area.Min.Y + AppHeader.Height * scale;
        var bottom = hasBar ? area.Max.Y - ActionBarHeight * scale : area.Max.Y;
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, bottom));
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            detailScrollY = ImGui.GetScrollY();
            DrawDetailHero(venue, scale);
            DrawDetailTitle(venue, scale);
            DrawNowPlaying(venue, scale);
            DrawAbout(venue, scale);
            DrawInfoCard(venue, scale);
            DrawLinks(venue, scale);
            DrawDetailTags(venue, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }

        DrawDetailHeader(area, venue, scale);
        if (hasBar)
        {
            DrawActionBar(new Rect(new Vector2(area.Min.X, bottom), area.Max), venue, scale);
        }
    }

    private void DrawDetailHeader(Rect area, VenueEvent venue, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        SocialChrome.DrawScreenHeader(area, string.Empty, Ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(1), string.Empty, true, true);
        var fadeStart = DetailHeroHeight(area.Width, scale) - 24f * scale;
        var titleAlpha = Math.Clamp((detailScrollY - fadeStart) / (TitleFadeDistance * scale), 0f, 1f);
        if (titleAlpha > 0f)
        {
            var reserve = (CellPadX + SocialChrome.HeaderIconPitch + SocialChrome.BackChipRadius) * scale;
            var title = Typography.FitText(venue.Title, MathF.Max(1f, area.Width - reserve * 2f), FadedTitleStyle);
            Typography.DrawCentered(drawList,
                new Vector2(area.Center.X, area.Min.Y + AppHeader.Height * scale * 0.5f), title,
                Palette.WithAlpha(Ink.TitleInk, titleAlpha), FadedTitleStyle);
            FeedCell.Hairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale,
                Palette.WithAlpha(Ink.Hairline, Ink.Hairline.W * titleAlpha));
        }

        var favorite = IsFavorite(venue.Id);
        var favoriteCenter = SocialChrome.HeaderSlot(area, 0);
        var favoriteReach = new Vector2(SocialChrome.HeaderIconRadius * scale, SocialChrome.HeaderIconRadius * scale);
        UiAnchors.Report("venues.detail.favorite",
            new Rect(favoriteCenter - favoriteReach, favoriteCenter + favoriteReach));
        if (DrawHeaderIcon(drawList, favoriteCenter,
                favorite ? PhoneIcons.StarFilled : PhoneIcons.Star, Loc.T(L.Venues.Favorites), favorite))
        {
            ToggleFavorite(venue.Id);
        }
    }

    private static float DetailHeroHeight(float width, float scale) =>
        MathF.Min(width * HeroAspect, HeroMaxHeight * scale);

    private void DrawDetailHero(VenueEvent venue, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = DetailHeroHeight(width, scale);
        var hero = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        VenueImage.Cover(drawList, hero, 0f, venue, detailText.Initial, Art);
        MediaOverlay.BottomScrim(drawList, hero.Min, hero.Max, HeroScrimShare);
        var inset = HeroInset * scale;
        VenueCard.DrawStatusPill(drawList, new Vector2(hero.Min.X + inset, hero.Min.Y + inset),
            detailText.Status.Kind, venue, hero.Max.X - inset, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawDetailTitle(VenueEvent venue, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var left = origin.X + pad;
        var textLeft = left;
        var hasLogo = venue.LogoUrl is not null && venue.BannerUrl is not null;
        var logoBottom = origin.Y;
        if (hasLogo)
        {
            var side = DetailLogoSide * scale;
            var logoTop = origin.Y - side * 0.5f;
            var logo = new Rect(new Vector2(left, logoTop), new Vector2(left + side, logoTop + side));
            var rim = 3f * scale;
            Squircle.Fill(drawList, logo.Min - new Vector2(rim, rim), logo.Max + new Vector2(rim, rim),
                side * 0.26f + rim, ImGui.GetColorU32(Ink.BackdropTop));
            VenueImage.Logo(drawList, logo, side * 0.26f, venue, detailText.Initial, Art);
            textLeft = logo.Max.X + DetailLogoGap * scale;
            logoBottom = logo.Max.Y;
        }

        var cursorY = origin.Y + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), venue.Title, Ink.TitleInk,
            DetailTitleStyle, textWidth);
        ImGui.SetCursorScreenPos(origin);
        if (detailText.Meta.Length > 0)
        {
            cursorY += 2f * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), detailText.Meta, Ink.MutedInk,
                DetailMetaStyle, textWidth);
            ImGui.SetCursorScreenPos(origin);
        }

        cursorY = MathF.Max(cursorY, logoBottom);
        if (detailText.Status.Label.Length > 0)
        {
            cursorY += Metrics.Space.Sm * scale;
            var statusLeft = left;
            var tint = VenueCard.StatusTint(detailText.Status.Kind, Ink);
            var statusHeight = Typography.LineHeight(DetailStatusStyle);
            if (detailText.Status.Kind == VenueStatusKind.Live)
            {
                MediaOverlay.LiveDot(drawList, new Vector2(statusLeft + 5f * scale, cursorY + statusHeight * 0.5f),
                    tint, scale);
                statusLeft += 16f * scale;
            }

            Typography.Draw(drawList, new Vector2(statusLeft, cursorY),
                Typography.FitText(detailText.Status.Label, MathF.Max(1f, origin.X + width - pad - statusLeft),
                    DetailStatusStyle), tint, DetailStatusStyle);
            cursorY += statusHeight;
        }

        if (detailText.Status.Kind == VenueStatusKind.Open)
        {
            cursorY += 2f * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.Venues.ScheduledOpen),
                Ink.FaintInk, HintStyle, width - pad * 2f);
            ImGui.SetCursorScreenPos(origin);
        }

        if (detailText.HostLine.Length > 0)
        {
            cursorY += 3f * scale;
            Typography.Draw(drawList, new Vector2(left, cursorY),
                Typography.FitText(detailText.HostLine, width - pad * 2f, HintStyle), Ink.MutedInk, HintStyle);
            cursorY += Typography.LineHeight(HintStyle);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + Metrics.Space.Md * scale));
    }

    private void DrawNowPlaying(VenueEvent venue, float scale)
    {
        if (!venue.IsConfirmedLive(DateTime.UtcNow))
        {
            return;
        }

        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.NowPlaying)), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var inner = CardInset * scale;
        var cardLeft = origin.X + pad;
        var cardRight = origin.X + width - pad;
        var radius = NowPlayingIconRadius * scale;
        var headlineHeight = Typography.LineHeight(InfoValueStyle);
        var captionHeight = Typography.LineHeight(CaptionStyle);
        var rowHeight = MathF.Max(radius * 2f, headlineHeight + 2f * scale + captionHeight);
        var djCount = detailDjs.Count;
        var djRowHeight = DjRowHeight * scale;
        var topBlock = djCount > 0 ? inner * 0.5f + djCount * djRowHeight : inner + rowHeight;
        var hasTwitch = !string.IsNullOrEmpty(venue.TwitchUrl);
        var fullWidth = MathF.Max(1f, cardRight - cardLeft - inner * 2f);
        var titleHeight = venue.LiveTitle.Length > 0
            ? Typography.CountWrappedLines(venue.LiveTitle, StreamTitleStyle, fullWidth) *
              Typography.LineHeight(StreamTitleStyle)
            : 0f;
        var genresHeight = venue.LiveGenres.Count > 0 ? VenueChips.Height(scale) : 0f;
        var cardHeight = topBlock + inner +
                         (titleHeight > 0f ? Metrics.Space.Sm * scale + titleHeight : 0f) +
                         (genresHeight > 0f ? Metrics.Space.Sm * scale + genresHeight : 0f) +
                         (hasTwitch ? Metrics.Space.Md * scale + TwitchButtonHeight * scale : 0f);
        ui.Card(drawList, new Vector2(cardLeft, origin.Y), new Vector2(cardRight, origin.Y + cardHeight),
            Metrics.Radius.Card * scale, elevated: true);
        if (djCount > 0)
        {
            var linkable = djCount > 1;
            for (var index = 0; index < djCount; index++)
            {
                var rowTop = origin.Y + inner * 0.5f + index * djRowHeight;
                DrawDetailDjRow(drawList, new Vector2(cardLeft, rowTop), new Vector2(cardRight, rowTop + djRowHeight),
                    index, linkable, inner, scale);
            }
        }
        else
        {
            DrawNowPlayingHeadline(drawList, venue, cardLeft, cardRight, origin.Y, rowHeight, scale);
        }

        var cursorY = origin.Y + topBlock;
        if (titleHeight > 0f)
        {
            cursorY += Metrics.Space.Sm * scale;
            Typography.DrawWrappedLeft(new Vector2(cardLeft + inner, cursorY), venue.LiveTitle, Ink.BodyInk,
                StreamTitleStyle, fullWidth);
            ImGui.SetCursorScreenPos(origin);
            cursorY += titleHeight;
        }

        if (genresHeight > 0f)
        {
            cursorY += Metrics.Space.Sm * scale;
            VenueCard.DrawChipRow(drawList, venue.LiveGenres, cardLeft + inner, cardRight - inner, cursorY, scale);
            cursorY += genresHeight;
        }

        if (hasTwitch)
        {
            var buttonTop = cursorY + Metrics.Space.Md * scale;
            var button = new Rect(new Vector2(cardLeft + inner, buttonTop),
                new Vector2(cardRight - inner, buttonTop + TwitchButtonHeight * scale));
            if (SocialPill.Accent(drawList, button, Loc.T(L.Venues.WatchOnTwitch), Ink,
                    TextStyles.SubheadlineEmphasized, button.Height * 0.5f))
            {
                UrlActions.AskThenOpen(venue.TwitchUrl!);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Xs * scale));
        DrawHint(Loc.T(L.Venues.ConfirmedLive), scale);
    }

    private void DrawNowPlayingHeadline(ImDrawListPtr drawList, VenueEvent venue, float cardLeft, float cardRight,
        float top, float rowHeight, float scale)
    {
        var inner = CardInset * scale;
        var radius = NowPlayingIconRadius * scale;
        var headlineHeight = Typography.LineHeight(InfoValueStyle);
        var captionHeight = Typography.LineHeight(CaptionStyle);
        var iconCenter = new Vector2(cardLeft + inner + radius, top + inner + rowHeight * 0.5f);
        drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.LiveGreen, 0.18f)),
            32);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Microphone, MediaOverlay.LiveGreen, 20f * scale);
        var textLeft = iconCenter.X + radius + 12f * scale;
        var textWidth = MathF.Max(1f, cardRight - inner - textLeft);
        var textTop = top + inner + (rowHeight - headlineHeight - 2f * scale - captionHeight) * 0.5f;
        var headline = venue.LiveHeadline.Length > 0 ? venue.LiveHeadline : Loc.T(L.Venues.LiveNowLabel);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.detail.playing.", venue.Id), headline, textLeft, textTop,
            textWidth, InfoValueStyle, Ink.TitleInk);
        var caption = detailText.Viewers.Length > 0 ? detailText.Viewers : Loc.T(L.Venues.SourceRolladeck);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + headlineHeight + 2f * scale),
            Typography.FitText(caption, textWidth, CaptionStyle), Ink.MutedInk, CaptionStyle);
    }

    private void DrawHint(string text, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y), text, Ink.FaintInk, HintStyle,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawDetailTags(VenueEvent venue, float scale)
    {
        if (venue.Tags.Count == 0)
        {
            return;
        }

        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Tags)), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X + CellPadX * scale;
        var right = origin.X + width - CellPadX * scale;
        var gap = Metrics.Space.Xs * scale;
        var lineHeight = VenueChips.Height(scale) + gap;
        var cursorX = left;
        var cursorY = origin.Y;
        for (var index = 0; index < venue.Tags.Count; index++)
        {
            var tag = venue.Tags[index];
            var chipWidth = VenueChips.Measure(tag, scale);
            if (cursorX + chipWidth > right && cursorX > left)
            {
                cursorX = left;
                cursorY += lineHeight;
            }

            VenueChips.Draw(drawList, new Vector2(cursorX, cursorY), tag, scale);
            cursorX += chipWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + lineHeight));
    }

    private string VenueLanguage(VenueEvent venue)
    {
        if (!venueLanguages.TryGetValue(venue.Id, out var language))
        {
            language = LanguageGuess.Detect(venue.Description);
            venueLanguages[venue.Id] = language;
        }

        return language;
    }

    private void DrawAbout(VenueEvent venue, float scale)
    {
        if (venue.Description.Length == 0)
        {
            return;
        }

        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.About)), scale);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var venueKey = new TranslationKey(TranslationSurface.Venue, venue.Id);
        var aboutText = translation.View(venueKey, venue.Description).Text;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + pad, origin.Y));
        using (Plugin.Fonts.Push(1f))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink.BodyInk))
        {
            ImGui.PushTextWrapPos(origin.X + width - pad - ImGui.GetWindowPos().X);
            Typography.Plain(aboutText);
            ImGui.PopTextWrapPos();
        }

        var language = VenueLanguage(venue);
        var linkHeight = TranslateLink.Height(translation, venueKey, language, scale);
        if (linkHeight > 0f)
        {
            var linkTop = new Vector2(origin.X + pad, ImGui.GetCursorScreenPos().Y);
            TranslateLink.Draw(translation, confirm, venueKey, language, venue.Description, linkTop,
                width - pad * 2f, Ink.MutedInk, Ink.Accent, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, linkTop.Y + linkHeight));
        }
        else
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X, ImGui.GetCursorScreenPos().Y));
        }

        ImGui.Dummy(new Vector2(width, Metrics.Space.Sm * scale));
    }

    private void DrawInfoCard(VenueEvent venue, float scale)
    {
        var rows = 0;
        rows += detailText.Window.Length > 0 ? 1 : 0;
        rows += venue.World.Length > 0 || venue.DataCenter.Length > 0 ? 1 : 0;
        rows += venue.LocationLine.Length > 0 && !string.Equals(venue.LocationLine, venue.World, StringComparison.Ordinal) ? 1 : 0;
        rows += venue.AttendeeCount > 0 ? 1 : 0;
        if (rows == 0)
        {
            return;
        }

        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Details)), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + rows * InfoRowHeight * scale));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, elevated: true);
        var rowIndex = 0;
        if (detailText.Window.Length > 0)
        {
            InfoRow(drawList, card, rowIndex++, rows, PhoneIcons.Clock, WhenTint, Loc.T(L.Venues.NextOpening),
                detailText.Window, scale);
        }

        if (venue.World.Length > 0 || venue.DataCenter.Length > 0)
        {
            var label = venue.World.Length > 0 ? Loc.T(L.Venues.World) : Loc.T(L.Venues.DataCenter);
            InfoRow(drawList, card, rowIndex++, rows, PhoneIcons.World, WorldTint, label,
                venue.World.Length > 0 ? venue.World : venue.DataCenter, scale);
        }

        if (venue.LocationLine.Length > 0 && !string.Equals(venue.LocationLine, venue.World, StringComparison.Ordinal))
        {
            InfoRow(drawList, card, rowIndex++, rows, PhoneIcons.MapPin, LocationTint, Loc.T(L.Venues.Location),
                venue.LocationLine, scale);
        }

        if (venue.AttendeeCount > 0)
        {
            InfoRow(drawList, card, rowIndex, rows, PhoneIcons.Users, HostTint, Loc.T(L.Venues.Attendees),
                venue.AttendeeCount.ToString(Loc.Culture), scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height + Metrics.Space.Sm * scale));
    }

    private void InfoRow(ImDrawListPtr drawList, Rect card, int rowIndex, int rowCount, string glyph, Vector4 tint,
        string label, string value, float scale)
    {
        var rowHeight = InfoRowHeight * scale;
        var rowTop = card.Min.Y + rowIndex * rowHeight;
        var centerY = rowTop + rowHeight * 0.5f;
        var inset = 14f * scale;
        var tileSide = InfoTileSide * scale;
        var tileMin = new Vector2(card.Min.X + inset, centerY - tileSide * 0.5f);
        Squircle.Fill(drawList, tileMin, tileMin + new Vector2(tileSide, tileSide), 8f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.22f)));
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tileSide * 0.5f, tileSide * 0.5f), glyph,
            Palette.Lighten(tint, 0.25f), 16f * scale);
        var labelLeft = tileMin.X + tileSide + 12f * scale;
        var labelSize = Typography.Measure(label, InfoLabelStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), label, Ink.MutedInk,
            InfoLabelStyle);
        var valueRight = card.Max.X - inset;
        var valueWidth = MathF.Max(1f, valueRight - labelLeft - labelSize.X - 12f * scale);
        var valueHeight = Typography.LineHeight(InfoValueStyle);
        Marquee.DrawRightAuto(new MarqueeId("venues.detail.row.", label), value, valueRight,
            centerY - valueHeight * 0.5f, valueWidth, InfoValueStyle, Ink.TitleInk);
        if (rowIndex < rowCount - 1)
        {
            FeedCell.Hairline(drawList, labelLeft, valueRight, rowTop + rowHeight, Ink.Hairline);
        }
    }

    private int CollectLinks(VenueEvent venue)
    {
        var count = 0;
        AddLink(ref count, Loc.T(L.Venues.Website), PhoneIcons.Link, venue.WebsiteUrl);
        AddLink(ref count, Loc.T(L.Venues.Discord), PhoneIcons.MessageCircle, venue.DiscordUrl);
        var listing = (venue.Sources & VenueSources.Partake) != 0
            ? Loc.T(L.Venues.SourcePartake)
            : Loc.T(L.Venues.SourceFfxiv);
        AddLink(ref count, listing, PhoneIcons.ExternalLink, venue.ListingUrl);
        AddLink(ref count, Loc.T(L.Venues.SourceRolladeck), PhoneIcons.ExternalLink, venue.RolladeckUrl);
        return count;
    }

    private void AddLink(ref int count, string label, string glyph, string? url)
    {
        if (string.IsNullOrEmpty(url) || count >= linkRows.Length)
        {
            return;
        }

        linkRows[count++] = new LinkRow(label, glyph, url);
    }

    private void DrawLinks(VenueEvent venue, float scale)
    {
        var count = CollectLinks(venue);
        if (count == 0)
        {
            return;
        }

        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.ListedOn)), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rowHeight = InfoRowHeight * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + count * rowHeight));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var inset = 14f * scale;
        for (var index = 0; index < count; index++)
        {
            var link = linkRows[index];
            var rowMin = new Vector2(card.Min.X, card.Min.Y + index * rowHeight);
            var rowMax = new Vector2(card.Max.X, rowMin.Y + rowHeight);
            var hovered = UiInteract.Hover(rowMin, rowMax);
            if (hovered)
            {
                var hoverRounding = index == 0 || index == count - 1 ? rounding : 0f;
                drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(Ink.HoverTint), hoverRounding,
                    index == 0 ? ImDrawFlags.RoundCornersTop :
                    index == count - 1 ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var centerY = rowMin.Y + rowHeight * 0.5f;
            PhoneIcon.Draw(drawList, new Vector2(rowMin.X + inset + 9f * scale, centerY), link.Glyph, Ink.AccentLink,
                18f * scale);
            var labelLeft = rowMin.X + inset + 30f * scale;
            var labelHeight = Typography.LineHeight(InfoValueStyle);
            Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelHeight * 0.5f),
                Typography.FitText(link.Label, MathF.Max(1f, rowMax.X - inset - 20f * scale - labelLeft),
                    InfoValueStyle), Ink.TitleInk, InfoValueStyle);
            PhoneIcon.Draw(drawList, new Vector2(rowMax.X - inset - 6f * scale, centerY), PhoneIcons.ChevronRight,
                Ink.FaintInk, 14f * scale);
            if (index < count - 1)
            {
                FeedCell.Hairline(drawList, labelLeft, rowMax.X - inset, rowMax.Y, Ink.Hairline);
            }

            if (UiInteract.Click(rowMin, rowMax, hovered))
            {
                UrlActions.AskThenOpen(link.Url);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height + Metrics.Space.Sm * scale));
    }

    private void DrawActionBar(Rect bar, VenueEvent venue, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        SocialChrome.PaintBarBackdrop(ui, drawList, bar, screenRect);
        FeedCell.Hairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 0.5f, Ink.Hairline);
        var pad = CellPadX * scale;
        var top = bar.Min.Y + (bar.Height - CtaHeight * scale) * 0.5f;
        var button = new Rect(new Vector2(bar.Min.X + pad, top), new Vector2(bar.Max.X - pad, top + CtaHeight * scale));
        UiAnchors.Report("venues.detail.go", button);
        if (SocialPill.Accent(drawList, button, Loc.T(L.Travel.GoThere), Ink, TextStyles.Headline,
                button.Height * 0.5f))
        {
            Teleport(venue);
        }
    }
}

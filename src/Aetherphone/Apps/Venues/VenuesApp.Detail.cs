using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
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
    private const float HeroMaxHeight = 240f;
    private const float HeroInset = 14f;
    private const float HeroLogoSide = 56f;
    private const float HeroLogoRim = 3f;
    private const float BlockGap = 12f;
    private const float StatusDotSpace = 16f;
    private const float ActionTileHeight = 58f;
    private const float ActionTileGap = 8f;
    private const float ActionTileRadius = 14f;
    private const float ActionGlyph = 20f;
    private const float ActionLabelGap = 4f;
    private const float InfoRowHeight = 50f;
    private const float InfoTileSide = 28f;
    private const float NowPlayingIconRadius = 20f;
    private const float TwitchButtonHeight = Button.RegularHeight;
    private const float CardInset = 14f;
    private const float ChipLineGap = 6f;
    private const int MaxLinkRows = 2;
    private const int MaxActionTiles = 4;

    private static readonly TextStyle MetaStyle = TextStyles.Subheadline;
    private static readonly TextStyle StatusStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle HintStyle = TextStyles.Footnote;
    private static readonly TextStyle InfoLabelStyle = TextStyles.Subheadline;
    private static readonly TextStyle InfoValueStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle CaptionStyle = TextStyles.Caption1;
    private static readonly TextStyle StreamTitleStyle = TextStyles.Subheadline;
    private static readonly TextStyle ActionLabelStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 WhenTint = new(0.95f, 0.58f, 0.20f, 1f);
    private static readonly Vector4 WorldTint = new(0.35f, 0.55f, 0.95f, 1f);
    private static readonly Vector4 LocationTint = new(0.30f, 0.75f, 0.45f, 1f);
    private static readonly Vector4 HostTint = new(0.62f, 0.45f, 0.92f, 1f);
    private static readonly Vector4 FavoriteInk = new(1f, 0.80f, 0.26f, 1f);

    private readonly LinkRow[] linkRows = new LinkRow[MaxLinkRows];
    private readonly ActionTile[] actionTiles = new ActionTile[MaxActionTiles];
    private readonly NavBarButton[] detailButtons = new NavBarButton[1];
    private VenueEvent? detailVenue;
    private int detailVersion = -1;
    private long detailMinute = -1;
    private DetailText detailText;

    private enum TileKind : byte
    {
        Go,
        Website,
        Discord,
        Copy,
    }

    private readonly record struct LinkRow(string Label, string Url);

    private readonly record struct ActionTile(TileKind Kind, string Glyph, string Label);

    private readonly record struct DetailText(VenueStatus Status, string Title, string Meta, string Window,
        string Viewers, string Initial, string HostLine, string Performer, string Playing, string StatusHint,
        string Attendees);

    private VenueEvent ResolveDetail(VenueEvent routed)
    {
        var nowUtc = DateTime.UtcNow;
        var minute = CurrentMinute(nowUtc);
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
        RebuildHours(resolved, nowUtc);
        detailVersion = venues.Version;
        detailMinute = minute;
        var title = VenueDisplayText.Clean(resolved.Title);
        var confirmed = resolved.IsConfirmedLive(nowUtc);
        var status = VenueFormat.Status(resolved, nowUtc);
        var viewers = resolved.LiveViewers > 0 ? VenueFormat.Viewers(resolved.LiveViewers) : string.Empty;
        var host = resolved.Host.Length > 0 ? Loc.T(L.Venues.HostedBy, resolved.Host) : string.Empty;
        var playing = confirmed ? VenueDisplayText.Clean(resolved.LiveTitle) : string.Empty;
        var hint = status.Kind == VenueStatusKind.Open ? Loc.T(L.Venues.ScheduledOpen) : string.Empty;
        detailText = new DetailText(status, title.Length > 0 ? title : resolved.Title, DetailMeta(resolved),
            NextWindow(resolved), viewers, VenueLabelCache.InitialOf(title), host,
            VenueFormat.Performer(resolved, nowUtc), playing, hint,
            resolved.AttendeeCount > 0 ? resolved.AttendeeCount.ToString(Loc.Culture) : string.Empty);
        return resolved;
    }

    private static string DetailMeta(VenueEvent venue)
    {
        var meta = VenueFormat.Meta(venue);
        for (var category = 0; category < VenueCategories.Count; category++)
        {
            if (VenueCategories.Matches(venue, category))
            {
                var kind = Loc.T(CategoryLabels[category]);
                return meta.Length > 0 ? string.Concat(kind, VenueDisplayText.Separator, meta) : kind;
            }
        }

        return meta;
    }

    private static string NextWindow(VenueEvent venue)
    {
        if (venue.StartUtc is not { } start)
        {
            return string.Empty;
        }

        var startText = TimeText.FutureMoment(new DateTimeOffset(start).ToUnixTimeSeconds());
        return venue.EndUtc is { } end ? $"{startText} – {TimeText.Clock(end.ToLocalTime())}" : startText;
    }

    private void DrawDetail(in PhoneContext context, VenueRoute route)
    {
        var venue = ResolveDetail(route.Venue!);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("venues.detail"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawDetailHero(drawList, venue, origin, width, scale);
            cursorY = DrawDetailHeader(drawList, origin.X, cursorY + BlockGap * scale, width, scale);
            cursorY = DrawActionTiles(drawList, venue, origin.X, cursorY + BlockGap * scale, width, scale);
            cursorY = DrawNowPlaying(drawList, venue, origin.X, cursorY, width, scale);
            cursorY = DrawHours(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawAbout(venue, origin.X, cursorY, width, scale);
            cursorY = DrawInfoCard(drawList, venue, origin.X, cursorY, width, scale);
            cursorY = DrawLinks(drawList, venue, origin.X, cursorY, width, scale);
            cursorY = DrawDetailTags(drawList, venue, origin.X, cursorY, width, scale);
            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
        }

        var favorite = IsFavorite(venue.Id);
        detailButtons[0] = new NavBarButton(favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
            Loc.T(L.Venues.Favorites));
        UiAnchors.Report("venues.detail.favorite", AppHeader.LargeTitleButtonRect(in navBar, 0, detailButtons.Length));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "venues.nav.detail", detailText.Title,
            NavBarStyle.From(ui), detailButtons, route.BackTitle, back);
        if (pressed == 0)
        {
            ToggleFavorite(venue.Id);
        }
    }

    private static float DetailHeroHeight(float width, float scale) =>
        MathF.Min(width * HeroAspect, HeroMaxHeight * scale);

    private float DrawDetailHero(ImDrawListPtr drawList, VenueEvent venue, Vector2 origin, float width, float scale)
    {
        var height = DetailHeroHeight(width, scale);
        var hero = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Metrics.Radius.Grouped * scale;
        VenueImage.Cover(drawList, hero, radius, venue, detailText.Initial, Art);
        var inset = HeroInset * scale;
        VenueCard.DrawStatusPill(drawList, new Vector2(hero.Min.X + inset, hero.Min.Y + inset),
            detailText.Status.Kind, venue, hero.Max.X - inset, scale);
        if (venue.LogoUrl is null || venue.BannerUrl is null)
        {
            return hero.Max.Y;
        }

        var side = HeroLogoSide * scale;
        var rim = HeroLogoRim * scale;
        var logo = new Rect(new Vector2(hero.Min.X + inset, hero.Max.Y - inset - side),
            new Vector2(hero.Min.X + inset + side, hero.Max.Y - inset));
        var logoRadius = side * Metrics.Radius.TileFactor;
        Squircle.Fill(drawList, logo.Min - new Vector2(rim, rim), logo.Max + new Vector2(rim, rim), logoRadius + rim,
            ImGui.GetColorU32(MediaOverlay.Fill));
        VenueImage.Logo(drawList, logo, logoRadius, venue, detailText.Initial, Art);
        return hero.Max.Y;
    }

    private float DrawDetailHeader(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = top;
        if (detailText.Meta.Length > 0)
        {
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), detailText.Meta, ui.MutedInk, MetaStyle,
                width);
        }

        if (detailText.HostLine.Length > 0)
        {
            cursorY += VenuesArt.LineGap * scale;
            Typography.Draw(drawList, new Vector2(left, cursorY),
                Typography.FitText(detailText.HostLine, width, HintStyle), ui.MutedInk, HintStyle);
            cursorY += Typography.LineHeight(HintStyle);
        }

        if (detailText.Status.Label.Length == 0)
        {
            return cursorY;
        }

        cursorY += Metrics.Space.Sm * scale;
        var statusLeft = left;
        var tint = VenueCard.StatusTint(detailText.Status.Kind, ui);
        var statusHeight = Typography.LineHeight(StatusStyle);
        if (detailText.Status.Kind is VenueStatusKind.Live or VenueStatusKind.Open)
        {
            MediaOverlay.LiveDot(drawList, new Vector2(statusLeft + 5f * scale, cursorY + statusHeight * 0.5f), tint,
                scale);
            statusLeft += StatusDotSpace * scale;
        }

        Typography.Draw(drawList, new Vector2(statusLeft, cursorY),
            Typography.FitText(detailText.Status.Label, MathF.Max(1f, left + width - statusLeft), StatusStyle), tint,
            StatusStyle);
        cursorY += statusHeight;
        if (detailText.StatusHint.Length > 0)
        {
            cursorY += VenuesArt.LineGap * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), detailText.StatusHint, ui.MutedInk,
                HintStyle, width);
        }

        return cursorY;
    }

    private int CollectActionTiles(VenueEvent venue)
    {
        var count = 0;
        if (venue.CanTeleport)
        {
            actionTiles[count++] = new ActionTile(TileKind.Go, PhoneIcons.NavigationFilled, Loc.T(L.Travel.GoThere));
        }

        if (!string.IsNullOrEmpty(venue.WebsiteUrl))
        {
            actionTiles[count++] = new ActionTile(TileKind.Website, PhoneIcons.Link, Loc.T(L.Venues.Website));
        }

        if (!string.IsNullOrEmpty(venue.DiscordUrl))
        {
            actionTiles[count++] = new ActionTile(TileKind.Discord, PhoneIcons.MessageCircle, Loc.T(L.Venues.Discord));
        }

        if (venue.PlaceLine.Length > 0)
        {
            actionTiles[count++] = new ActionTile(TileKind.Copy, PhoneIcons.Copy, Loc.T(L.Venues.Copy));
        }

        return count;
    }

    private float DrawActionTiles(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float width,
        float scale)
    {
        var count = CollectActionTiles(venue);
        if (count == 0)
        {
            return top - BlockGap * scale;
        }

        var gap = ActionTileGap * scale;
        var tileWidth = (width - gap * (count - 1)) / count;
        var height = ActionTileHeight * scale;
        for (var index = 0; index < count; index++)
        {
            var tile = actionTiles[index];
            var min = new Vector2(left + index * (tileWidth + gap), top);
            var rect = new Rect(min, min + new Vector2(tileWidth, height));
            if (tile.Kind == TileKind.Go)
            {
                UiAnchors.Report("venues.detail.go", rect);
            }

            if (DrawActionTile(drawList, rect, tile, scale))
            {
                RunTile(tile.Kind, venue);
            }
        }

        return top + height;
    }

    private bool DrawActionTile(ImDrawListPtr drawList, Rect rect, in ActionTile tile, float scale)
    {
        var primary = tile.Kind == TileKind.Go;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID(tile.Glyph), down, PressFx.ControlPressedScale);
        var half = new Vector2(rect.Width, rect.Height) * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = ActionTileRadius * scale;
        if (primary)
        {
            Squircle.Fill(drawList, min, max, radius,
                ImGui.GetColorU32(hovered ? Palette.Lighten(ui.Accent, 0.12f) : ui.Accent));
        }
        else
        {
            ui.Card(drawList, min, max, radius);
            if (hovered)
            {
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
            }
        }

        var glyphInk = primary ? AccentRing.Ink : Palette.Lighten(ui.Accent, 0.18f);
        var labelInk = primary ? AccentRing.Ink : ui.TitleInk;
        var labelHeight = Typography.LineHeight(ActionLabelStyle);
        var glyphSize = ActionGlyph * scale;
        var blockTop = (min.Y + max.Y - glyphSize - ActionLabelGap * scale - labelHeight) * 0.5f;
        var centerX = (min.X + max.X) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(centerX, blockTop + glyphSize * 0.5f), tile.Glyph, glyphInk, glyphSize);
        var label = Typography.FitText(tile.Label, MathF.Max(1f, max.X - min.X - 8f * scale), ActionLabelStyle);
        Typography.DrawCentered(drawList,
            new Vector2(centerX, blockTop + glyphSize + ActionLabelGap * scale + labelHeight * 0.5f), label, labelInk,
            ActionLabelStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void RunTile(TileKind kind, VenueEvent venue)
    {
        switch (kind)
        {
            case TileKind.Go:
                Teleport(venue);
                break;
            case TileKind.Website:
                UrlActions.AskThenOpen(venue.WebsiteUrl!);
                break;
            case TileKind.Discord:
                UrlActions.AskThenOpen(venue.DiscordUrl!);
                break;
            case TileKind.Copy:
                ImGui.SetClipboardText(venue.PlaceLine);
                UiFeedback.Play(UiSound.Success);
                ShellToast.Show();
                break;
        }
    }

    private float DrawNowPlaying(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float width,
        float scale)
    {
        if (!venue.IsConfirmedLive(DateTime.UtcNow))
        {
            return top;
        }

        var cardTop = Section(drawList, left, top, width, Loc.T(L.Venues.NowPlaying), string.Empty, out _, scale);
        var inner = CardInset * scale;
        var cardRight = left + width;
        var radius = NowPlayingIconRadius * scale;
        var headlineHeight = Typography.LineHeight(InfoValueStyle);
        var captionHeight = Typography.LineHeight(CaptionStyle);
        var rowHeight = MathF.Max(radius * 2f, headlineHeight + 2f * scale + captionHeight);
        var djCount = detailDjs.Count;
        var djRowHeight = DjRowHeight * scale;
        var topBlock = djCount > 0 ? inner * 0.5f + djCount * djRowHeight : inner + rowHeight;
        var hasTwitch = !string.IsNullOrEmpty(venue.TwitchUrl);
        var fullWidth = MathF.Max(1f, width - inner * 2f);
        var titleHeight = detailText.Playing.Length > 0
            ? Typography.MeasureWrappedBlock(detailText.Playing, StreamTitleStyle, fullWidth).Y
            : 0f;
        var genresHeight = venue.LiveGenres.Count > 0 ? VenueChips.Height(scale) : 0f;
        var cardHeight = topBlock + inner +
                         (titleHeight > 0f ? Metrics.Space.Sm * scale + titleHeight : 0f) +
                         (genresHeight > 0f ? Metrics.Space.Sm * scale + genresHeight : 0f) +
                         (hasTwitch ? Metrics.Space.Md * scale + TwitchButtonHeight * scale : 0f);
        ui.Card(drawList, new Vector2(left, cardTop), new Vector2(cardRight, cardTop + cardHeight), Metrics.Radius.Grouped * scale);
        if (djCount > 0)
        {
            var linkable = djCount > 1;
            for (var index = 0; index < djCount; index++)
            {
                var rowTop = cardTop + inner * 0.5f + index * djRowHeight;
                DrawDetailDjRow(drawList, new Vector2(left, rowTop), new Vector2(cardRight, rowTop + djRowHeight),
                    index, linkable, inner, scale);
            }
        }
        else
        {
            DrawNowPlayingHeadline(drawList, venue, left, cardRight, cardTop, rowHeight, scale);
        }

        var cursorY = cardTop + topBlock;
        if (titleHeight > 0f)
        {
            cursorY += Metrics.Space.Sm * scale;
            Typography.DrawWrappedLeft(new Vector2(left + inner, cursorY), detailText.Playing, ui.BodyInk,
                StreamTitleStyle, fullWidth);
            cursorY += titleHeight;
        }

        if (genresHeight > 0f)
        {
            cursorY += Metrics.Space.Sm * scale;
            VenueCard.DrawChipRow(drawList, venue.LiveGenres, left + inner, cardRight - inner, cursorY, scale);
            cursorY += genresHeight;
        }

        if (hasTwitch)
        {
            var buttonTop = cursorY + Metrics.Space.Md * scale;
            var button = new Rect(new Vector2(left + inner, buttonTop),
                new Vector2(cardRight - inner, buttonTop + TwitchButtonHeight * scale));
            if (VenuesArt.Action(drawList, ui, button, ImGui.GetID("venues.detail.twitch"),
                    Loc.T(L.Venues.WatchOnTwitch), PhoneIcons.ExternalLink, false, ButtonStyle.Gray))
            {
                UrlActions.AskThenOpen(venue.TwitchUrl!);
            }
        }

        var hintTop = cardTop + cardHeight + Metrics.Space.Xs * scale;
        return hintTop + Typography.DrawWrappedLeft(new Vector2(left, hintTop), Loc.T(L.Venues.ConfirmedLive),
            ui.MutedInk, HintStyle, width);
    }

    private void DrawNowPlayingHeadline(ImDrawListPtr drawList, VenueEvent venue, float cardLeft, float cardRight,
        float top, float rowHeight, float scale)
    {
        var inner = CardInset * scale;
        var radius = NowPlayingIconRadius * scale;
        var headlineHeight = Typography.LineHeight(InfoValueStyle);
        var captionHeight = Typography.LineHeight(CaptionStyle);
        var iconCenter = new Vector2(cardLeft + inner + radius, top + inner + rowHeight * 0.5f);
        drawList.AddCircleFilled(iconCenter, radius,
            ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.LiveGreen, 0.18f)), 32);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Microphone, MediaOverlay.LiveGreen, 20f * scale);
        var textLeft = iconCenter.X + radius + 12f * scale;
        var textWidth = MathF.Max(1f, cardRight - inner - textLeft);
        var textTop = top + inner + (rowHeight - headlineHeight - 2f * scale - captionHeight) * 0.5f;
        var headline = detailText.Performer.Length > 0 ? detailText.Performer : Loc.T(L.Venues.LiveNowLabel);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.detail.playing.", venue.Id), headline, textLeft, textTop,
            textWidth, InfoValueStyle, ui.TitleInk);
        var caption = detailText.Viewers.Length > 0 ? detailText.Viewers : Loc.T(L.Venues.SourceRolladeck);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + headlineHeight + 2f * scale),
            Typography.FitText(caption, textWidth, CaptionStyle), ui.MutedInk, CaptionStyle);
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

    private float DrawAbout(VenueEvent venue, float left, float top, float width, float scale)
    {
        if (venue.Description.Length == 0)
        {
            return top;
        }

        var drawList = ImGui.GetWindowDrawList();
        var textTop = Section(drawList, left, top, width, Loc.T(L.Venues.About), string.Empty, out _, scale);
        var venueKey = new TranslationKey(TranslationSurface.Venue, venue.Id);
        var aboutText = translation.View(venueKey, venue.Description).Text;
        ImGui.SetCursorScreenPos(new Vector2(left, textTop));
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.BodyInk))
        {
            ImGui.PushTextWrapPos(left + width - ImGui.GetWindowPos().X);
            Typography.Plain(aboutText);
            ImGui.PopTextWrapPos();
        }

        var cursorY = ImGui.GetCursorScreenPos().Y;
        var language = VenueLanguage(venue);
        var linkHeight = TranslateLink.Height(translation, venueKey, language, scale);
        if (linkHeight <= 0f)
        {
            return cursorY;
        }

        TranslateLink.Draw(translation, confirm, venueKey, language, venue.Description, new Vector2(left, cursorY),
            width, ui.MutedInk, ui.Accent, scale);
        return cursorY + linkHeight;
    }

    private float DrawInfoCard(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float width,
        float scale)
    {
        var showWindow = venue.Openings.Count == 0 && detailText.Window.Length > 0;
        var showWorld = venue.World.Length > 0 || venue.DataCenter.Length > 0;
        var showLocation = venue.LocationLine.Length > 0 &&
                           !string.Equals(venue.LocationLine, venue.World, StringComparison.Ordinal);
        var showAttendees = venue.AttendeeCount > 0;
        var rows = (showWindow ? 1 : 0) + (showWorld ? 1 : 0) + (showLocation ? 1 : 0) + (showAttendees ? 1 : 0);
        if (rows == 0)
        {
            return top;
        }

        var cardTop = Section(drawList, left, top, width, Loc.T(L.Venues.Details), string.Empty, out _, scale);
        var card = new Rect(new Vector2(left, cardTop),
            new Vector2(left + width, cardTop + rows * InfoRowHeight * scale));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var rowIndex = 0;
        if (showWindow)
        {
            InfoRow(drawList, card, rowIndex++, rows, PhoneIcons.Clock, WhenTint, Loc.T(L.Venues.NextOpening),
                detailText.Window, scale);
        }

        if (showWorld)
        {
            var label = venue.World.Length > 0 ? Loc.T(L.Venues.World) : Loc.T(L.Venues.DataCenter);
            InfoRow(drawList, card, rowIndex++, rows, PhoneIcons.World, WorldTint, label,
                venue.World.Length > 0 ? venue.World : venue.DataCenter, scale);
        }

        if (showLocation)
        {
            InfoRow(drawList, card, rowIndex++, rows, PhoneIcons.MapPin, LocationTint, Loc.T(L.Venues.Location),
                venue.LocationLine, scale);
        }

        if (showAttendees)
        {
            InfoRow(drawList, card, rowIndex, rows, PhoneIcons.Users, HostTint, Loc.T(L.Venues.Attendees),
                detailText.Attendees, scale);
        }

        return card.Max.Y;
    }

    private void InfoRow(ImDrawListPtr drawList, Rect card, int rowIndex, int rowCount, string glyph, Vector4 tint,
        string label, string value, float scale)
    {
        var rowHeight = InfoRowHeight * scale;
        var rowTop = card.Min.Y + rowIndex * rowHeight;
        var centerY = rowTop + rowHeight * 0.5f;
        var inset = CardInset * scale;
        var tileSide = InfoTileSide * scale;
        var tileMin = new Vector2(card.Min.X + inset, centerY - tileSide * 0.5f);
        var tileMax = tileMin + new Vector2(tileSide, tileSide);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSide * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, glyph, AccentRing.Ink, 16f * scale);
        var labelLeft = tileMax.X + VenuesArt.TextGap * scale;
        var labelSize = Typography.Measure(label, InfoLabelStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), label, ui.MutedInk,
            InfoLabelStyle);
        var valueRight = card.Max.X - inset;
        var valueWidth = MathF.Max(1f, valueRight - labelLeft - labelSize.X - VenuesArt.TextGap * scale);
        var valueHeight = Typography.LineHeight(InfoValueStyle);
        Marquee.DrawRightAuto(drawList, new MarqueeId("venues.detail.row.", label), value, valueRight,
            centerY - valueHeight * 0.5f, valueWidth, InfoValueStyle, ui.TitleInk);
        if (rowIndex < rowCount - 1)
        {
            FeedCell.Hairline(drawList, labelLeft, valueRight, rowTop + rowHeight, ui.Hairline);
        }
    }

    private int CollectLinks(VenueEvent venue)
    {
        var count = 0;
        var listing = (venue.Sources & VenueSources.Partake) != 0
            ? Loc.T(L.Venues.SourcePartake)
            : Loc.T(L.Venues.SourceFfxiv);
        AddLink(ref count, listing, venue.ListingUrl);
        AddLink(ref count, Loc.T(L.Venues.SourceRolladeck), venue.RolladeckUrl);
        return count;
    }

    private void AddLink(ref int count, string label, string? url)
    {
        if (string.IsNullOrEmpty(url) || count >= linkRows.Length)
        {
            return;
        }

        linkRows[count++] = new LinkRow(label, url);
    }

    private float DrawLinks(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float width, float scale)
    {
        var count = CollectLinks(venue);
        if (count == 0)
        {
            return top;
        }

        var cardTop = Section(drawList, left, top, width, Loc.T(L.Venues.ListedOn), string.Empty, out _, scale);
        var rowHeight = InfoRowHeight * scale;
        var card = new Rect(new Vector2(left, cardTop), new Vector2(left + width, cardTop + count * rowHeight));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var inset = CardInset * scale;
        for (var index = 0; index < count; index++)
        {
            var link = linkRows[index];
            var row = new Rect(new Vector2(card.Min.X, card.Min.Y + index * rowHeight),
                new Vector2(card.Max.X, card.Min.Y + (index + 1) * rowHeight));
            var hovered = VenuesArt.RowWash(drawList, ui, row, scale);
            var centerY = row.Center.Y;
            PhoneIcon.Draw(drawList, new Vector2(row.Min.X + inset + 9f * scale, centerY), PhoneIcons.ExternalLink,
                Palette.Lighten(ui.Accent, 0.18f), 18f * scale);
            var labelLeft = row.Min.X + inset + 30f * scale;
            var labelHeight = Typography.LineHeight(InfoValueStyle);
            Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelHeight * 0.5f),
                Typography.FitText(link.Label, MathF.Max(1f, row.Max.X - inset - 20f * scale - labelLeft),
                    InfoValueStyle), ui.TitleInk, InfoValueStyle);
            PhoneIcon.Draw(drawList, new Vector2(row.Max.X - inset - 6f * scale, centerY), PhoneIcons.ChevronRight,
                ui.MutedInk, 14f * scale);
            if (index < count - 1)
            {
                FeedCell.Hairline(drawList, labelLeft, row.Max.X - inset, row.Max.Y, ui.Hairline);
            }

            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                UrlActions.AskThenOpen(link.Url);
            }
        }

        return card.Max.Y;
    }

    private float DrawDetailTags(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float width,
        float scale)
    {
        if (venue.Tags.Count == 0)
        {
            return top;
        }

        var cursorY = Section(drawList, left, top, width, Loc.T(L.Venues.Tags), string.Empty, out _, scale);
        var right = left + width;
        var gap = Metrics.Space.Xs * scale;
        var lineHeight = VenueChips.Height(scale) + ChipLineGap * scale;
        var cursorX = left;
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

        return cursorY + VenueChips.Height(scale);
    }
}

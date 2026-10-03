using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal enum VenueCardAction : byte
{
    None,
    Open,
    ToggleFavorite,
    Teleport,
    Twitch,
}

internal static class VenueCard
{
    public const float RailWidth = 148f;
    public const float RailHeight = 192f;

    private const float HeroAspect = 0.48f;
    private const float HeroMaxHeight = 210f;
    private const float PadX = SocialChrome.CellPadX;
    private const float PadTop = 12f;
    private const float PillGap = 6f;
    private const float PadBottom = 14f;
    private const float LogoSide = 42f;
    private const float LogoGap = 11f;
    private const float RowGap = 8f;
    private const float OverlayInset = 12f;
    private const float HeroScrimShare = 0.42f;
    private const float StatGlyph = 13f;
    private const float RailRounding = 18f;
    private const float RailPad = 11f;
    private const float RailScrimShare = 0.66f;
    public const float HoverLift = 4f;
    private const float ShadowOpacity = 0.35f;
    private const float RimAlpha = 0.30f;
    private const float RimWeight = 1.5f;
    private const float ActionButtonHeight = 36f;
    private const float ActionRowHeight = ActionButtonHeight + RowGap;
    private const float FeaturedRounding = 22f;
    private const float FeaturedPad = 14f;
    private const float FeaturedScrimShare = 0.7f;
    private const float GoHeight = 36f;
    private const float GoGlyph = 15f;
    private const float RowCellHeight = 76f;
    private const float RowThumb = 52f;
    private const float LeadColumn = 50f;

    private static readonly TextStyle FeaturedTitleStyle = TextStyles.Title2;
    private static readonly TextStyle RowTitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle LeadTopStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle ActionStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle GoStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle NowPlayingStyle = TextStyles.Footnote;
    private const float NowPlayingGap = 4f;

    private static readonly TextStyle TitleStyle = TextStyles.Headline;
    private static readonly TextStyle MetaStyle = TextStyles.Footnote;
    private static readonly TextStyle StatusStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle StatStyle = TextStyles.Caption1;
    private static readonly TextStyle RailTitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle RailStatusStyle = TextStyles.Caption1;
    private static readonly Vector4 RailMutedInk = new(1f, 1f, 1f, 0.80f);
    private static readonly Vector4 FavoriteInk = new(1f, 0.80f, 0.26f, 1f);

    public static float FeedHeight(VenueEvent venue, in VenueCardText text, float width, float scale, bool actions)
    {
        var height = FeedHeight(venue, text, width, scale);
        return HasActions(venue, actions) ? height + ActionRowHeight * scale : height;
    }

    private static bool HasActions(VenueEvent venue, bool actions) =>
        actions && (venue.CanTeleport || !string.IsNullOrEmpty(venue.TwitchUrl));

    public static float FeedHeight(VenueEvent venue, in VenueCardText text, float width, float scale)
    {
        var header = MathF.Max(Typography.LineHeight(TitleStyle) + 2f * scale + Typography.LineHeight(MetaStyle),
            venue.LogoUrl is null ? 0f : LogoSide * scale);
        var height = HeroHeight(width, scale) + PadTop * scale + header + PadBottom * scale;
        if (HasStatusRow(text))
        {
            height += RowGap * scale + Typography.LineHeight(StatusStyle);
        }

        if (HasNowPlaying(venue, text))
        {
            height += NowPlayingGap * scale + Typography.LineHeight(NowPlayingStyle);
        }

        if (ChipsOf(venue, text).Count > 0)
        {
            height += RowGap * scale + VenueChips.Height(scale);
        }

        return height;
    }

    private static bool HasNowPlaying(VenueEvent venue, in VenueCardText text) =>
        text.Status.Kind == VenueStatusKind.Live && venue.LiveTitle.Length > 0;

    private static IReadOnlyList<string> ChipsOf(VenueEvent venue, in VenueCardText text) =>
        text.Status.Kind == VenueStatusKind.Live && venue.LiveGenres.Count > 0 ? venue.LiveGenres : venue.Tags;

    public static VenueCardAction DrawFeed(VenueEvent venue, in VenueCardText text, bool favorite, in VenueArt art,
        SocialInk ink, bool actions = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var height = FeedHeight(venue, text, width, scale, actions);
        var origin = ImGui.GetCursorScreenPos();
        if (!ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return VenueCardAction.None;
        }

        var cell = FeedCell.Begin(drawList, height, ink.HoverTint);
        var bounds = cell.Bounds;
        var hero = new Rect(bounds.Min, new Vector2(bounds.Max.X, bounds.Min.Y + HeroHeight(bounds.Width, scale)));
        VenueImage.Cover(drawList, hero, 0f, venue, text.Initial, art);
        MediaOverlay.BottomScrim(drawList, hero.Min, hero.Max, HeroScrimShare);
        var inset = OverlayInset * scale;
        var starCenter = new Vector2(hero.Max.X - inset - MediaOverlay.GlassButtonRadius * scale,
            hero.Min.Y + inset + MediaOverlay.GlassButtonRadius * scale);
        DrawStatusPill(drawList, new Vector2(hero.Min.X + inset, hero.Min.Y + inset), text.Status.Kind, venue,
            starCenter.X - MediaOverlay.GlassButtonRadius * scale - PillGap * scale, scale);
        var starTapped = MediaOverlay.GlassButton(drawList, starCenter,
            favorite ? PhoneIcons.StarFilled : PhoneIcons.Star, string.Empty, scale, null,
            favorite ? FavoriteInk : MediaOverlay.White);
        PaintBody(drawList, venue, text, bounds, hero.Max.Y, art, ink, scale);
        var action = HasActions(venue, actions)
            ? DrawActionRow(drawList, venue, bounds, ink, scale)
            : VenueCardAction.None;
        FeedCell.End(drawList, cell, ink.Hairline);
        if (starTapped)
        {
            return VenueCardAction.ToggleFavorite;
        }

        if (action != VenueCardAction.None)
        {
            return action;
        }

        return cell.Tapped ? VenueCardAction.Open : VenueCardAction.None;
    }

    private static VenueCardAction DrawActionRow(ImDrawListPtr drawList, VenueEvent venue, Rect bounds,
        SocialInk ink, float scale)
    {
        var pad = PadX * scale;
        var height = ActionButtonHeight * scale;
        var top = bounds.Max.Y - PadBottom * scale - height;
        var left = bounds.Min.X + pad;
        var right = bounds.Max.X - pad;
        var hasTwitch = !string.IsNullOrEmpty(venue.TwitchUrl);
        var gap = 8f * scale;
        var split = venue.CanTeleport && hasTwitch ? (right - left - gap) * 0.5f : right - left;
        var action = VenueCardAction.None;
        if (venue.CanTeleport)
        {
            var rect = new Rect(new Vector2(left, top), new Vector2(left + split, top + height));
            if (SocialPill.Accent(drawList, rect, Loc.T(L.Travel.GoThere), ink, ActionStyle, height * 0.5f))
            {
                action = VenueCardAction.Teleport;
            }

            left += split + gap;
        }

        if (hasTwitch)
        {
            var rect = new Rect(new Vector2(left, top), new Vector2(left + split, top + height));
            if (SocialPill.Outline(drawList, rect, Loc.T(L.Venues.WatchOnTwitch), ink, ActionStyle, height * 0.5f,
                    ink.ChipFill))
            {
                action = VenueCardAction.Twitch;
            }
        }

        return action;
    }

    public static VenueCardAction DrawFeatured(ImDrawListPtr drawList, Rect rest, VenueEvent venue,
        in VenueCardText text, bool favorite, in VenueArt art, SocialInk ink, Rect clip, bool interactive)
    {
        var scale = UiScale.Current;
        var rounding = FeaturedRounding * scale;
        var pad = FeaturedPad * scale;
        var live = interactive && clip.Contains(ImGui.GetMousePos());
        var hovered = live && UiInteract.Hover(rest.Min, rest.Max);
        var tapped = live && UiInteract.Click(rest.Min, rest.Max, hovered);
        var buttonReach = (MediaOverlay.GlassButtonRadius * 2f + FeaturedPad) * scale;
        var overButtons = hovered && (UiInteract.Hover(new Vector2(rest.Max.X - buttonReach, rest.Min.Y),
            new Vector2(rest.Max.X, rest.Min.Y + buttonReach)) || (venue.CanTeleport &&
            UiInteract.Hover(new Vector2(rest.Max.X - buttonReach * 3f, rest.Max.Y - (GoHeight + FeaturedPad) * scale),
                rest.Max)));
        var card = Lift(drawList, rest, text.PressId, hovered, hovered && !overButtons, rounding, scale, out var eased);
        VenueImage.Cover(drawList, card, rounding, venue, text.Initial, art);
        Squircle.FillVerticalGradient(drawList, new Vector2(card.Min.X, card.Max.Y - card.Height * FeaturedScrimShare),
            card.Max, rounding, ImGui.GetColorU32(MediaOverlay.ScrimClear), ImGui.GetColorU32(MediaOverlay.ScrimDeep));
        DrawRim(drawList, card, rounding, eased, scale);
        var action = VenueCardAction.None;
        var radius = MediaOverlay.GlassButtonRadius * scale;
        DrawStatusPill(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), text.Status.Kind, venue,
            card.Max.X - pad - radius * 2f - PillGap * scale, scale);
        var starCenter = new Vector2(card.Max.X - pad - radius, card.Min.Y + pad + radius);
        var starLive = live && InsideClip(clip, starCenter.X - radius, starCenter.X + radius);
        if (MediaOverlay.GlassButton(drawList, starCenter, favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
                string.Empty, scale, null, favorite ? FavoriteInk : MediaOverlay.White, starLive))
        {
            action = VenueCardAction.ToggleFavorite;
        }

        var textLeft = card.Min.X + pad;
        var textWidth = MathF.Max(1f, card.Max.X - pad - textLeft);
        var metaHeight = Typography.LineHeight(MetaStyle);
        var titleHeight = Typography.LineHeight(FeaturedTitleStyle);
        var statusHeight = Typography.LineHeight(StatusStyle);
        var bottomRowHeight = venue.CanTeleport ? GoHeight * scale : metaHeight;
        var bottomRowTop = card.Max.Y - pad - bottomRowHeight;
        var metaRight = card.Max.X - pad;
        if (venue.CanTeleport)
        {
            var label = Loc.T(L.Travel.GoThere);
            var goWidth = Typography.Measure(label, GoStyle).X + (GoGlyph + 6f + 28f) * scale;
            var go = new Rect(new Vector2(card.Max.X - pad - goWidth, bottomRowTop),
                new Vector2(card.Max.X - pad, bottomRowTop + bottomRowHeight));
            if (DrawGoButton(drawList, go, label, ink, live && InsideClip(clip, go.Min.X, go.Max.X)))
            {
                action = VenueCardAction.Teleport;
            }

            metaRight = go.Min.X - 10f * scale;
        }

        var metaTop = bottomRowTop + (bottomRowHeight - metaHeight) * 0.5f;
        var titleTop = bottomRowTop - titleHeight - 2f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, metaTop),
            Typography.FitText(text.Meta, MathF.Max(1f, metaRight - textLeft), MetaStyle), RailMutedInk, MetaStyle);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.featured.title.", venue.Id), venue.Title, textLeft,
            titleTop, textWidth, FeaturedTitleStyle, MediaOverlay.White);
        if (text.Status.Label.Length > 0)
        {
            var statusTop = titleTop - statusHeight;
            var tint = text.Status.Kind == VenueStatusKind.Upcoming ? MediaOverlay.White : MediaOverlay.LiveGreen;
            Typography.Draw(drawList, new Vector2(textLeft, statusTop),
                Typography.FitText(text.Status.Label, textWidth, StatusStyle), tint, StatusStyle);
        }

        if (hovered && action == VenueCardAction.None)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (action != VenueCardAction.None)
        {
            return action;
        }

        return tapped ? VenueCardAction.Open : VenueCardAction.None;
    }

    public static Rect Lift(ImDrawListPtr drawList, Rect rest, string id, bool hovered, bool pressable, float rounding,
        float scale, out float eased)
    {
        eased = HoverFx.Amount(id, hovered);
        var pressed = pressable && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = HoverLift * scale * eased - rest.Width * 0.5f * (1f - PressFx.Scale(id, pressed, PressFx.CardPressedScale));
        var card = new Rect(rest.Min - new Vector2(grow, grow), rest.Max + new Vector2(grow, grow));
        Elevation.Card(drawList, card.Min, card.Max, rounding, scale, ShadowOpacity * (1f + eased));
        return card;
    }

    public static void DrawRim(ImDrawListPtr drawList, Rect card, float rounding, float eased, float scale)
    {
        if (eased <= 0.001f)
        {
            return;
        }

        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.White, RimAlpha * eased)), RimWeight * scale);
    }

    private static bool InsideClip(Rect clip, float left, float right) => left >= clip.Min.X && right <= clip.Max.X;

    private static bool DrawGoButton(ImDrawListPtr drawList, Rect rect, string label, SocialInk ink, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        AccentPill.Paint(drawList, rect.Min, rect.Max, rect.Height * 0.5f, hovered, ink.Accent, ink.AccentDeep,
            ink.AccentShadow);
        var glyphSize = GoGlyph * scale;
        var labelSize = Typography.Measure(label, GoStyle);
        var contentLeft = rect.Center.X - (glyphSize + 6f * scale + labelSize.X) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(contentLeft + glyphSize * 0.5f, rect.Center.Y),
            PhoneIcons.NavigationFilled, MediaOverlay.White, glyphSize);
        Typography.Draw(drawList,
            new Vector2(contentLeft + glyphSize + 6f * scale, rect.Center.Y - labelSize.Y * 0.5f), label,
            MediaOverlay.White, GoStyle);
        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float RowHeight(float scale) => RowCellHeight * scale;

    public static VenueCardAction DrawRow(VenueEvent venue, in VenueCardText text, bool favorite, in VenueArt art,
        SocialInk ink, string leadTop = "", string leadBottom = "", bool leadLive = false, string title = "")
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var height = RowHeight(scale);
        var origin = ImGui.GetCursorScreenPos();
        if (!ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return VenueCardAction.None;
        }

        var cell = FeedCell.Begin(drawList, height, ink.HoverTint);
        var bounds = cell.Bounds;
        var pad = PadX * scale;
        var left = bounds.Min.X + pad;
        var centerY = bounds.Center.Y;
        if (leadTop.Length > 0 || leadLive)
        {
            var columnWidth = LeadColumn * scale;
            var topStyle = leadLive ? StatusStyle : LeadTopStyle;
            var topInk = leadLive ? MediaOverlay.LiveGreen : ink.TitleInk;
            var topLabel = leadLive ? Loc.T(L.Common.Live) : leadTop;
            var topHeight = Typography.LineHeight(topStyle);
            var bottomHeight = leadBottom.Length > 0 ? Typography.LineHeight(StatStyle) : 0f;
            var blockTop = centerY - (topHeight + bottomHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(left, blockTop), Typography.FitText(topLabel, columnWidth, topStyle),
                topInk, topStyle);
            if (leadBottom.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(left, blockTop + topHeight),
                    Typography.FitText(leadBottom, columnWidth, StatStyle), ink.MutedInk, StatStyle);
            }

            left += columnWidth + 8f * scale;
        }

        var thumbSide = RowThumb * scale;
        var thumb = new Rect(new Vector2(left, centerY - thumbSide * 0.5f),
            new Vector2(left + thumbSide, centerY + thumbSide * 0.5f));
        if (venue.LogoUrl is not null)
        {
            VenueImage.Logo(drawList, thumb, thumbSide * 0.26f, venue, text.Initial, art);
        }
        else
        {
            VenueImage.Cover(drawList, thumb, thumbSide * 0.26f, venue, text.Initial, art);
        }

        var starCenter = new Vector2(bounds.Max.X - pad - 10f * scale, centerY);
        var starExtent = new Vector2(16f * scale, 16f * scale);
        var starHovered = UiInteract.Hover(starCenter - starExtent, starCenter + starExtent);
        PhoneIcon.Draw(drawList, starCenter, favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
            favorite ? FavoriteInk : starHovered ? ink.TitleInk : ink.FaintInk, 18f * scale);
        var starTapped = UiInteract.Click(starCenter - starExtent, starCenter + starExtent, starHovered);
        var textLeft = thumb.Max.X + 12f * scale;
        var textWidth = MathF.Max(1f, starCenter.X - 20f * scale - textLeft);
        var heading = title.Length > 0 ? title : venue.Title;
        var subline = title.Length > 0 ? venue.Title : text.Meta;
        var headingHeight = Typography.LineHeight(RowTitleStyle);
        var sublineHeight = Typography.LineHeight(MetaStyle);
        var thirdLine = title.Length > 0 ? text.Meta : text.Status.Label;
        var thirdHeight = thirdLine.Length > 0 ? Typography.LineHeight(StatStyle) : 0f;
        var top = centerY - (headingHeight + sublineHeight + thirdHeight) * 0.5f;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.row.title.", venue.Id), heading, textLeft, top, textWidth,
            RowTitleStyle, ink.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, top + headingHeight),
            Typography.FitText(subline, textWidth, MetaStyle), ink.MutedInk, MetaStyle);
        if (thirdLine.Length > 0)
        {
            var thirdInk = title.Length > 0 ? ink.FaintInk : StatusTint(text.Status.Kind, ink);
            Typography.Draw(drawList, new Vector2(textLeft, top + headingHeight + sublineHeight),
                Typography.FitText(thirdLine, textWidth, StatStyle), thirdInk, StatStyle);
        }

        FeedCell.End(drawList, cell, ink.Hairline);
        if (starTapped)
        {
            return VenueCardAction.ToggleFavorite;
        }

        return cell.Tapped ? VenueCardAction.Open : VenueCardAction.None;
    }

    public static bool DrawRail(ImDrawListPtr drawList, Rect rest, VenueEvent venue, in VenueCardText text,
        in VenueArt art, SocialInk ink, Rect clip, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && clip.Contains(ImGui.GetMousePos()) && UiInteract.Hover(rest.Min, rest.Max);
        var rounding = RailRounding * scale;
        var card = Lift(drawList, rest, text.PressId, hovered, hovered, rounding, scale, out var eased);
        VenueImage.Cover(drawList, card, rounding, venue, text.Initial, art);
        Squircle.FillVerticalGradient(drawList, new Vector2(card.Min.X, card.Max.Y - card.Height * RailScrimShare),
            card.Max, rounding, ImGui.GetColorU32(MediaOverlay.ScrimClear),
            ImGui.GetColorU32(MediaOverlay.ScrimDeep));
        DrawRim(drawList, card, rounding, eased, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = RailPad * scale;
        DrawStatusPill(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), text.Status.Kind, venue,
            card.Max.X - pad, scale);
        var textWidth = card.Width - pad * 2f;
        var statusHeight = Typography.LineHeight(RailStatusStyle);
        var titleHeight = Typography.LineHeight(RailTitleStyle);
        var statusTop = card.Max.Y - pad - statusHeight;
        var titleTop = statusTop - titleHeight - 1f * scale;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.rail.title.", venue.Id), venue.Title, card.Min.X + pad,
            titleTop, textWidth, RailTitleStyle, MediaOverlay.White);
        var statusLeft = card.Min.X + pad;
        if (text.Stat.Length > 0)
        {
            statusLeft += DrawStat(drawList, statusLeft, statusTop + statusHeight * 0.5f, text.Stat,
                venue.LiveViewers > 0 ? PhoneIcons.Eye : PhoneIcons.Users, RailMutedInk, scale) + 8f * scale;
        }

        var railStatus = text.Status.Label.Length > 0 ? text.Status.Label : venue.World;
        Typography.Draw(drawList, new Vector2(statusLeft, statusTop),
            Typography.FitText(railStatus, MathF.Max(1f, card.Max.X - pad - statusLeft), RailStatusStyle),
            RailMutedInk, RailStatusStyle);
        return interactive && UiInteract.Click(rest.Min, rest.Max, hovered);
    }

    private static void PaintBody(ImDrawListPtr drawList, VenueEvent venue, in VenueCardText text, Rect bounds,
        float top, in VenueArt art, SocialInk ink, float scale)
    {
        var pad = PadX * scale;
        var left = bounds.Min.X + pad;
        var right = bounds.Max.X - pad;
        var cursorY = top + PadTop * scale;
        var textLeft = left;
        var titleHeight = Typography.LineHeight(TitleStyle);
        var metaHeight = Typography.LineHeight(MetaStyle);
        var headerHeight = titleHeight + 2f * scale + metaHeight;
        if (venue.LogoUrl is not null)
        {
            var side = LogoSide * scale;
            var logo = new Rect(new Vector2(left, cursorY), new Vector2(left + side, cursorY + side));
            VenueImage.Logo(drawList, logo, side * 0.28f, venue, text.Initial, art);
            textLeft = logo.Max.X + LogoGap * scale;
            cursorY += MathF.Max(0f, (side - headerHeight) * 0.5f);
            headerHeight = MathF.Max(headerHeight, side);
        }

        var textWidth = MathF.Max(1f, right - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.card.title.", venue.Id), venue.Title, textLeft, cursorY,
            textWidth, TitleStyle, ink.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY + titleHeight + 2f * scale),
            Typography.FitText(text.Meta, textWidth, MetaStyle), ink.MutedInk, MetaStyle);
        cursorY = top + PadTop * scale + headerHeight;
        if (HasStatusRow(text))
        {
            cursorY += RowGap * scale;
            DrawStatusRow(drawList, venue, text, left, right, cursorY, ink, scale);
            cursorY += Typography.LineHeight(StatusStyle);
        }

        if (HasNowPlaying(venue, text))
        {
            cursorY += NowPlayingGap * scale;
            var lineHeight = Typography.LineHeight(NowPlayingStyle);
            var glyphSize = 13f * scale;
            PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, cursorY + lineHeight * 0.5f), PhoneIcons.Music,
                ink.MutedInk, glyphSize);
            var titleLeft = left + glyphSize + 6f * scale;
            Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.card.playing.", venue.Id), venue.LiveTitle, titleLeft,
                cursorY, MathF.Max(1f, right - titleLeft), NowPlayingStyle, ink.BodyInk);
            cursorY += lineHeight;
        }

        var chips = ChipsOf(venue, text);
        if (chips.Count > 0)
        {
            cursorY += RowGap * scale;
            DrawChipRow(drawList, chips, left, right, cursorY, scale);
        }
    }

    private static void DrawStatusRow(ImDrawListPtr drawList, VenueEvent venue, in VenueCardText text, float left,
        float right, float top, SocialInk ink, float scale)
    {
        var lineHeight = Typography.LineHeight(StatusStyle);
        var centerY = top + lineHeight * 0.5f;
        var statRight = right;
        if (text.Stat.Length > 0)
        {
            var statWidth = StatWidth(text.Stat, scale);
            DrawStat(drawList, right - statWidth, centerY, text.Stat,
                venue.LiveViewers > 0 ? PhoneIcons.Eye : PhoneIcons.Users, ink.MutedInk, scale);
            statRight = right - statWidth - 10f * scale;
        }

        if (text.Status.Label.Length == 0)
        {
            return;
        }

        var cursorX = left;
        var tint = StatusTint(text.Status.Kind, ink);
        if (text.Status.Kind == VenueStatusKind.Live)
        {
            MediaOverlay.LiveDot(drawList, new Vector2(cursorX + 5f * scale, centerY), tint, scale);
            cursorX += 16f * scale;
        }

        Typography.Draw(drawList, new Vector2(cursorX, top),
            Typography.FitText(text.Status.Label, MathF.Max(1f, statRight - cursorX), StatusStyle), tint,
            StatusStyle);
    }

    public static Vector4 StatusTint(VenueStatusKind kind, SocialInk ink) =>
        kind switch
        {
            VenueStatusKind.Live => MediaOverlay.LiveGreen,
            VenueStatusKind.Open => Palette.WithAlpha(MediaOverlay.LiveGreen, 0.82f),
            _ => ink.AccentLink,
        };

    public static void DrawStatusPill(ImDrawListPtr drawList, Vector2 topLeft, VenueStatusKind kind,
        VenueEvent venue, float right, float scale)
    {
        var left = topLeft.X;
        if (kind == VenueStatusKind.Live)
        {
            left += MediaOverlay.LivePill(drawList, topLeft, Loc.T(L.Common.Live), scale) + PillGap * scale;
        }
        else if (kind == VenueStatusKind.Open)
        {
            left += MediaOverlay.Pill(drawList, topLeft, Loc.T(L.Venues.OpenNow), MediaOverlay.Fill,
                MediaOverlay.LiveGreen, scale, string.Empty, true,
                Palette.WithAlpha(MediaOverlay.LiveGreen, 0.55f)) + PillGap * scale;
        }

        if (!venue.HasLiveDj(DateTime.UtcNow))
        {
            return;
        }

        var label = Loc.T(L.Venues.ActiveDj);
        if (left + MediaOverlay.PillWidth(label, scale, true, true) > right)
        {
            return;
        }

        MediaOverlay.Pill(drawList, new Vector2(left, topLeft.Y), label, MediaOverlay.Fill, MediaOverlay.White,
            scale, PhoneIcons.Microphone, true);
    }

    private static float StatWidth(string stat, float scale) =>
        Typography.Measure(stat, StatStyle).X + (StatGlyph + 4f) * scale;

    private static float DrawStat(ImDrawListPtr drawList, float left, float centerY, string stat, string glyph,
        Vector4 color, float scale)
    {
        PhoneIcon.Draw(drawList, new Vector2(left + StatGlyph * scale * 0.5f, centerY), glyph, color,
            StatGlyph * scale);
        var size = Typography.Measure(stat, StatStyle);
        Typography.Draw(drawList, new Vector2(left + (StatGlyph + 4f) * scale, centerY - size.Y * 0.5f), stat, color,
            StatStyle);
        return (StatGlyph + 4f) * scale + size.X;
    }

    public static void DrawChipRow(ImDrawListPtr drawList, IReadOnlyList<string> items, float left, float right,
        float top, float scale)
    {
        var gap = 5f * scale;
        var cursor = left;
        for (var index = 0; index < items.Count; index++)
        {
            var tag = items[index];
            var width = VenueChips.Measure(tag, scale);
            var remaining = items.Count - index;
            var reserve = remaining > 1 ? VenueChips.Measure(VenueLabelCache.Plus(remaining - 1), scale) + gap : 0f;
            if (cursor + width + reserve > right)
            {
                VenueChips.DrawNeutral(drawList, new Vector2(cursor, top), VenueLabelCache.Plus(remaining), scale);
                return;
            }

            VenueChips.Draw(drawList, new Vector2(cursor, top), tag, scale);
            cursor += width + gap;
        }
    }

    private static bool HasStatusRow(in VenueCardText text) => text.Status.Label.Length > 0 || text.Stat.Length > 0;

    private static float HeroHeight(float width, float scale) => MathF.Min(width * HeroAspect, HeroMaxHeight * scale);
}

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
    public const float HoverLift = 4f;

    private const float HeroAspect = 0.52f;
    private const float HeroMaxHeight = 200f;
    private const float Pad = 14f;
    private const float PillGap = 6f;
    private const float LogoSide = 40f;
    private const float LogoGap = 11f;
    private const float StatusGap = 8f;
    private const float PerformerGap = 6f;
    private const float PlayingGap = 2f;
    private const float ChipGap = 10f;
    private const float ActionGap = 12f;
    private const float OverlayInset = 12f;
    private const float HeroScrimShare = 0.42f;
    private const float StatGlyph = 13f;
    private const float LineGlyph = 14f;
    private const float RailRounding = 18f;
    private const float RailPad = 11f;
    private const float RailScrimShare = 0.66f;
    private const float RimAlpha = 0.30f;
    private const float RimWeight = 1.5f;
    private const float ActionButtonHeight = Button.RegularHeight;
    private const float FeaturedRounding = 22f;
    private const float FeaturedPad = 14f;
    private const float FeaturedScrimShare = 0.72f;
    private const float GoHeight = Button.RegularHeight;
    private const float LeadColumn = 50f;
    private const float LeadGap = 6f;
    public const float LeadWidth = LeadColumn + LeadGap;
    private const float StarHit = 16f;
    private const float StarGlyph = 18f;
    private const float HoverAlpha = 0.6f;

    private static readonly TextStyle TitleStyle = TextStyles.Headline;
    private static readonly TextStyle MetaStyle = TextStyles.Footnote;
    private static readonly TextStyle StatusStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle PerformerStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle PlayingStyle = TextStyles.Footnote;
    private static readonly TextStyle StatStyle = TextStyles.Caption1;
    private static readonly TextStyle FeaturedTitleStyle = TextStyles.Title2;
    private static readonly TextStyle RowTitleStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle LeadTopStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle RailTitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle RailStatusStyle = TextStyles.Caption1;
    private static readonly Vector4 OverlayMutedInk = new(1f, 1f, 1f, 0.80f);
    private static readonly Vector4 FavoriteInk = new(1f, 0.80f, 0.26f, 1f);

    public static float FeedHeight(VenueEvent venue, in VenueCardText text, float width, float scale, bool actions)
    {
        var header = MathF.Max(Typography.LineHeight(TitleStyle) + 2f * scale + Typography.LineHeight(MetaStyle),
            venue.LogoUrl is null ? 0f : LogoSide * scale);
        var height = HeroHeight(width, scale) + Pad * scale + header + Pad * scale;
        if (HasStatusRow(text))
        {
            height += StatusGap * scale + Typography.LineHeight(StatusStyle);
        }

        if (text.Performer.Length > 0)
        {
            height += PerformerGap * scale + Typography.LineHeight(PerformerStyle);
        }

        if (text.Playing.Length > 0)
        {
            height += PlayingGap * scale + Typography.LineHeight(PlayingStyle);
        }

        if (ChipsOf(venue, text).Count > 0)
        {
            height += ChipGap * scale + VenueChips.Height(scale);
        }

        if (HasActions(venue, actions))
        {
            height += ActionGap * scale + ActionButtonHeight * scale;
        }

        return height;
    }

    private static bool HasActions(VenueEvent venue, bool actions) =>
        actions && (venue.CanTeleport || !string.IsNullOrEmpty(venue.TwitchUrl));

    private static IReadOnlyList<string> ChipsOf(VenueEvent venue, in VenueCardText text) =>
        text.Status.Kind == VenueStatusKind.Live && venue.LiveGenres.Count > 0 ? venue.LiveGenres : venue.Tags;

    public static VenueCardAction DrawFeed(ImDrawListPtr drawList, AppSkin ui, Rect card, VenueEvent venue,
        in VenueCardText text, bool favorite, in VenueArt art, bool actions, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var hero = new Rect(card.Min, new Vector2(card.Max.X, card.Min.Y + HeroHeight(card.Width, scale)));
        drawList.PushClipRect(hero.Min, hero.Max, true);
        VenueImage.Cover(drawList, new Rect(hero.Min, hero.Max + new Vector2(0f, radius)), radius, venue,
            text.Initial, art);
        MediaOverlay.BottomScrim(drawList, hero.Min, hero.Max, HeroScrimShare);
        drawList.PopClipRect();
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ui.HoverTint, ui.HoverTint.W * HoverAlpha)));
        }

        var inset = OverlayInset * scale;
        var starRadius = MediaOverlay.GlassButtonRadius * scale;
        var starCenter = new Vector2(hero.Max.X - inset - starRadius, hero.Min.Y + inset + starRadius);
        DrawStatusPill(drawList, new Vector2(hero.Min.X + inset, hero.Min.Y + inset), text.Status.Kind, venue,
            starCenter.X - starRadius - PillGap * scale, scale);
        var starExtent = new Vector2(starRadius, starRadius);
        var overChild = UiInteract.Hover(starCenter - starExtent, starCenter + starExtent);
        var action = MediaOverlay.GlassButton(drawList, starCenter, favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
            string.Empty, scale, null, favorite ? FavoriteInk : MediaOverlay.White)
            ? VenueCardAction.ToggleFavorite
            : VenueCardAction.None;
        var bodyBottom = PaintBody(drawList, ui, venue, text, card, hero.Max.Y, art, scale);
        if (HasActions(venue, actions))
        {
            var top = bodyBottom + ActionGap * scale;
            var row = new Rect(new Vector2(card.Min.X + Pad * scale, top),
                new Vector2(card.Max.X - Pad * scale, top + ActionButtonHeight * scale));
            overChild |= UiInteract.Hover(row.Min, row.Max);
            var tapped = DrawActionRow(drawList, ui, venue, row, ImGui.GetID(text.PressId), scale);
            if (tapped != VenueCardAction.None)
            {
                action = tapped;
            }
        }

        if (action != VenueCardAction.None)
        {
            return action;
        }

        if (hovered && !overChild)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return !overChild && UiInteract.Click(card.Min, card.Max, hovered)
            ? VenueCardAction.Open
            : VenueCardAction.None;
    }

    private static VenueCardAction DrawActionRow(ImDrawListPtr drawList, AppSkin ui, VenueEvent venue, Rect row,
        uint key, float scale)
    {
        var hasTwitch = !string.IsNullOrEmpty(venue.TwitchUrl);
        var gap = 8f * scale;
        var split = venue.CanTeleport && hasTwitch ? (row.Width - gap) * 0.5f : row.Width;
        var left = row.Min.X;
        var action = VenueCardAction.None;
        if (venue.CanTeleport)
        {
            var rect = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + split, row.Max.Y));
            if (VenuesArt.Action(drawList, ui, rect, key + 1u, Loc.T(L.Travel.GoThere),
                    PhoneIcons.NavigationFilled, false, ButtonStyle.Prominent))
            {
                action = VenueCardAction.Teleport;
            }

            left += split + gap;
        }

        if (hasTwitch)
        {
            var rect = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + split, row.Max.Y));
            if (VenuesArt.Action(drawList, ui, rect, key + 2u, Loc.T(L.Venues.WatchOnTwitch),
                    PhoneIcons.ExternalLink, false, ButtonStyle.Gray))
            {
                action = VenueCardAction.Twitch;
            }
        }

        return action;
    }

    private static float PaintBody(ImDrawListPtr drawList, AppSkin ui, VenueEvent venue, in VenueCardText text,
        Rect card, float top, in VenueArt art, float scale)
    {
        var pad = Pad * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var cursorY = top + pad;
        var textLeft = left;
        var titleHeight = Typography.LineHeight(TitleStyle);
        var metaHeight = Typography.LineHeight(MetaStyle);
        var headerHeight = titleHeight + 2f * scale + metaHeight;
        var headerTop = cursorY;
        if (venue.LogoUrl is not null)
        {
            var side = LogoSide * scale;
            var logo = new Rect(new Vector2(left, cursorY), new Vector2(left + side, cursorY + side));
            VenueImage.Logo(drawList, logo, side * Metrics.Radius.TileFactor, venue, text.Initial, art);
            textLeft = logo.Max.X + LogoGap * scale;
            cursorY += MathF.Max(0f, (side - headerHeight) * 0.5f);
            headerHeight = MathF.Max(headerHeight, side);
        }

        var textWidth = MathF.Max(1f, right - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.card.title.", venue.Id), text.Title, textLeft, cursorY,
            textWidth, TitleStyle, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY + titleHeight + 2f * scale),
            Typography.FitText(text.Meta, textWidth, MetaStyle), ui.MutedInk, MetaStyle);
        cursorY = headerTop + headerHeight;
        if (HasStatusRow(text))
        {
            cursorY += StatusGap * scale;
            DrawStatusRow(drawList, ui, venue, text, left, right, cursorY, scale);
            cursorY += Typography.LineHeight(StatusStyle);
        }

        if (text.Performer.Length > 0)
        {
            cursorY += PerformerGap * scale;
            cursorY += DrawLine(drawList, new MarqueeId("venues.card.performer.", venue.Id), PhoneIcons.Microphone,
                MediaOverlay.LiveGreen, text.Performer, ui.TitleInk, PerformerStyle, left, right, cursorY, scale);
        }

        if (text.Playing.Length > 0)
        {
            cursorY += PlayingGap * scale;
            cursorY += DrawLine(drawList, new MarqueeId("venues.card.playing.", venue.Id), PhoneIcons.Music,
                ui.MutedInk, text.Playing, ui.BodyInk, PlayingStyle, left, right, cursorY, scale);
        }

        var chips = ChipsOf(venue, text);
        if (chips.Count > 0)
        {
            cursorY += ChipGap * scale;
            DrawChipRow(drawList, chips, left, right, cursorY, scale);
            cursorY += VenueChips.Height(scale);
        }

        return cursorY;
    }

    private static float DrawLine(ImDrawListPtr drawList, MarqueeId id, string glyph, Vector4 glyphInk, string text,
        Vector4 ink, in TextStyle style, float left, float right, float top, float scale)
    {
        var lineHeight = Typography.LineHeight(style);
        var glyphSize = LineGlyph * scale;
        PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, top + lineHeight * 0.5f), glyph, glyphInk,
            glyphSize);
        var textLeft = left + glyphSize + 6f * scale;
        Marquee.DrawLeftAuto(drawList, id, text, textLeft, top, MathF.Max(1f, right - textLeft), style, ink);
        return lineHeight;
    }

    public static VenueCardAction DrawFeatured(ImDrawListPtr drawList, Rect rest, VenueEvent venue,
        in VenueCardText text, bool favorite, in VenueArt art, AppSkin ui, Rect clip, bool interactive)
    {
        var scale = UiScale.Current;
        var rounding = FeaturedRounding * scale;
        var pad = FeaturedPad * scale;
        var live = interactive && UiInteract.Hover(clip.Min, clip.Max);
        var hovered = live && UiInteract.Hover(rest.Min, rest.Max);
        var radius = MediaOverlay.GlassButtonRadius * scale;
        var starCenter = new Vector2(rest.Max.X - pad - radius, rest.Min.Y + pad + radius);
        var goRect = GoRect(rest, venue, scale);
        var starExtent = new Vector2(radius, radius);
        var overGo = goRect.Width > 0f && UiInteract.Hover(goRect.Min, goRect.Max);
        var overButtons = hovered && (UiInteract.Hover(starCenter - starExtent, starCenter + starExtent) || overGo);
        var card = Lift(drawList, rest, text.PressId, hovered, hovered && !overButtons, rounding, scale, out var eased);
        VenueImage.Cover(drawList, card, rounding, venue, text.Initial, art);
        Squircle.FillVerticalGradient(drawList, new Vector2(card.Min.X, card.Max.Y - card.Height * FeaturedScrimShare),
            card.Max, rounding, ImGui.GetColorU32(MediaOverlay.ScrimClear), ImGui.GetColorU32(MediaOverlay.ScrimDeep));
        DrawRim(drawList, card, rounding, eased, scale);
        var action = VenueCardAction.None;
        DrawStatusPill(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), text.Status.Kind, venue,
            card.Max.X - pad - radius * 2f - PillGap * scale, scale);
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
        if (goRect.Width > 0f)
        {
            if (DrawGoButton(drawList, goRect, Loc.T(L.Travel.GoThere), ui,
                    live && InsideClip(clip, goRect.Min.X, goRect.Max.X), unchecked(ImGui.GetID(text.PressId) + 3u)))
            {
                action = VenueCardAction.Teleport;
            }

            metaRight = goRect.Min.X - 10f * scale;
        }

        var metaTop = bottomRowTop + (bottomRowHeight - metaHeight) * 0.5f;
        var titleTop = bottomRowTop - titleHeight - 2f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, metaTop),
            Typography.FitText(text.Meta, MathF.Max(1f, metaRight - textLeft), MetaStyle), OverlayMutedInk, MetaStyle);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.featured.title.", venue.Id), text.Title, textLeft,
            titleTop, textWidth, FeaturedTitleStyle, MediaOverlay.White);
        var lineTop = titleTop;
        if (text.Status.Label.Length > 0)
        {
            lineTop -= statusHeight;
            var tint = text.Status.Kind == VenueStatusKind.Upcoming ? MediaOverlay.White : MediaOverlay.LiveGreen;
            Typography.Draw(drawList, new Vector2(textLeft, lineTop),
                Typography.FitText(text.Status.Label, textWidth, StatusStyle), tint, StatusStyle);
        }

        if (text.Performer.Length > 0)
        {
            lineTop -= statusHeight + 2f * scale;
            Typography.Draw(drawList, new Vector2(textLeft, lineTop),
                Typography.FitText(text.Performer, textWidth, StatusStyle), MediaOverlay.White, StatusStyle);
        }

        if (hovered && action == VenueCardAction.None && !overButtons)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (action != VenueCardAction.None)
        {
            return action;
        }

        return live && !overButtons && UiInteract.Click(rest.Min, rest.Max, hovered)
            ? VenueCardAction.Open
            : VenueCardAction.None;
    }

    private static Rect GoRect(Rect card, VenueEvent venue, float scale)
    {
        if (!venue.CanTeleport)
        {
            return default;
        }

        var pad = FeaturedPad * scale;
        var label = Loc.T(L.Travel.GoThere);
        var width = VenuesArt.ActionWidth(label, PhoneIcons.NavigationFilled, false, GoHeight * scale);
        var top = card.Max.Y - pad - GoHeight * scale;
        return new Rect(new Vector2(card.Max.X - pad - width, top),
            new Vector2(card.Max.X - pad, top + GoHeight * scale));
    }

    public static Rect Lift(ImDrawListPtr drawList, Rect rest, string id, bool hovered, bool pressable, float rounding,
        float scale, out float eased)
    {
        eased = HoverFx.Amount(id, hovered);
        var pressed = pressable && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = HoverLift * scale * eased -
                   rest.Width * 0.5f * (1f - PressFx.Scale(id, pressed, PressFx.CardPressedScale));
        var card = new Rect(rest.Min - new Vector2(grow, grow), rest.Max + new Vector2(grow, grow));
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

    private static bool DrawGoButton(ImDrawListPtr drawList, Rect rect, string label, AppSkin ui, bool interactive,
        uint key)
    {
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink, ButtonStyle.Prominent, ButtonRole.Normal, true, hovered, key);
        VenuesArt.DrawGlyphLabel(drawList, face, label, PhoneIcons.NavigationFilled, false);
        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static VenueCardAction DrawRow(ImDrawListPtr drawList, AppSkin ui, Rect row, VenueEvent venue,
        in VenueCardText text, bool favorite, in VenueArt art, float scale, string leadTop = "",
        string leadBottom = "", bool leadLive = false, string title = "")
    {
        var pad = VenuesArt.RowPad * scale;
        var left = row.Min.X + pad;
        var centerY = row.Center.Y;
        var starCenter = new Vector2(row.Max.X - pad - StarGlyph * scale * 0.5f, centerY);
        var starExtent = new Vector2(StarHit * scale, StarHit * scale);
        var overStar = UiInteract.Hover(starCenter - starExtent, starCenter + starExtent);
        var hovered = !overStar && VenuesArt.RowWash(drawList, ui, row, scale);
        if (leadTop.Length > 0 || leadLive)
        {
            var columnWidth = LeadColumn * scale;
            var topStyle = leadLive ? StatusStyle : LeadTopStyle;
            var topInk = leadLive ? MediaOverlay.LiveGreen : ui.TitleInk;
            var topLabel = leadLive ? Loc.T(L.Common.Live) : leadTop;
            var topHeight = Typography.LineHeight(topStyle);
            var bottomHeight = leadBottom.Length > 0 ? Typography.LineHeight(StatStyle) : 0f;
            var blockTop = centerY - (topHeight + bottomHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(left, blockTop), Typography.FitText(topLabel, columnWidth, topStyle),
                topInk, topStyle);
            if (leadBottom.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(left, blockTop + topHeight),
                    Typography.FitText(leadBottom, columnWidth, StatStyle), ui.MutedInk, StatStyle);
            }

            left += columnWidth + LeadGap * scale;
        }

        var thumbSide = VenuesArt.RowThumb * scale;
        var thumb = new Rect(new Vector2(left, centerY - thumbSide * 0.5f),
            new Vector2(left + thumbSide, centerY + thumbSide * 0.5f));
        var thumbRadius = thumbSide * Metrics.Radius.TileFactor;
        if (venue.LogoUrl is not null)
        {
            VenueImage.Logo(drawList, thumb, thumbRadius, venue, text.Initial, art);
        }
        else
        {
            VenueImage.Cover(drawList, thumb, thumbRadius, venue, text.Initial, art);
        }

        PhoneIcon.Draw(drawList, starCenter, favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
            favorite ? FavoriteInk : overStar ? ui.TitleInk : ui.MutedInk, StarGlyph * scale);
        if (overStar)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var starTapped = UiInteract.Click(starCenter - starExtent, starCenter + starExtent, overStar);
        var textLeft = thumb.Max.X + VenuesArt.TextGap * scale;
        var textWidth = MathF.Max(1f, starCenter.X - StarHit * scale - textLeft);
        var heading = title.Length > 0 ? title : text.Title;
        var subline = title.Length > 0 ? text.Title : text.Meta;
        var thirdLine = title.Length > 0 ? text.Meta : text.Status.Label;
        var headingHeight = Typography.LineHeight(RowTitleStyle);
        var sublineHeight = Typography.LineHeight(MetaStyle);
        var thirdHeight = thirdLine.Length > 0 ? Typography.LineHeight(StatStyle) : 0f;
        var top = centerY - (headingHeight + sublineHeight + thirdHeight) * 0.5f;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.row.title.", venue.Id), heading, textLeft, top, textWidth,
            RowTitleStyle, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, top + headingHeight),
            Typography.FitText(subline, textWidth, MetaStyle), ui.MutedInk, MetaStyle);
        if (thirdLine.Length > 0)
        {
            var thirdInk = title.Length > 0 ? ui.MutedInk : StatusTint(text.Status.Kind, ui);
            Typography.Draw(drawList, new Vector2(textLeft, top + headingHeight + sublineHeight),
                Typography.FitText(thirdLine, textWidth, StatStyle), thirdInk, StatStyle);
        }

        if (starTapped)
        {
            return VenueCardAction.ToggleFavorite;
        }

        return !overStar && UiInteract.Click(row.Min, row.Max, hovered) ? VenueCardAction.Open : VenueCardAction.None;
    }

    public static bool DrawRail(ImDrawListPtr drawList, Rect rest, VenueEvent venue, in VenueCardText text,
        in VenueArt art, Rect clip, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && UiInteract.Hover(clip.Min, clip.Max) && UiInteract.Hover(rest.Min, rest.Max);
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
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.rail.title.", venue.Id), text.Title, card.Min.X + pad,
            titleTop, textWidth, RailTitleStyle, MediaOverlay.White);
        var statusLeft = card.Min.X + pad;
        if (text.Stat.Length > 0)
        {
            statusLeft += DrawStat(drawList, statusLeft, statusTop + statusHeight * 0.5f, text.Stat,
                venue.LiveViewers > 0 ? PhoneIcons.Eye : PhoneIcons.Users, OverlayMutedInk, scale) + 8f * scale;
        }

        var railStatus = text.Status.Label.Length > 0 ? text.Status.Label : venue.World;
        Typography.Draw(drawList, new Vector2(statusLeft, statusTop),
            Typography.FitText(railStatus, MathF.Max(1f, card.Max.X - pad - statusLeft), RailStatusStyle),
            OverlayMutedInk, RailStatusStyle);
        return interactive && UiInteract.Click(rest.Min, rest.Max, hovered);
    }

    private static void DrawStatusRow(ImDrawListPtr drawList, AppSkin ui, VenueEvent venue, in VenueCardText text,
        float left, float right, float top, float scale)
    {
        var lineHeight = Typography.LineHeight(StatusStyle);
        var centerY = top + lineHeight * 0.5f;
        var statRight = right;
        if (text.Stat.Length > 0)
        {
            var statWidth = StatWidth(text.Stat, scale);
            DrawStat(drawList, right - statWidth, centerY, text.Stat,
                venue.LiveViewers > 0 ? PhoneIcons.Eye : PhoneIcons.Users, ui.MutedInk, scale);
            statRight = right - statWidth - 10f * scale;
        }

        if (text.Status.Label.Length == 0)
        {
            return;
        }

        var cursorX = left;
        var tint = StatusTint(text.Status.Kind, ui);
        if (text.Status.Kind is VenueStatusKind.Live or VenueStatusKind.Open)
        {
            MediaOverlay.LiveDot(drawList, new Vector2(cursorX + 5f * scale, centerY), tint, scale);
            cursorX += 16f * scale;
        }

        Typography.Draw(drawList, new Vector2(cursorX, top),
            Typography.FitText(text.Status.Label, MathF.Max(1f, statRight - cursorX), StatusStyle), tint,
            StatusStyle);
    }

    public static Vector4 StatusTint(VenueStatusKind kind, AppSkin ui) =>
        kind switch
        {
            VenueStatusKind.Live => MediaOverlay.LiveGreen,
            VenueStatusKind.Open => Palette.WithAlpha(MediaOverlay.LiveGreen, 0.86f),
            _ => Palette.Lighten(ui.Accent, 0.18f),
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

    internal static float HeroHeight(float width, float scale) => MathF.Min(width * HeroAspect, HeroMaxHeight * scale);
}

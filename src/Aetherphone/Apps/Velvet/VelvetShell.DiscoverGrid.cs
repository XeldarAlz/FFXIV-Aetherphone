using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const string ListSurfaceId = "velvetListSurface";
    private const int GridFillBelow = 8;
    private const float GridGap = 10f;
    private const float GridCardAspect = 0.82f;
    private const float GridCoverFocus = 0.2f;
    private const float GridDetailGlyphGap = 6f;
    private const float GridActionRadius = 22f;
    private const float GridActionGap = 10f;
    private const float GridActionTextGap = 12f;
    private const float GridRequestedHeight = 28f;
    private const float GridRequestedGlyphGap = 6f;
    private const float GridHoverLift = 4f;
    private const float GridHoverSmoothTime = 0.11f;
    private const float GridPressShrink = 0.97f;
    private const float GridShadowOpacity = 0.35f;
    private const float GridFooterTop = 18f;
    private const float GridBottomPad = 28f;

    private readonly List<GridLabel> gridLabels = new();
    private readonly List<VelvetFitItem> gridFitScratch = new();
    private LanguageInfo? gridLabelsLanguage;
    private int gridLabelsVersion = -1;
    private string gridHiddenLabel = string.Empty;
    private int gridHiddenCount = -1;
    private bool deckModeSynced;

    private readonly record struct GridLabel(string NameId, string SayId, string ConnectId, string MetaLine,
        string PhotoBadge, string Fit, bool FitWarns);

    private void DrawDiscoverGrid(Rect body)
    {
        RefillDeck(GridFillBelow);
        if (deck.Count == 0)
        {
            DrawDeckEmpty(body);
            return;
        }

        EnsureStamps();
        EnsureGridLabels();
        var scale = UiScale.Current;
        using (ImRaii.PushId(ListSurfaceId))
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var inset = DeckCardInset * scale;
            var gap = GridGap * scale;
            var cardWidth = MathF.Max(1f, width - inset * 2f);
            var cardHeight = cardWidth * GridCardAspect;
            var origin = ImGui.GetCursorScreenPos();
            for (var index = 0; index < deck.Count; index++)
            {
                var min = new Vector2(origin.X + inset, origin.Y + inset + index * (cardHeight + gap));
                var card = new Rect(min, new Vector2(min.X + cardWidth, min.Y + cardHeight));
                if (index == 0)
                {
                    UiAnchors.Report("velvet.discover.card", card);
                }

                if (ImGui.IsRectVisible(card.Min, card.Max))
                {
                    DrawGridCard(drawList, index, card, scale);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, inset + deck.Count * cardHeight + (deck.Count - 1) * gap));
            DrawGridFooter(origin.X + width * 0.5f, width);
            Gap(GridBottomPad);
        }
    }

    private void DrawGridCard(ImDrawListPtr drawList, int index, Rect card, float scale)
    {
        var profile = deck[index];
        var label = gridLabels[index];
        var name = DisplayNameOf(profile.DisplayName, profile.Handle);
        var photos = CardPhotos(profile);
        var pad = DeckCoverPad * scale;
        var actionsWidth = GridActionRadius * 4f * scale + GridActionGap * scale;
        var actionStrip = new Rect(
            new Vector2(card.Max.X - pad - actionsWidth, card.Max.Y - pad - GridActionRadius * 2f * scale),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var overActions = hovered && UiInteract.Hover(actionStrip.Min, actionStrip.Max);
        var pressed = hovered && !overActions && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var eased = VAnim.Toggle(label.NameId, hovered, delta, GridHoverSmoothTime);
        var grow = GridHoverLift * scale * eased
            - card.Width * 0.5f * (1f - PressFx.Scale(label.NameId, pressed, GridPressShrink));
        var body = new Rect(card.Min - new Vector2(grow, grow), card.Max + new Vector2(grow, grow));
        var radius = DeckCoverRadius * scale;
        Elevation.Card(drawList, body.Min, body.Max, radius, scale, GridShadowOpacity * (1f + eased));
        var coverUrl = photos.Length > 0 ? photos[0].Url : profile.AvatarUrl ?? string.Empty;
        DrawCoverImage(drawList, body.Min, body.Max, coverUrl, radius, name, GridCoverFocus);
        Squircle.FillVerticalGradient(drawList, new Vector2(body.Min.X, body.Max.Y - body.Height * DeckScrimShare),
            body.Max, radius, VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.94f).Packed());
        if (eased > 0.001f)
        {
            Squircle.Stroke(drawList, body.Min, body.Max, radius,
                VelvetTheme.Alpha(VelvetTheme.OnAccent, DeckRimAlpha * eased).Packed(), DeckRimWeight * scale);
        }

        var badgeLeft = DrawDeckSeenBadge(drawList, profile.UserId, body, body.Min.X + pad, scale);
        if (label.PhotoBadge.Length > 0)
        {
            DrawDeckCoverBadge(drawList, label.PhotoBadge, PhoneIcons.Photo, VelvetTheme.RoseInk, body, badgeLeft,
                scale);
        }

        DrawDeckPresence(drawList, profile.Presence, body, pad, scale);

        var actionHovered = DrawGridActions(drawList, profile, in label, body, pad, actionsWidth, scale);
        var bodyHovered = hovered && !actionHovered;
        var textLeft = body.Min.X + pad;
        var wideWidth = MathF.Max(1f, body.Width - pad * 2f);
        var narrowWidth = MathF.Max(1f, wideWidth - actionsWidth - GridActionTextGap * scale);
        var actionTop = actionStrip.Min.Y - GridActionTextGap * scale;
        var lineGap = DeckCoverLineGap * scale;
        var intentHeight = Typography.LineHeight(DeckIntentStyle);
        var bottom = body.Max.Y - pad;
        if (label.Fit.Length > 0)
        {
            bottom -= intentHeight;
            DrawGridFit(drawList, in label, textLeft, bottom,
                GridTextWidth(bottom, intentHeight, actionTop, wideWidth, narrowWidth), scale);
            bottom -= lineGap;
        }

        var intentY = bottom - intentHeight;
        Typography.Draw(drawList, new Vector2(textLeft, intentY),
            Typography.FitText(VelvetIntent.Summary(profile.LookingFor),
                GridTextWidth(intentY, intentHeight, actionTop, wideWidth, narrowWidth), DeckIntentStyle),
            VelvetTheme.RoseInk, DeckIntentStyle);
        bottom = intentY - lineGap;
        if (label.MetaLine.Length > 0)
        {
            var metaHeight = Typography.LineHeight(DeckMetaStyle);
            var metaY = bottom - metaHeight;
            Typography.Draw(drawList, new Vector2(textLeft, metaY),
                Typography.FitText(label.MetaLine,
                    GridTextWidth(metaY, metaHeight, actionTop, wideWidth, narrowWidth), DeckMetaStyle),
                VelvetTheme.BodyInk, DeckMetaStyle);
            bottom = metaY - lineGap;
        }

        var nameHeight = Typography.LineHeight(DeckNameStyle);
        var nameY = bottom - nameHeight;
        UserName.Draw(drawList, label.NameId, name, profile.Badges, profile.BadgeIds, textLeft, nameY,
            GridTextWidth(nameY, nameHeight, actionTop, wideWidth, narrowWidth), DeckNameStyle, VelvetTheme.TitleInk,
            bodyHovered, false);
        if (bodyHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, bodyHovered))
        {
            OpenProfile(profile.UserId);
        }
    }

    private static float GridTextWidth(float top, float height, float actionTop, float wide, float narrow) =>
        top + height > actionTop ? narrow : wide;

    private bool DrawGridActions(ImDrawListPtr drawList, VelvetProfileDto profile, in GridLabel label, Rect body,
        float pad, float actionsWidth, float scale)
    {
        if (profile.ConnectionState != VelvetConnectionState.None)
        {
            DrawGridRequested(drawList, body, pad, actionsWidth, scale);
            return false;
        }

        var radius = GridActionRadius * scale;
        var centerY = body.Max.Y - pad - radius;
        var connectCenter = new Vector2(body.Max.X - pad - radius, centerY);
        var sayCenter = new Vector2(connectCenter.X - radius * 2f - GridActionGap * scale, centerY);
        var say = DrawGridAction(drawList, label.SayId, sayCenter, radius, PhoneIcons.MessageCircle, VelvetTheme.CardHi,
            VelvetTheme.Card, VelvetTheme.TitleInk, 0f, Loc.T(L.Velvet.DeckSay), scale, out var sayHovered);
        var connect = DrawGridAction(drawList, label.ConnectId, connectCenter, radius, PhoneIcons.HeartFilled,
            VelvetTheme.Rose, VelvetTheme.RoseDeep, VelvetTheme.OnAccent, DeckGlowReach, Loc.T(L.Velvet.Connect),
            scale, out var connectHovered);
        if (say)
        {
            RequestIntro(profile.UserId, profile.DisplayName, profile.Handle, profile.AvatarUrl);
        }
        else if (connect)
        {
            store.Connect(profile.UserId);
        }

        return sayHovered || connectHovered;
    }

    private static bool DrawGridAction(ImDrawListPtr drawList, string id, Vector2 center, float radius, string glyph,
        Vector4 fill, Vector4 deep, Vector4 ink, float glowReach, string tooltip, float scale, out bool hovered)
    {
        var extent = new Vector2(radius, radius);
        hovered = UiInteract.Hover(center - extent, center + extent);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var eased = Math.Clamp(VAnim.Toggle(id, hovered, delta, GridHoverSmoothTime), 0f, 1f);
        var drawRadius = radius * (1f + DeckHoverGrow * eased) * PressFx.Scale(id, pressed, DeckPressShrink);
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), drawRadius, DeckShadow.Packed(), 32);
        AccentGloss.Circle(drawList, center, drawRadius, DeckBodyTone(fill, DeckHoverTopLift * eased, 1f),
            DeckBodyTone(deep, DeckHoverBottomLift * eased, 1f), scale, eased, glowReach);
        if (eased > 0.001f)
        {
            drawList.AddCircle(center, drawRadius,
                VelvetTheme.Alpha(VelvetTheme.OnAccent, DeckRimAlpha * eased).Packed(), 40, DeckRimWeight * scale);
        }

        PhoneIcon.Draw(drawList, center, glyph, ink, VIcon.Overflow * scale * drawRadius / radius);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(id, new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private static void DrawGridRequested(ImDrawListPtr drawList, Rect body, float pad, float actionsWidth,
        float scale)
    {
        var height = GridRequestedHeight * scale;
        var max = new Vector2(body.Max.X - pad, body.Max.Y - pad);
        var min = new Vector2(max.X - actionsWidth, max.Y - height);
        var rounding = height * 0.5f;
        Squircle.Fill(drawList, min, max, rounding, DeckBadgeFill.Packed());
        Squircle.Stroke(drawList, min, max, rounding, VelvetTheme.Hairline.Packed(), Metrics.Stroke.Hairline * scale);
        var glyphSize = VIcon.Small * scale;
        var gap = GridRequestedGlyphGap * scale;
        var label = Typography.FitText(Loc.T(L.Social.Requested),
            MathF.Max(1f, actionsWidth - rounding * 2f - glyphSize - gap), TextStyles.Footnote);
        var textSize = Typography.Measure(label, TextStyles.Footnote);
        var centerY = (min.Y + max.Y) * 0.5f;
        var left = (min.X + max.X) * 0.5f - (glyphSize + gap + textSize.X) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, centerY), PhoneIcons.Check,
            VelvetTheme.Moonlight, glyphSize);
        Typography.Draw(drawList, new Vector2(left + glyphSize + gap, centerY - textSize.Y * 0.5f), label,
            VelvetTheme.BodyInk, TextStyles.Footnote);
    }

    private static void DrawGridFit(ImDrawListPtr drawList, in GridLabel label, float left, float top, float width,
        float scale)
    {
        var glyphSize = VIcon.Chip * scale;
        var glyphCenter = new Vector2(left + glyphSize * 0.5f, top + Typography.LineHeight(DeckIntentStyle) * 0.5f);
        PhoneIcon.Draw(drawList, glyphCenter, label.FitWarns ? PhoneIcons.Ban : PhoneIcons.Check,
            label.FitWarns ? VelvetTheme.Danger : VelvetTheme.Online, glyphSize);
        var textLeft = left + glyphSize + GridDetailGlyphGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(label.Fit, MathF.Max(1f, width - glyphSize - GridDetailGlyphGap * scale),
                DeckIntentStyle),
            label.FitWarns ? VelvetTheme.ToneInk(VelvetTheme.Danger) : VelvetTheme.BodyInk, DeckIntentStyle);
    }

    private void DrawGridFooter(float centerX, float width)
    {
        if (store.LoadingMoreDiscover)
        {
            InfiniteScroll.DrawLoadingRow(centerX, VelvetTheme.MutedInk);
            return;
        }

        if (store.HasMoreDiscover)
        {
            if (!store.LoadingDiscover && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreDiscover();
            }

            return;
        }

        var passCount = store.PassCount;
        if (passCount == 0 || store.LoadingDiscover)
        {
            return;
        }

        if (gridHiddenCount != passCount)
        {
            gridHiddenCount = passCount;
            gridHiddenLabel = Loc.T(L.Velvet.DeckPassedHidden, passCount);
        }

        var scale = UiScale.Current;
        Gap(GridFooterTop);
        var origin = ImGui.GetCursorScreenPos();
        var hintBottom = Typography.DrawWrappedCentered(ImGui.GetWindowDrawList(), gridHiddenLabel,
            TextStyles.Footnote, VelvetTheme.MutedInk, new Vector2(centerX, origin.Y),
            MathF.Max(1f, width - FeedCell.PadX * 2f * scale));
        var actionTop = hintBottom + EndActionGap * scale;
        var body = new Rect(new Vector2(centerX - width * 0.5f, origin.Y),
            new Vector2(centerX + width * 0.5f, actionTop + EndActionHeight * scale));
        if (DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DeckShowAgain), ConfirmButtonTone.Neutral,
                "velvet.grid.showAgain"))
        {
            ShowPassesAgain();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, actionTop - origin.Y));
    }

    private void EnsureGridLabels()
    {
        if (gridLabelsVersion == deckVersion && ReferenceEquals(gridLabelsLanguage, Loc.Current))
        {
            return;
        }

        gridLabelsVersion = deckVersion;
        gridLabelsLanguage = Loc.Current;
        gridHiddenCount = -1;
        gridLabels.Clear();
        for (var index = 0; index < deck.Count; index++)
        {
            gridLabels.Add(GridLabelOf(deck[index]));
        }
    }

    private GridLabel GridLabelOf(VelvetProfileDto profile)
    {
        var nameId = "velvet.grid.name." + profile.UserId;
        var sayId = "velvet.grid.say." + profile.UserId;
        var connectId = "velvet.grid.connect." + profile.UserId;
        var metaLine = GridMetaLine(profile);
        var photoCount = CardPhotos(profile).Length;
        var photoBadge = photoCount > 1 ? Loc.Plural(L.Velvet.PhotoBadge, photoCount) : string.Empty;
        if (deckMe is { } me)
        {
            VelvetFit.Describe(me, profile, gridFitScratch);
            var headline = VelvetFit.Headline(gridFitScratch);
            if (headline >= 0)
            {
                var item = gridFitScratch[headline];
                return new GridLabel(nameId, sayId, connectId, metaLine, photoBadge, FitLabel(in item),
                    VelvetFit.Warns(item.Kind));
            }
        }

        return new GridLabel(nameId, sayId, connectId, metaLine, photoBadge, string.Empty, false);
    }

    private string GridMetaLine(VelvetProfileDto profile)
    {
        var region = RegionCodeOf(profile);
        var race = profile.Race > 0 ? VelvetRace.Label(gameData, profile.Race) : string.Empty;
        if (region.Length == 0)
        {
            return race;
        }

        return race.Length > 0 ? string.Concat(region, FilterSummarySeparator, race) : region;
    }
}

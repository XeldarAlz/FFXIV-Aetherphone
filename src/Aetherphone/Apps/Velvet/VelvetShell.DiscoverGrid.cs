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
    private const float GridActionRadius = RoundButton.RegularRadius;
    private const float GridActionTextGap = 12f;
    private const float GridRequestedWidth = 98f;
    private const float GridRequestedHeight = 28f;
    private const float GridRequestedGlyphGap = 6f;
    private const float GridHoverLift = 4f;
    private const float GridBottomPad = 28f;

    private readonly List<GridLabel> gridLabels = new();
    private readonly List<VelvetFitItem> gridFitScratch = new();
    private LanguageInfo? gridLabelsLanguage;
    private int gridLabelsVersion = -1;
    private bool gridScrollTopPending;

    private readonly record struct GridLabel(string NameId, string ConnectId, string MetaLine, string Fit,
        bool FitWarns);

    private void DrawDiscoverGrid(Rect body)
    {
        RefillCards(GridFillBelow);
        if (cards.Count == 0)
        {
            DrawDiscoverEmpty(body);
            return;
        }

        EnsureGridLabels();
        var scale = UiScale.Current;
        using (ImRaii.PushId(ListSurfaceId))
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            if (gridScrollTopPending)
            {
                surface.JumpToTop();
                gridScrollTopPending = false;
            }

            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var inset = CardInset * scale;
            var gap = GridGap * scale;
            var cardWidth = MathF.Max(1f, width - inset * 2f);
            var cardHeight = cardWidth * GridCardAspect;
            var origin = ImGui.GetCursorScreenPos();
            for (var index = 0; index < cards.Count; index++)
            {
                var min = new Vector2(origin.X + inset, origin.Y + inset + index * (cardHeight + gap));
                var card = new Rect(min, new Vector2(min.X + cardWidth, min.Y + cardHeight));
                if (!ImGui.IsRectVisible(card.Min, card.Max))
                {
                    continue;
                }

                if (index == 0)
                {
                    UiAnchors.Report("velvet.discover.card", card);
                }

                DrawGridCard(drawList, index, card, scale);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, inset + cards.Count * cardHeight + (cards.Count - 1) * gap));
            DrawGridFooter(origin.X + width * 0.5f);
            Gap(GridBottomPad);
        }
    }

    private void DrawGridCard(ImDrawListPtr drawList, int index, Rect card, float scale)
    {
        var profile = cards[index];
        var label = gridLabels[index];
        var name = DisplayNameOf(profile.DisplayName, profile.Handle);
        var photos = CardPhotos(profile);
        var pad = CardCoverPad * scale;
        var actionsWidth = GridActionsWidth(profile) * scale;
        var actionStrip = new Rect(
            new Vector2(card.Max.X - pad - actionsWidth, card.Max.Y - pad - GridActionRadius * 2f * scale),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var overActions = hovered && UiInteract.Hover(actionStrip.Min, actionStrip.Max);
        var pressed = hovered && !overActions && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var eased = VAnim.Toggle(label.NameId, hovered, delta, Motion.HoverLift);
        var grow = GridHoverLift * scale * eased
            - card.Width * 0.5f * (1f - PressFx.Scale(label.NameId, pressed, PressFx.CardPressedScale));
        var body = new Rect(card.Min - new Vector2(grow, grow), card.Max + new Vector2(grow, grow));
        var radius = CardCoverRadius * scale;
        var coverUrl = photos.Length > 0 ? photos[0].Url : profile.AvatarUrl ?? string.Empty;
        DrawCoverImage(drawList, body.Min, body.Max, coverUrl, radius, name, GridCoverFocus);
        Squircle.FillVerticalGradient(drawList, new Vector2(body.Min.X, body.Max.Y - body.Height * CardScrimShare),
            body.Max, radius, VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.94f).Packed());
        if (eased > 0.001f)
        {
            Squircle.Stroke(drawList, body.Min, body.Max, radius,
                VelvetTheme.Alpha(VelvetTheme.OnAccent, CardRimAlpha * eased).Packed(), CardRimWeight * scale);
        }

        DrawCardPresence(drawList, profile.Presence, body, pad, scale);

        var actionHovered = DrawGridActions(drawList, profile, in label, body, pad, actionsWidth, scale);
        var bodyHovered = hovered && !actionHovered;
        var textLeft = body.Min.X + pad;
        var wideWidth = MathF.Max(1f, body.Width - pad * 2f);
        var narrowWidth = MathF.Max(1f, wideWidth - actionsWidth - GridActionTextGap * scale);
        var actionTop = actionStrip.Min.Y - GridActionTextGap * scale;
        var lineGap = CardCoverLineGap * scale;
        var intentHeight = Typography.LineHeight(CardIntentStyle);
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
                GridTextWidth(intentY, intentHeight, actionTop, wideWidth, narrowWidth), CardIntentStyle),
            VelvetTheme.RoseInk, CardIntentStyle);
        bottom = intentY - lineGap;
        if (label.MetaLine.Length > 0)
        {
            var metaHeight = Typography.LineHeight(CardMetaStyle);
            var metaY = bottom - metaHeight;
            Typography.Draw(drawList, new Vector2(textLeft, metaY),
                Typography.FitText(label.MetaLine,
                    GridTextWidth(metaY, metaHeight, actionTop, wideWidth, narrowWidth), CardMetaStyle),
                VelvetTheme.BodyInk, CardMetaStyle);
            bottom = metaY - lineGap;
        }

        var nameHeight = Typography.LineHeight(CardNameStyle);
        var nameY = bottom - nameHeight;
        UserName.Draw(drawList, label.NameId, name, profile.Badges, profile.BadgeIds, textLeft, nameY,
            GridTextWidth(nameY, nameHeight, actionTop, wideWidth, narrowWidth), CardNameStyle, VelvetTheme.TitleInk,
            bodyHovered, false, self: RewardedSelf(profile.UserId));
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

    private static float GridActionsWidth(VelvetProfileDto profile) =>
        profile.ConnectionState != VelvetConnectionState.None ? GridRequestedWidth : GridActionRadius * 2f;

    private bool DrawGridActions(ImDrawListPtr drawList, VelvetProfileDto profile, in GridLabel label, Rect body,
        float pad, float actionsWidth, float scale)
    {
        if (profile.ConnectionState != VelvetConnectionState.None)
        {
            DrawGridRequested(drawList, body, pad, actionsWidth, scale);
            return false;
        }

        var radius = GridActionRadius * scale;
        var connectCenter = new Vector2(body.Max.X - pad - radius, body.Max.Y - pad - radius);
        if (DrawGridAction(drawList, label.ConnectId, connectCenter, radius, PhoneIcons.HeartFilled,
                Loc.T(L.Velvet.Connect), scale, out var connectHovered))
        {
            RequestIntro(profile.UserId, profile.DisplayName, profile.Handle, profile.AvatarUrl);
        }

        return connectHovered;
    }

    private static bool DrawGridAction(ImDrawListPtr drawList, string id, Vector2 center, float radius, string glyph,
        string tooltip, float scale, out bool hovered)
    {
        var extent = new Vector2(radius, radius);
        var clicked = RoundButton.Draw(drawList, ImGui.GetID(id), center, radius, VelvetTheme.Ink,
            ButtonStyle.Prominent, true, false, out var face);
        hovered = face.Hovered;
        PhoneIcon.Draw(drawList, center, glyph, face.LabelInk,
            VIcon.Overflow * scale * face.Face.Width / MathF.Max(radius * 2f, 0.0001f));
        HoverTooltip.Show(id, new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Above);
        return clicked;
    }

    private static void DrawGridRequested(ImDrawListPtr drawList, Rect body, float pad, float actionsWidth,
        float scale)
    {
        var height = GridRequestedHeight * scale;
        var max = new Vector2(body.Max.X - pad, body.Max.Y - pad);
        var min = new Vector2(max.X - actionsWidth, max.Y - height);
        var rounding = height * 0.5f;
        Squircle.Fill(drawList, min, max, rounding, CardBadgeFill.Packed());
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
        var glyphCenter = new Vector2(left + glyphSize * 0.5f, top + Typography.LineHeight(CardIntentStyle) * 0.5f);
        PhoneIcon.Draw(drawList, glyphCenter, label.FitWarns ? PhoneIcons.Ban : PhoneIcons.Check,
            label.FitWarns ? VelvetTheme.Danger : VelvetTheme.Online, glyphSize);
        var textLeft = left + glyphSize + GridDetailGlyphGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(label.Fit, MathF.Max(1f, width - glyphSize - GridDetailGlyphGap * scale),
                CardIntentStyle),
            label.FitWarns ? VelvetTheme.ToneInk(VelvetTheme.Danger) : VelvetTheme.BodyInk, CardIntentStyle);
    }

    private void DrawGridFooter(float centerX)
    {
        if (store.LoadingMoreDiscover)
        {
            InfiniteScroll.DrawLoadingRow(centerX, VelvetTheme.MutedInk);
            return;
        }

        if (!store.HasMoreDiscover || store.LoadingDiscover || !InfiniteScroll.ReachedBottom())
        {
            return;
        }

        store.LoadMoreDiscover();
    }

    private void EnsureGridLabels()
    {
        if (gridLabelsVersion == cardsVersion && ReferenceEquals(gridLabelsLanguage, Loc.Current))
        {
            return;
        }

        gridLabelsVersion = cardsVersion;
        gridLabelsLanguage = Loc.Current;
        gridLabels.Clear();
        for (var index = 0; index < cards.Count; index++)
        {
            gridLabels.Add(GridLabelOf(cards[index]));
        }
    }

    private GridLabel GridLabelOf(VelvetProfileDto profile)
    {
        var nameId = "velvet.grid.name." + profile.UserId;
        var connectId = "velvet.grid.connect." + profile.UserId;
        var metaLine = GridMetaLine(profile);
        if (cardViewer is { } me)
        {
            VelvetFit.Describe(me, profile, gridFitScratch);
            var headline = VelvetFit.Headline(gridFitScratch);
            if (headline >= 0)
            {
                var item = gridFitScratch[headline];
                return new GridLabel(nameId, connectId, metaLine, FitLabel(in item), VelvetFit.Warns(item.Kind));
            }
        }

        return new GridLabel(nameId, connectId, metaLine, string.Empty, false);
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

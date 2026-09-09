using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float DeckCardInset = 12f;
    private const float DeckCoverAspect = 1.2f;
    private const float DeckCoverMaxShare = 0.62f;
    private const float DeckCoverRadius = 18f;
    private const float DeckPhotoRadius = 14f;
    private const float DeckStackPeek = 6f;
    private const float DeckStackShrink = 10f;
    private const float DeckScrimShare = 0.55f;
    private const float DeckCoverPad = 16f;
    private const float DeckCoverLineGap = 4f;
    private const float DeckBadgeHeight = 24f;
    private const float DeckBadgePad = 10f;
    private const float DeckBadgeGlyphGap = 6f;
    private const float DeckBadgeGap = 6f;
    private const float DeckPresenceDot = 4f;
    private const float DeckStampPad = 10f;
    private const float DeckStampStroke = 2.5f;
    private const float DeckStampMinProgress = 0.05f;
    private const float DeckColumnLead = 4f;
    private const float DeckFitRowHeight = 24f;
    private const float DeckFitGlyphGap = 8f;
    private const float DeckBottomPad = 24f;
    private const float DeckIndentEpsilon = 0.01f;
    private const float PreviewBottomPad = 40f;

    private static readonly TextStyle DeckNameStyle = TextStyles.Title2;
    private static readonly TextStyle DeckMetaStyle = TextStyles.Subheadline;
    private static readonly TextStyle DeckIntentStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle DeckStampStyle = TextStyles.Title3;
    private static readonly TextStyle DeckFitStyle = TextStyles.Subheadline;
    private static readonly Vector4 DeckBadgeFill = new(0.03f, 0.01f, 0.06f, 0.55f);
    private static readonly VelvetCardPhotoDto[] NoCardPhotos = Array.Empty<VelvetCardPhotoDto>();

    private readonly List<VelvetFitItem> fitItems = new();
    private readonly List<string> fitLabels = new();
    private string fitUserId = string.Empty;
    private VelvetProfileDto? fitMe;
    private LanguageInfo? fitLanguage;
    private string deckNameId = string.Empty;
    private string deckMetaLine = string.Empty;
    private string previewNameId = string.Empty;
    private string previewMetaLine = string.Empty;
    private string deckPhotoBadge = string.Empty;
    private int deckPhotoBadgeCount = -1;
    private string deckSeenLabel = string.Empty;
    private string stampPass = string.Empty;
    private string stampConnect = string.Empty;
    private LanguageInfo? stampLanguage;
    private bool deckScrollTopPending;
    private string viewerUrl = string.Empty;

    private static VelvetCardPhotoDto[] CardPhotos(VelvetProfileDto profile) => profile.Photos ?? NoCardPhotos;

    private string CardMetaLine(VelvetProfileDto profile)
    {
        var meta = SocialIdentity.ProfileMeta(profile.Handle, RegionCodeOf(profile));
        var race = profile.Race > 0 ? VelvetRace.Label(gameData, profile.Race) : string.Empty;
        return race.Length > 0 ? string.Concat(meta, FilterSummarySeparator, race) : meta;
    }

    private void OpenPhotoViewer(string url)
    {
        viewerUrl = url;
        photoViewer.Open(this, viewerSource);
    }

    private void OnDeckTopChanged()
    {
        fitUserId = string.Empty;
        deckPhotoBadgeCount = -1;
        deckScrollTopPending = true;
        if (deck.Count == 0)
        {
            return;
        }

        var top = deck[0];
        deckNameId = "velvet.deck.name." + top.UserId;
        deckMetaLine = CardMetaLine(top);
    }

    private void DrawDeckCard(VelvetProfileDto profile, Rect body, in AppSurface.SurfaceScope surface)
    {
        var scale = UiScale.Current;
        if (deckScrollTopPending)
        {
            surface.JumpToTop();
            deckScrollTopPending = false;
        }

        EnsureFit(profile);
        EnsureStamps();
        var photos = CardPhotos(profile);
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var inset = DeckCardInset * scale;
        var offsetX = cardSlide.Value;
        var origin = ImGui.GetCursorScreenPos();
        var innerWidth = MathF.Max(1f, width - inset * 2f);
        var left = origin.X + inset + offsetX;
        var coverHeight = MathF.Min(innerWidth * DeckCoverAspect, body.Height * DeckCoverMaxShare);
        var coverTop = origin.Y + DeckStackPeek * scale;
        var cover = new Rect(new Vector2(left, coverTop), new Vector2(left + innerWidth, coverTop + coverHeight));
        if (deck.Count > 1)
        {
            DrawDeckStack(drawList, cover, scale);
        }

        DrawCardCover(drawList, profile, photos, cover, scale, deckNameId, deckMetaLine);
        DrawDeckStamp(drawList, cover, innerWidth, scale);
        UiAnchors.Report("velvet.discover.card", cover);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cover.Max.Y - origin.Y));

        var indent = MathF.Abs(inset + offsetX) < DeckIndentEpsilon ? DeckIndentEpsilon : inset + offsetX;
        ImGui.Indent(indent);
        Gap(DeckColumnLead);
        DrawDeckFit(innerWidth);
        DrawCardColumn(profile, photos, innerWidth, ViewerAgainst(profile));
        ImGui.Unindent(indent);
        Gap(DeckActionBarHeight + DeckBottomPad);
    }

    private void DrawCardColumn(VelvetProfileDto profile, VelvetCardPhotoDto[] photos, float innerWidth,
        VelvetProfileDto? viewer)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawIntroCard(profile, innerWidth);
        var photoIndex = 1;
        DrawCardPhoto(drawList, photos, ref photoIndex, innerWidth);
        DrawFactsCard(profile, innerWidth);
        if (VelvetIntent.IncludesErp(profile.LookingFor))
        {
            DrawTokenCard(L.Velvet.CardKinks, PhoneIcons.Flame, KinkTone, profile.Kinks, innerWidth, viewer,
                VelvetTokenGroup.Kinks);
        }

        DrawCardPhoto(drawList, photos, ref photoIndex, innerWidth);
        DrawTokenCard(L.Velvet.CardTags, PhoneIcons.Hash, VelvetTheme.Rose, profile.Tags, innerWidth, viewer,
            VelvetTokenGroup.Tags);
        DrawTokenCard(L.Velvet.CardLimits, PhoneIcons.Shield, VelvetTheme.Gold, profile.Limits, innerWidth, viewer,
            VelvetTokenGroup.Limits);
        while (photoIndex < photos.Length)
        {
            DrawCardPhoto(drawList, photos, ref photoIndex, innerWidth);
        }
    }

    private void OpenCardPreview()
    {
        if (store.Me is not { } me)
        {
            return;
        }

        previewNameId = "velvet.preview.name." + me.UserId;
        previewMetaLine = CardMetaLine(me);
        router.Push(VelvetView.CardPreview);
    }

    private void DrawCardPreview(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.CardPreviewTitle)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        if (store.Me is not { } me)
        {
            DrawEmpty(body, Loc.T(L.Common.Loading), string.Empty);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            var photos = CardPhotos(me);
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var inset = DeckCardInset * scale;
            var origin = ImGui.GetCursorScreenPos();
            var innerWidth = MathF.Max(1f, width - inset * 2f);
            var coverHeight = MathF.Min(innerWidth * DeckCoverAspect, body.Height * DeckCoverMaxShare);
            var coverTop = origin.Y + DeckStackPeek * scale;
            var cover = new Rect(new Vector2(origin.X + inset, coverTop),
                new Vector2(origin.X + inset + innerWidth, coverTop + coverHeight));
            DrawCardCover(drawList, me, photos, cover, scale, previewNameId, previewMetaLine);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cover.Max.Y - origin.Y));
            ImGui.Indent(inset);
            Gap(DeckColumnLead);
            DrawCardColumn(me, photos, innerWidth, null);
            ImGui.Unindent(inset);
            Gap(PreviewBottomPad);
        }
    }

    private void EnsureFit(VelvetProfileDto profile)
    {
        var me = store.Me;
        if (fitUserId == profile.UserId && ReferenceEquals(fitMe, me) && ReferenceEquals(fitLanguage, Loc.Current))
        {
            return;
        }

        fitUserId = profile.UserId;
        fitMe = me;
        fitLanguage = Loc.Current;
        fitItems.Clear();
        fitLabels.Clear();
        if (me is null)
        {
            return;
        }

        VelvetFit.Describe(me, profile, fitItems);
        for (var index = 0; index < fitItems.Count; index++)
        {
            fitLabels.Add(FitLabel(fitItems[index]));
        }
    }

    private static string FitLabel(in VelvetFitItem item) =>
        item.Kind switch
        {
            VelvetFitKind.SharedKinks => Loc.Plural(L.Velvet.FitSharedKinks, item.Value),
            VelvetFitKind.SharedIntent => Loc.T(L.Velvet.FitBothHereFor, VelvetIntent.Label(item.Value)),
            VelvetFitKind.SharedTag => Loc.T(L.Velvet.FitBoth, VelvetTokenLabels.Of(item.Token)),
            VelvetFitKind.SharedLanguage => Loc.T(L.Velvet.FitBothSpeak, VelvetLanguages.Label(item.Value)),
            VelvetFitKind.Conflict => Loc.T(L.Velvet.FitConflict, VelvetTokenLabels.Of(item.Token)),
            VelvetFitKind.NoSharedLanguage => Loc.T(L.Velvet.FitNoSharedLanguage),
            _ => Loc.T(L.Velvet.FitNoConflicts),
        };

    private void EnsureStamps()
    {
        if (ReferenceEquals(stampLanguage, Loc.Current))
        {
            return;
        }

        stampLanguage = Loc.Current;
        stampPass = Loc.Upper(Loc.T(L.Velvet.DeckPass));
        stampConnect = Loc.Upper(Loc.T(L.Velvet.Connect));
        deckSeenLabel = Loc.T(L.Velvet.DeckSeenBefore);
    }

    private static void DrawDeckStack(ImDrawListPtr drawList, Rect cover, float scale)
    {
        var shrink = DeckStackShrink * scale;
        var peek = DeckStackPeek * scale;
        var radius = DeckCoverRadius * scale;
        Squircle.Fill(drawList, new Vector2(cover.Min.X + shrink, cover.Min.Y - peek),
            new Vector2(cover.Max.X - shrink, cover.Min.Y + radius * 2f), radius,
            VelvetTheme.Alpha(VelvetTheme.CardHi, 0.75f).Packed());
    }

    private void DrawCardCover(ImDrawListPtr drawList, VelvetProfileDto profile, VelvetCardPhotoDto[] photos,
        Rect cover, float scale, string nameId, string metaLine, bool interactive = true)
    {
        var name = DisplayNameOf(profile.DisplayName, profile.Handle);
        var radius = DeckCoverRadius * scale;
        var pad = DeckCoverPad * scale;
        var coverIsPhoto = photos.Length > 0;
        var coverUrl = coverIsPhoto ? photos[0].Url : profile.AvatarUrl ?? string.Empty;
        DrawCoverImage(drawList, cover.Min, cover.Max, coverUrl, radius, name);
        Squircle.FillVerticalGradient(drawList, new Vector2(cover.Min.X, cover.Max.Y - cover.Height * DeckScrimShare),
            cover.Max, radius, VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.94f).Packed());

        var badgeLeft = cover.Min.X + DeckCoverPad * scale;
        badgeLeft = DrawDeckSeenBadge(drawList, profile.UserId, cover, badgeLeft, scale);
        DrawDeckPhotoBadge(drawList, photos.Length, cover, badgeLeft, scale);
        DrawDeckPresence(drawList, profile.Presence, cover, pad, scale);

        var textLeft = cover.Min.X + pad;
        var textWidth = MathF.Max(1f, cover.Width - pad * 2f);
        var intent = VelvetIntent.Summary(profile.LookingFor);
        var intentHeight = Typography.LineHeight(DeckIntentStyle);
        var intentY = cover.Max.Y - pad - intentHeight;
        var metaHeight = Typography.LineHeight(DeckMetaStyle);
        var metaY = intentY - DeckCoverLineGap * scale - metaHeight;
        var nameHeight = Typography.LineHeight(DeckNameStyle);
        var nameY = metaY - DeckCoverLineGap * scale - nameHeight;

        var textBlock = new Rect(new Vector2(textLeft, nameY), new Vector2(textLeft + textWidth, cover.Max.Y - pad));
        var textHovered = interactive && UiInteract.Hover(textBlock.Min, textBlock.Max);
        UserName.Draw(drawList, nameId, name, profile.Badges, profile.BadgeIds, textLeft, nameY, textWidth,
            DeckNameStyle, VelvetTheme.TitleInk, textHovered, false);
        Typography.Draw(drawList, new Vector2(textLeft, metaY),
            Typography.FitText(metaLine, textWidth, DeckMetaStyle), VelvetTheme.BodyInk, DeckMetaStyle);
        Typography.Draw(drawList, new Vector2(textLeft, intentY),
            Typography.FitText(intent, textWidth, DeckIntentStyle), VelvetTheme.RoseInk, DeckIntentStyle);

        if (!interactive)
        {
            return;
        }

        if (UiInteract.Click(textBlock.Min, textBlock.Max, textHovered))
        {
            OpenProfile(profile.UserId);
            return;
        }

        var pictureHovered = !textHovered && UiInteract.Hover(cover.Min, cover.Max);
        if (!UiInteract.Click(cover.Min, cover.Max, pictureHovered))
        {
            return;
        }

        if (coverIsPhoto)
        {
            OpenPhotoViewer(coverUrl);
            return;
        }

        OpenProfile(profile.UserId);
    }

    private void DrawDeckPhotoBadge(ImDrawListPtr drawList, int count, Rect cover, float left, float scale)
    {
        if (count <= 1)
        {
            return;
        }

        if (deckPhotoBadgeCount != count)
        {
            deckPhotoBadgeCount = count;
            deckPhotoBadge = Loc.Plural(L.Velvet.PhotoBadge, count);
        }

        DrawDeckCoverBadge(drawList, deckPhotoBadge, PhoneIcons.Photo, VelvetTheme.RoseInk, cover, left, scale);
    }

    private float DrawDeckSeenBadge(ImDrawListPtr drawList, string userId, Rect cover, float left, float scale)
    {
        if (!SeenBefore(userId))
        {
            return left;
        }

        return DrawDeckCoverBadge(drawList, deckSeenLabel, PhoneIcons.ArrowBackUp, VelvetTheme.Moonlight, cover, left,
            scale);
    }

    private static float DrawDeckCoverBadge(ImDrawListPtr drawList, string label, string glyph, Vector4 glyphInk,
        Rect cover, float left, float scale)
    {
        var glyphSize = VIcon.Small * scale;
        var textSize = Typography.Measure(label, TextStyles.Footnote);
        var pad = DeckBadgePad * scale;
        var min = new Vector2(left, cover.Min.Y + DeckCoverPad * scale);
        var max = new Vector2(min.X + pad * 2f + glyphSize + DeckBadgeGlyphGap * scale + textSize.X,
            min.Y + DeckBadgeHeight * scale);
        Squircle.Fill(drawList, min, max, DeckBadgeHeight * scale * 0.5f, DeckBadgeFill.Packed());
        var glyphCenter = new Vector2(min.X + pad + glyphSize * 0.5f, (min.Y + max.Y) * 0.5f);
        PhoneIcon.Draw(drawList, glyphCenter, glyph, glyphInk, glyphSize);
        Typography.Draw(drawList, new Vector2(glyphCenter.X + glyphSize * 0.5f + DeckBadgeGlyphGap * scale,
            glyphCenter.Y - textSize.Y * 0.5f), label, VelvetTheme.OnAccent, TextStyles.Footnote);
        return max.X + DeckBadgeGap * scale;
    }

    private static void DrawDeckPresence(ImDrawListPtr drawList, int presence, Rect cover, float inset, float scale)
    {
        if (!VelvetTheme.PresenceActive(presence))
        {
            return;
        }

        var label = VelvetPresence.Label(presence);
        var textSize = Typography.Measure(label, TextStyles.Footnote);
        var pad = DeckBadgePad * scale;
        var dot = DeckPresenceDot * scale;
        var top = cover.Min.Y + inset;
        var max = new Vector2(cover.Max.X - inset, top + DeckBadgeHeight * scale);
        var min = new Vector2(max.X - pad * 2f - dot * 2f - DeckBadgeGlyphGap * scale - textSize.X, top);
        var centerY = (min.Y + max.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, DeckBadgeHeight * scale * 0.5f, DeckBadgeFill.Packed());
        drawList.AddCircleFilled(new Vector2(min.X + pad + dot, centerY), dot,
            VelvetTheme.PresenceColor(presence).Packed(), 16);
        Typography.Draw(drawList, new Vector2(min.X + pad + dot * 2f + DeckBadgeGlyphGap * scale,
            centerY - textSize.Y * 0.5f), label, VelvetTheme.OnAccent, TextStyles.Footnote);
    }

    private void DrawDeckStamp(ImDrawListPtr drawList, Rect cover, float innerWidth, float scale)
    {
        var slide = cardSlide.Value;
        var progress = MathF.Min(1f, MathF.Abs(slide) / MathF.Max(1f, innerWidth * DeckCommitFraction));
        if (progress < DeckStampMinProgress)
        {
            return;
        }

        var passing = slide < 0f;
        var label = passing ? stampPass : stampConnect;
        var tone = VelvetTheme.Alpha(passing ? VelvetTheme.Moonlight : VelvetTheme.RoseGlow, progress);
        var textSize = Typography.Measure(label, DeckStampStyle);
        var pad = DeckStampPad * scale;
        var inset = DeckCoverPad * scale;
        var top = cover.Min.Y + inset + DeckBadgeHeight * scale + pad;
        var min = passing
            ? new Vector2(cover.Max.X - inset - textSize.X - pad * 2f, top)
            : new Vector2(cover.Min.X + inset, top);
        var max = new Vector2(min.X + textSize.X + pad * 2f, top + textSize.Y + pad);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale,
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.45f * progress).Packed());
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Sm * scale, tone.Packed(), DeckStampStroke * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad * 0.5f), label, tone, DeckStampStyle);
    }

    private void DrawDeckFit(float innerWidth)
    {
        if (fitLabels.Count == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var glyphSize = VIcon.Chip * scale;
        var rowHeight = DeckFitRowHeight * scale;
        Gap(VCard.Gap);
        var card = VCard.Begin(drawList, innerWidth, VCard.HeaderBlock * scale + rowHeight * fitLabels.Count, scale);
        VCard.Header(drawList, card.ContentOrigin, card.ContentWidth, PhoneIcons.HeartHandshake, VelvetTheme.Rose,
            Loc.T(L.Velvet.FitTitle), scale);
        var textLeft = card.ContentOrigin.X + glyphSize + DeckFitGlyphGap * scale;
        var textWidth = MathF.Max(1f, card.ContentWidth - glyphSize - DeckFitGlyphGap * scale);
        var rowTop = card.ContentOrigin.Y + VCard.HeaderBlock * scale;
        for (var index = 0; index < fitLabels.Count; index++)
        {
            var conflict = VelvetFit.Warns(fitItems[index].Kind);
            var tone = conflict ? VelvetTheme.Danger : VelvetTheme.Online;
            var ink = conflict ? VelvetTheme.ToneInk(VelvetTheme.Danger) : VelvetTheme.BodyInk;
            var centerY = rowTop + rowHeight * 0.5f;
            PhoneIcon.Draw(drawList, new Vector2(card.ContentOrigin.X + glyphSize * 0.5f, centerY),
                conflict ? PhoneIcons.X : PhoneIcons.Check, tone, glyphSize);
            Typography.Draw(drawList, new Vector2(textLeft, centerY - Typography.LineHeight(DeckFitStyle) * 0.5f),
                Typography.FitText(fitLabels[index], textWidth, DeckFitStyle), ink, DeckFitStyle);
            rowTop += rowHeight;
        }

        VCard.End(card);
    }

    private void DrawCardPhoto(ImDrawListPtr drawList, VelvetCardPhotoDto[] photos, ref int photoIndex,
        float innerWidth)
    {
        if (photoIndex >= photos.Length)
        {
            return;
        }

        var photo = photos[photoIndex];
        photoIndex++;
        var scale = UiScale.Current;
        Gap(VCard.Gap);
        var height = PostAspects.TallDisplayHeight(innerWidth, photo.Width, photo.Height);
        var min = ImGui.GetCursorScreenPos();
        var max = new Vector2(min.X + innerWidth, min.Y + height);
        DrawMedia(drawList, min, max, photo.Url, DeckPhotoRadius * scale);
        if (UiInteract.Click(min, max))
        {
            OpenPhotoViewer(photo.Url);
        }

        ImGui.Dummy(new Vector2(innerWidth, height));
    }

    private void DrawCoverImage(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding,
        string fallbackName, float focusY = ImageFit.CenterFocus)
    {
        var texture = url.Length > 0 ? images.Get(url) : null;
        if (texture is null)
        {
            drawList.AddRectFilled(min, max, VelvetTheme.PlumWell.Packed(), rounding, ImDrawFlags.RoundCornersAll);
            Squircle.FillVerticalGradient(drawList, min, max, rounding,
                VelvetTheme.Alpha(VelvetTheme.CardHi, 0.6f).Packed(), VelvetTheme.Alpha(VelvetTheme.PlumWell, 0f).Packed());
            var monogram = fallbackName.Length > 0 ? fallbackName[..1].ToUpperInvariant() : "?";
            var monogramCenter = new Vector2((min.X + max.X) * 0.5f, min.Y + (max.Y - min.Y) * 0.40f);
            ProgressRing.Glow(monogramCenter, (max.X - min.X) * 0.22f, VelvetTheme.Alpha(VelvetTheme.Rose, 0.28f), 0.5f);
            Typography.DrawCentered(drawList, monogramCenter, monogram, VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.7f),
                TextStyles.LargeTitle);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y, focusY);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }
}

using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float CardInset = 12f;
    private const float CardCoverMaxShare = 0.62f;
    private const float CardCoverRadius = 18f;
    private const float CardCoverLead = 6f;
    private const float CardScrimShare = 0.55f;
    private const float CardCoverPad = 16f;
    private const float CardCoverLineGap = 4f;
    private const float CardBadgeHeight = 24f;
    private const float CardBadgePad = 10f;
    private const float CardBadgeGlyphGap = 6f;
    private const float CardPresenceDot = 4f;
    private const float CardColumnLead = 4f;
    private const float CardFitRowHeight = 24f;
    private const float CardFitGlyphGap = 8f;
    private const float PreviewBottomPad = 40f;

    private static readonly TextStyle CardNameStyle = TextStyles.Title2;
    private static readonly TextStyle CardMetaStyle = TextStyles.Subheadline;
    private static readonly TextStyle CardIntentStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CardFitStyle = TextStyles.Subheadline;
    private static readonly Vector4 CardBadgeFill = new(0.03f, 0.01f, 0.06f, 0.55f);
    private static readonly VelvetCardPhotoDto[] NoCardPhotos = Array.Empty<VelvetCardPhotoDto>();

    private readonly List<VelvetFitItem> fitItems = new();
    private readonly List<string> fitLabels = new();
    private string fitUserId = string.Empty;
    private VelvetProfileDto? fitMe;
    private LanguageInfo? fitLanguage;
    private string previewNameId = string.Empty;
    private string previewMetaLine = string.Empty;
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

    private void DrawCardColumn(VelvetProfileDto profile, float innerWidth, VelvetProfileDto? viewer)
    {
        DrawIntroCard(profile, innerWidth);
        DrawFactsCard(profile, innerWidth);
        if (VelvetIntent.IncludesErp(profile.LookingFor))
        {
            DrawTokenCard(L.Velvet.CardKinks, PhoneIcons.Flame, KinkTone, profile.Kinks, innerWidth, viewer,
                VelvetTokenGroup.Kinks);
        }

        DrawTokenCard(L.Velvet.CardTags, PhoneIcons.Hash, VelvetTheme.Rose, profile.Tags, innerWidth, viewer,
            VelvetTokenGroup.Tags);
        DrawTokenCard(L.Velvet.CardLimits, PhoneIcons.Shield, VelvetTheme.Gold, profile.Limits, innerWidth, viewer,
            VelvetTokenGroup.Limits);
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
            var inset = CardInset * scale;
            var origin = ImGui.GetCursorScreenPos();
            var innerWidth = MathF.Max(1f, width - inset * 2f);
            var coverHeight = MathF.Min(innerWidth * GridCardAspect, body.Height * CardCoverMaxShare);
            var coverTop = origin.Y + CardCoverLead * scale;
            var cover = new Rect(new Vector2(origin.X + inset, coverTop),
                new Vector2(origin.X + inset + innerWidth, coverTop + coverHeight));
            DrawCardCover(drawList, me, photos, cover, scale, previewNameId, previewMetaLine);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cover.Max.Y - origin.Y));
            ImGui.Indent(inset);
            Gap(CardColumnLead);
            DrawCardColumn(me, innerWidth, null);
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

    private void DrawCardFit(float innerWidth)
    {
        if (fitLabels.Count == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var glyphSize = VIcon.Chip * scale;
        var rowHeight = CardFitRowHeight * scale;
        Gap(VCard.Gap);
        var card = VCard.Begin(drawList, innerWidth, VCard.HeaderBlock * scale + rowHeight * fitLabels.Count, scale);
        VCard.Header(drawList, card.ContentOrigin, card.ContentWidth, PhoneIcons.HeartHandshake, VelvetTheme.Rose,
            Loc.T(L.Velvet.FitTitle), scale);
        var textLeft = card.ContentOrigin.X + glyphSize + CardFitGlyphGap * scale;
        var textWidth = MathF.Max(1f, card.ContentWidth - glyphSize - CardFitGlyphGap * scale);
        var rowTop = card.ContentOrigin.Y + VCard.HeaderBlock * scale;
        for (var index = 0; index < fitLabels.Count; index++)
        {
            var conflict = VelvetFit.Warns(fitItems[index].Kind);
            var tone = conflict ? VelvetTheme.Danger : VelvetTheme.Online;
            var ink = conflict ? VelvetTheme.ToneInk(VelvetTheme.Danger) : VelvetTheme.BodyInk;
            var centerY = rowTop + rowHeight * 0.5f;
            PhoneIcon.Draw(drawList, new Vector2(card.ContentOrigin.X + glyphSize * 0.5f, centerY),
                conflict ? PhoneIcons.X : PhoneIcons.Check, tone, glyphSize);
            Typography.Draw(drawList, new Vector2(textLeft, centerY - Typography.LineHeight(CardFitStyle) * 0.5f),
                Typography.FitText(fitLabels[index], textWidth, CardFitStyle), ink, CardFitStyle);
            rowTop += rowHeight;
        }

        VCard.End(card);
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

    private void DrawCardCover(ImDrawListPtr drawList, VelvetProfileDto profile, VelvetCardPhotoDto[] photos,
        Rect cover, float scale, string nameId, string metaLine, bool interactive = true)
    {
        var name = DisplayNameOf(profile.DisplayName, profile.Handle);
        var radius = CardCoverRadius * scale;
        var pad = CardCoverPad * scale;
        var coverIsPhoto = photos.Length > 0;
        var coverUrl = coverIsPhoto ? photos[0].Url : profile.AvatarUrl ?? string.Empty;
        DrawCoverImage(drawList, cover.Min, cover.Max, coverUrl, radius, name);
        Squircle.FillVerticalGradient(drawList, new Vector2(cover.Min.X, cover.Max.Y - cover.Height * CardScrimShare),
            cover.Max, radius, VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.94f).Packed());

        DrawCardPresence(drawList, profile.Presence, cover, pad, scale);

        var textLeft = cover.Min.X + pad;
        var textWidth = MathF.Max(1f, cover.Width - pad * 2f);
        var intent = VelvetIntent.Summary(profile.LookingFor);
        var intentHeight = Typography.LineHeight(CardIntentStyle);
        var intentY = cover.Max.Y - pad - intentHeight;
        var metaHeight = Typography.LineHeight(CardMetaStyle);
        var metaY = intentY - CardCoverLineGap * scale - metaHeight;
        var nameHeight = Typography.LineHeight(CardNameStyle);
        var nameY = metaY - CardCoverLineGap * scale - nameHeight;

        var textBlock = new Rect(new Vector2(textLeft, nameY), new Vector2(textLeft + textWidth, cover.Max.Y - pad));
        var textHovered = interactive && UiInteract.Hover(textBlock.Min, textBlock.Max);
        UserName.Draw(drawList, nameId, name, profile.Badges, profile.BadgeIds, textLeft, nameY, textWidth,
            CardNameStyle, VelvetTheme.TitleInk, textHovered, false);
        Typography.Draw(drawList, new Vector2(textLeft, metaY),
            Typography.FitText(metaLine, textWidth, CardMetaStyle), VelvetTheme.BodyInk, CardMetaStyle);
        Typography.Draw(drawList, new Vector2(textLeft, intentY),
            Typography.FitText(intent, textWidth, CardIntentStyle), VelvetTheme.RoseInk, CardIntentStyle);

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

    private static void DrawCardCoverBadge(ImDrawListPtr drawList, string label, string glyph, Vector4 glyphInk,
        Rect cover, float left, float scale)
    {
        var glyphSize = VIcon.Small * scale;
        var textSize = Typography.Measure(label, TextStyles.Footnote);
        var pad = CardBadgePad * scale;
        var min = new Vector2(left, cover.Min.Y + CardCoverPad * scale);
        var max = new Vector2(min.X + pad * 2f + glyphSize + CardBadgeGlyphGap * scale + textSize.X,
            min.Y + CardBadgeHeight * scale);
        Squircle.Fill(drawList, min, max, CardBadgeHeight * scale * 0.5f, CardBadgeFill.Packed());
        var glyphCenter = new Vector2(min.X + pad + glyphSize * 0.5f, (min.Y + max.Y) * 0.5f);
        PhoneIcon.Draw(drawList, glyphCenter, glyph, glyphInk, glyphSize);
        Typography.Draw(drawList, new Vector2(glyphCenter.X + glyphSize * 0.5f + CardBadgeGlyphGap * scale,
            glyphCenter.Y - textSize.Y * 0.5f), label, VelvetTheme.OnAccent, TextStyles.Footnote);
    }

    private static void DrawCardPresence(ImDrawListPtr drawList, int presence, Rect cover, float inset, float scale)
    {
        if (!VelvetTheme.PresenceActive(presence))
        {
            return;
        }

        var label = VelvetPresence.Label(presence);
        var textSize = Typography.Measure(label, TextStyles.Footnote);
        var pad = CardBadgePad * scale;
        var dot = CardPresenceDot * scale;
        var top = cover.Min.Y + inset;
        var max = new Vector2(cover.Max.X - inset, top + CardBadgeHeight * scale);
        var min = new Vector2(max.X - pad * 2f - dot * 2f - CardBadgeGlyphGap * scale - textSize.X, top);
        var centerY = (min.Y + max.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, CardBadgeHeight * scale * 0.5f, CardBadgeFill.Packed());
        drawList.AddCircleFilled(new Vector2(min.X + pad + dot, centerY), dot,
            VelvetTheme.PresenceColor(presence).Packed(), 16);
        Typography.Draw(drawList, new Vector2(min.X + pad + dot * 2f + CardBadgeGlyphGap * scale,
            centerY - textSize.Y * 0.5f), label, VelvetTheme.OnAccent, TextStyles.Footnote);
    }

    private void DrawCoverImage(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding,
        string fallbackName, float focusY = ImageFit.CenterFocus)
    {
        var texture = url.Length > 0 ? images.Get(url) : null;
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, VelvetTheme.PlumWell.Packed());
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
        Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int GuideCopyLines = 3;
    private const float GuidePlateDelay = 0.04f;
    private const float GuideCopyDelay = 0.12f;
    private const float GuideFillDelay = 0.45f;
    private const float PlateRowShare = 0.18f;
    private const float PlatePitchShare = 1.2f;
    private const int PlateTiles = 4;
    private const float PlateHeatShare = 0.36f;
    private const float PlateLinkShare = 0.1f;
    private const float PlateSecondShare = 0.82f;
    private const float PlateThirdShare = 0.7f;
    private const float PlateNumeralShare = 0.4f;
    private const int GuideChosenScore = 4;
    private const int PodiumPlaces = 3;
    private const float PlatePhotoShare = 0.52f;

    private static readonly LocString[] GuideTitles =
    {
        L.Prism.GuideIntroTitle, L.Prism.GuideTryTitle, L.Prism.GuidePostTitle, L.Prism.GuideWardrobeTitle,
        L.Prism.GuideEnterTitle, L.Prism.JudgeHeat, L.Prism.FinalTitle, L.Prism.GuideRevealTitle,
    };

    private static readonly LocString[] GuideBodies =
    {
        L.Prism.GuideIntroBody, L.Prism.GuideTryBody, L.Prism.GuidePostBody, L.Prism.GuideWardrobeBody,
        L.Prism.GuideEnterBody, L.Prism.GuideJudgeBody, L.Prism.GuideFinalBody, L.Prism.GuideRevealBody,
    };

    private static readonly int[] PodiumSlots = { 2, 1, 3 };

    private static readonly string PiecesGlyph = IconGlyph.Of(FontAwesomeIcon.LayerGroup);

    private readonly CachedText[] prizeTexts = new CachedText[PodiumPlaces];
    private CachedText guidePiecesText;
    private int guidePage;
    private PrismTab guideReturn = PrismTab.Discover;

    private void StartGuide(PrismTab returnTab)
    {
        guidePage = 0;
        guideReturn = returnTab;
        if (glams.IsSignedIn)
        {
            RefreshDiscover(false);
        }

        Go(PrismScreen.Guide);
    }

    private void TurnGuidePage(int delta)
    {
        var next = guidePage + delta;
        if (next < 0 || next >= GuideTitles.Length)
        {
            FinishGuide();
            return;
        }

        guidePage = next;
        Go(PrismScreen.Guide, delta);
    }

    private void FinishGuide()
    {
        OpenTab(guideReturn);
    }

    private void DrawGuide(ImDrawListPtr drawList, Rect content, float scale)
    {
        var left = content.Min.X;
        var right = content.Max.X;
        var width = right - left;
        var last = guidePage == GuideTitles.Length - 1;
        PrismArt.StoryBars(drawList, left, right, content.Min.Y + StoryBarGapUnits * scale, GuideTitles.Length,
            guidePage, 1f, scale);
        var tap = Metrics.Size.TapTarget * scale;
        var rowTop = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale - Metrics.Space.Xs * scale;
        var back = new Rect(new Vector2(left, rowTop), new Vector2(left + tap, rowTop + tap));
        HoverTooltip.Show("prism.guide.back", back, Loc.T(L.Prism.Back));
        if (BackButton.Draw("prism.guide.back", back.Center, BackChevronUnits * scale, PrismArt.Fore,
                UiInteract.Hover(back.Min, back.Max), scale))
        {
            TurnGuidePage(-1);
            return;
        }

        var skip = Loc.T(L.Prism.Skip);
        if (!last && DrawButton(drawList, PlainRect(new Vector2(right, back.Center.Y), skip, width * 0.5f, 1f, scale),
                skip, ButtonStyle.Plain, scale))
        {
            FinishGuide();
            return;
        }

        var next = ActionRect(content, scale);
        if (DrawButton(drawList, next, Loc.T(last ? L.Prism.GetStarted : L.Common.Next), ButtonStyle.Solid, scale,
                id: "prism.guide.next"))
        {
            TurnGuidePage(1);
            return;
        }

        var title = Loc.T(GuideTitles[guidePage]);
        var copy = PrismType.Sentences(Loc.T(GuideBodies[guidePage]), PrismType.Body, width);
        var copyWidth = PrismType.BalancedWidth(copy, PrismType.Body, width);
        var copyHeight = MathF.Max(Typography.MeasureWrappedBlock(copy, PrismType.Body, copyWidth).Y,
            Typography.LineHeight(PrismType.Body) * GuideCopyLines);
        var titleHeight = PrismType.HeroHeight(title, width);
        var textTop = next.Min.Y - Metrics.Space.Xxl * scale - copyHeight - Metrics.Space.Sm * scale - titleHeight;
        var plate = new Rect(new Vector2(left, back.Max.Y + Metrics.Space.Lg * scale),
            new Vector2(right, textTop - Metrics.Space.Xl * scale));
        if (plate.Height > 0f)
        {
            var mark = PrismMotion.Mark(drawList);
            DrawGuidePlate(drawList, plate, scale);
            PrismMotion.Center(drawList, mark, plate);
            PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, GuidePlateDelay, Motion.PageSettle),
                PrismMotion.RiseUnits * scale);
        }

        var text = PrismMotion.Mark(drawList);
        var top = textTop + PrismType.DrawHero(drawList, new Vector2(left, textTop), title, width, PrismArt.Fore) +
                  Metrics.Space.Sm * scale;
        Typography.DrawWrappedLeft(new Vector2(left, top), copy, PrismArt.Muted, PrismType.Body, copyWidth);
        PrismMotion.Rise(drawList, text, PrismMotion.At(screenAge, GuideCopyDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
    }

    private void DrawGuidePlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        switch (guidePage)
        {
            case 0:
                DrawCoverPlate(drawList, plate, scale);
                break;
            case 1:
                DrawTryPlate(drawList, plate, scale);
                break;
            case 2:
                DrawPostPlate(drawList, plate, scale);
                break;
            case 3:
                DrawWardrobePlate(drawList, plate, scale);
                break;
            case 4:
                DrawEnterPlate(drawList, plate, scale);
                break;
            case 5:
                DrawJudgePlate(drawList, plate, scale);
                break;
            case 6:
                DrawFinalPlate(drawList, plate, scale);
                break;
            default:
                DrawRevealPlate(drawList, plate, scale);
                break;
        }
    }

    private static Rect Portrait(float centerX, float top, float height)
    {
        var width = height * PrismStore.EntryRatio;
        return new Rect(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));
    }

    private GlamDto? GuideGlam(int index)
    {
        var today = glams.Lane(GlamScope.Today).Items;
        if (index < today.Length)
        {
            return today[index];
        }

        var feed = glams.Lane(discoverScope).Items;
        var next = index - today.Length;
        return next < feed.Length ? feed[next] : null;
    }

    private void DrawGuideLook(ImDrawListPtr drawList, Rect frame, int index, float alpha, float scale)
    {
        if (!PrismStock.Draw(drawList, frame, index, alpha))
        {
            PrismArt.Placeholder(drawList, frame, false, scale, alpha);
        }
    }

    private void DrawCoverPlate(ImDrawListPtr drawList, Rect plate, float scale) =>
        DrawGuideLook(drawList, Portrait(plate.Center.X, plate.Min.Y, plate.Height), 0, 1f, scale);

    private void DrawTryPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var gap = Metrics.Space.Sm * scale;
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height - tap - gap);
        DrawGuideLook(drawList, frame, 1, 1f, scale);
        var pieces = GuidePieces(GuideGlam(1));
        var pad = Metrics.Space.Md * scale;
        if (pieces.Length > 0)
        {
            PrismArt.PhotoPill(drawList, new Vector2(frame.Min.X + pad, frame.Max.Y - pad), frame.Width - pad * 2f,
                PiecesGlyph, pieces, false, scale);
        }

        PrismArt.TryOnMoment(drawList, frame, PhoneIcons.Wand, Loc.T(L.Prism.OnYourCharacter),
            screenAge - GuideFillDelay, scale);
        var middle = frame.Max.Y + gap + tap * 0.5f;
        var glyph = PrismArt.IconUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(frame.Min.X + glyph * 0.5f, middle), PhoneIcons.Heart, PrismArt.Fore,
            glyph);
        PhoneIcon.Draw(drawList, new Vector2(frame.Min.X + glyph * 2f, middle), PhoneIcons.Bookmark, PrismArt.Fore,
            glyph);
        var label = Loc.T(L.Prism.TryOnGlam);
        var pill = new Rect(
            new Vector2(frame.Max.X - ButtonWidth([label], frame.Width * 0.5f, scale), middle - tap * 0.5f),
            new Vector2(frame.Max.X, middle + tap * 0.5f));
        DrawPillLabel(drawList, PillFace(drawList, pill, true, false, ImGui.GetID("prism.guide.tryOn"), 1f), label,
            false, pill, 1f, scale);
    }

    private string GuidePieces(GlamDto? glam)
    {
        var count = 0;
        var gear = glam?.Gear ?? Array.Empty<GlamGearDto>();
        for (var index = 0; index < gear.Length; index++)
        {
            count += gear[index].ItemId != 0 ? 1 : 0;
        }

        if (count == 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(count, Loc.Current.Code);
        return guidePiecesText.IsCurrent(key)
            ? guidePiecesText.Value
            : guidePiecesText.Store(key, Loc.Plural(L.Prism.PieceCount, count));
    }

    private void DrawPostPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var photoHeight = plate.Height * PlatePhotoShare;
        var photoWidth = MathF.Min(photoHeight * PrismStore.EntryRatio,
            (plate.Width - gap * (GlamStore.MaxPhotos - 1)) / GlamStore.MaxPhotos);
        photoHeight = photoWidth / PrismStore.EntryRatio;
        var rowWidth = photoWidth * GlamStore.MaxPhotos + gap * (GlamStore.MaxPhotos - 1);
        var left = plate.Center.X - rowWidth * 0.5f;
        var shown = PrismMotion.At(screenAge, GuideFillDelay, Motion.Sheet) * GlamStore.MaxPhotos;
        for (var index = 0; index < GlamStore.MaxPhotos; index++)
        {
            var min = new Vector2(left + index * (photoWidth + gap), plate.Min.Y);
            DrawGuideLook(drawList, new Rect(min, min + new Vector2(photoWidth, photoHeight)), index,
                Math.Clamp(shown - index, 0f, 1f), scale);
        }

        var tilesTop = plate.Min.Y + photoHeight + gap * 2f;
        var size = MathF.Min(plate.Max.Y - tilesTop, (rowWidth - gap * (BodySlots.Length - 1)) / BodySlots.Length);
        var gear = GuideGlam(0)?.Gear ?? Array.Empty<GlamGearDto>();
        var first = plate.Center.X - (size * BodySlots.Length + gap * (BodySlots.Length - 1)) * 0.5f + size * 0.5f;
        for (var index = 0; index < BodySlots.Length; index++)
        {
            DrawBoardSlot(drawList, new Vector2(first + index * (size + gap), tilesTop + size * 0.5f), size,
                BodySlots[index], gear, true, scale);
        }
    }

    private void DrawWardrobePlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var tileHeight = (plate.Height - gap) * 0.5f;
        var tileWidth = MathF.Min(tileHeight * PrismStore.EntryRatio, (plate.Width - gap) * 0.5f);
        tileHeight = tileWidth / PrismStore.EntryRatio;
        var left = plate.Center.X - tileWidth - gap * 0.5f;
        for (var index = 0; index < PlateTiles; index++)
        {
            var min = new Vector2(left + index % 2 * (tileWidth + gap), plate.Min.Y + index / 2 * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (index is 0 or PlateTiles - 1)
            {
                DrawGuideLook(drawList, tile, index, 1f, scale);
                continue;
            }

            PrismArt.Placeholder(drawList, tile, false, scale);
            DrawOutfitBoard(drawList, tile, GuideGlam(index)?.Gear ?? Array.Empty<GlamGearDto>(), scale);
        }
    }

    private void DrawEnterPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height);
        DrawGuideLook(drawList, frame, 2, 1f, scale);
        if (store.Week is not { Title.Length: > 0 } week)
        {
            return;
        }

        var pad = Metrics.Space.Md * scale;
        var caption = Typography.LineHeight(PrismType.Caption);
        var headline = Typography.LineHeight(PrismType.Headline);
        PrismArt.Scrim(drawList, frame, caption + headline, scale);
        var room = frame.Width - pad * 2f;
        var top = frame.Max.Y - pad - caption - headline;
        Typography.Draw(drawList, new Vector2(frame.Min.X + pad, top),
            Typography.FitText(Loc.T(L.Prism.ThemeLabel), room, PrismType.Caption), PrismArt.PhotoMuted,
            PrismType.Caption);
        Typography.Draw(drawList, new Vector2(frame.Min.X + pad, top + caption),
            Typography.FitText(week.Title, room, PrismType.Headline), PrismArt.PhotoFore, PrismType.Headline);
    }

    private void DrawJudgePlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var gap = Metrics.Space.Md * scale;
        var rowHeight = plate.Height * PlateRowShare;
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height - rowHeight - labelLine - gap * 2f);
        DrawGuideLook(drawList, frame, 3, 1f, scale);
        var size = MathF.Min(rowHeight, plate.Width / (PrismArt.Stars * PlatePitchShare));
        var rowWidth = size * PrismArt.Stars * PlatePitchShare;
        var row = new Rect(new Vector2(plate.Center.X - rowWidth * 0.5f, frame.Max.Y + gap),
            new Vector2(plate.Center.X + rowWidth * 0.5f, frame.Max.Y + gap + size));
        DrawScoreButtons(drawList, row, GuideChosenScore, PrismMotion.At(screenAge, GuideFillDelay, Motion.Release),
            false, scale);

        var anchors = row.Max.Y + gap;
        PrismType.Draw(drawList, new Vector2(row.Min.X, anchors), Loc.T(L.Prism.ScoreLow), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(row.Max.X, anchors), Loc.T(L.Prism.ScoreHigh),
            PrismArt.Muted, PrismFace.Label, labelPixels);
    }

    private void DrawFinalPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var minGap = Metrics.Space.Sm * scale;
        var heatWidth = MathF.Min(plate.Height * PlateHeatShare * PrismStore.EntryRatio,
            (plate.Width - minGap * (PodiumPlaces + 1)) / PodiumPlaces);
        var heatHeight = heatWidth / PrismStore.EntryRatio;
        var gap = (plate.Width - heatWidth * PodiumPlaces) / (PodiumPlaces + 1);
        for (var index = 0; index < PodiumPlaces; index++)
        {
            var x = plate.Min.X + gap + index * (heatWidth + gap);
            DrawGuideLook(drawList,
                new Rect(new Vector2(x, plate.Min.Y), new Vector2(x + heatWidth, plate.Min.Y + heatHeight)), index,
                1f, scale);
        }

        var finalTop = plate.Min.Y + heatHeight + plate.Height * PlateLinkShare;
        var final = Portrait(plate.Center.X, finalTop, plate.Max.Y - finalTop);
        DrawGuideLook(drawList, final, PodiumPlaces, 1f, scale);
        var pad = Metrics.Space.Sm * scale;
        PrismArt.PhotoPill(drawList, new Vector2(final.Min.X + pad, final.Max.Y - pad), final.Width - pad * 2f,
            PhoneIcons.Crown, Loc.T(L.Prism.FinalTitle), false, scale);
    }

    private void DrawRevealPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var gap = Metrics.Space.Sm * scale;
        var tallest = plate.Height - labelLine - gap;
        var ratio = PrismStore.EntryRatio;
        var framesWidth = tallest * ratio * (1f + PlateSecondShare + PlateThirdShare);
        var room = plate.Width - gap * 2f;
        if (framesWidth > room)
        {
            tallest *= room / framesWidth;
            framesWidth = room;
        }

        var bottom = plate.Min.Y + tallest;
        var x = plate.Center.X - (framesWidth + gap * 2f) * 0.5f;
        var prizes = store.Week?.Prizes?.Final;
        for (var index = 0; index < PodiumSlots.Length; index++)
        {
            var place = PodiumSlots[index];
            var height = tallest * (place == 1 ? 1f : place == 2 ? PlateSecondShare : PlateThirdShare);
            var width = height * ratio;
            var frame = new Rect(new Vector2(x, bottom - height), new Vector2(x + width, bottom));
            DrawGuideLook(drawList, frame, place - 1, 1f, scale);
            var numeralPixels = width * PlateNumeralShare;
            var numeralHeight = PrismType.Snap(PrismFace.Numerals, numeralPixels);
            PrismArt.Scrim(drawList, frame, numeralHeight, scale);
            PrismType.Draw(drawList, new Vector2(frame.Min.X + gap, frame.Max.Y - numeralHeight), Plain[place],
                PrismArt.PhotoFore, PrismFace.Numerals, numeralPixels);
            var prize = PrizeLine(prizes, place - 1);
            if (prize.Length > 0)
            {
                var glyph = labelLine * CurrencyGlyph.GlyphFraction;
                CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
                    new Vector2(frame.Min.X + glyph * 0.5f, bottom + gap + labelLine * 0.5f), glyph);
                PrismType.Draw(drawList, new Vector2(frame.Min.X + CurrencyGlyph.Reserve(labelLine), bottom + gap),
                    prize, PrismArt.Fore, PrismFace.Label, labelPixels);
            }

            x += width + gap;
        }
    }

    private string PrizeLine(long[]? prizes, int index)
    {
        if (prizes is null || index >= prizes.Length || prizes[index] <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(prizes[index], Loc.Current.Code);
        return prizeTexts[index].IsCurrent(key)
            ? prizeTexts[index].Value
            : prizeTexts[index].Store(key, string.Concat("+", prizes[index].ToString("N0", Loc.Culture)));
    }
}

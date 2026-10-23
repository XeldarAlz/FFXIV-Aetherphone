using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int GuideCopyLines = 3;
    private const float GuidePlateDelay = 0.04f;
    private const float GuideCopyDelay = 0.12f;
    private const float GuideFillDelay = 0.45f;
    private const float PlateStripShare = 0.14f;
    private const float PlateRowShare = 0.18f;
    private const float PlatePitchShare = 1.2f;
    private const int PlateTiles = 4;
    private const int PlatePieces = 4;
    private const float PlateHeatShare = 0.36f;
    private const float PlateLinkShare = 0.1f;
    private const float PlateSecondShare = 0.82f;
    private const float PlateThirdShare = 0.7f;
    private const float PlateNumeralShare = 0.4f;
    private const float PlateBarShare = 0.06f;
    private const float PlateBarFaint = 0.6f;
    private const int GuideChosenScore = 4;
    private const int PodiumPlaces = 3;
    private const int PlateRows = 3;
    private const float PlateIconShare = 0.62f;
    private const float PlateBarShortShare = 0.55f;
    private const float PlatePhotoShare = 0.52f;
    private const float PlateTileIconShare = 0.28f;
    private const float PlateTilePadShare = 0.08f;
    private const float PlateStripBars = 1.6f;
    private const float PlateBylineShare = 0.45f;
    private const float PlateTitleGapBars = 1.4f;
    private const float PlateTitleShare = 0.8f;

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

    private static readonly string[] HeatLetters = { "A", "B", "C" };
    private static readonly int[] PodiumSlots = { 2, 1, 3 };

    private readonly CachedText[] prizeTexts = new CachedText[PodiumPlaces];
    private int guidePage;
    private PrismTab guideReturn = PrismTab.Discover;

    private void StartGuide(PrismTab returnTab)
    {
        guidePage = 0;
        guideReturn = returnTab;
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

    private void DrawCoverPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height);
        var today = glams.Lane(GlamScope.Today).Items;
        var photo = today.Length > 0 ? Cover(today[0]) : string.Empty;
        var pad = Metrics.Space.Md * scale;
        var bar = frame.Height * PlateBarShare;
        var strip = new Rect(new Vector2(frame.Min.X + pad, frame.Max.Y - pad - bar * PlateStripBars),
            new Vector2(frame.Max.X - pad, frame.Max.Y - pad));
        var byline = strip.Min.Y - pad - bar * 0.5f;
        var title = byline - bar * PlateTitleGapBars;
        var ink = PrismArt.Fore;
        if (photo.Length > 0)
        {
            DrawLook(drawList, frame, photo, false, focusY: CoverFocus);
            PrismArt.Scrim(drawList, frame, frame.Max.Y - title, scale);
            ink = PrismArt.Fore;
        }
        else
        {
            PrismArt.Placeholder(drawList, frame, false, scale);
        }

        drawList.AddRectFilled(strip.Min, strip.Max, ImGui.GetColorU32(ink), strip.Height * 0.5f);
        drawList.AddRectFilled(new Vector2(strip.Min.X, byline),
            new Vector2(strip.Min.X + strip.Width * PlateBylineShare, byline + bar * 0.5f),
            ImGui.GetColorU32(Palette.WithAlpha(ink, PlateBarFaint)));
        drawList.AddRectFilled(new Vector2(strip.Min.X, title),
            new Vector2(strip.Min.X + strip.Width * PlateTitleShare, title + bar), ImGui.GetColorU32(ink));
    }

    private void DrawEnterPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var stripHeight = plate.Height * PlateStripShare;
        var gap = Metrics.Space.Md * scale;
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height - stripHeight - gap);
        var fill = PrismMotion.At(screenAge, GuideFillDelay, Motion.Sheet);
        PrismArt.Placeholder(drawList, frame, false, scale);
        drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Back, 1f - fill)));
        drawList.AddRect(frame.Min, frame.Max, ImGui.GetColorU32(PrismArt.Faint), 0f,
            ImDrawFlags.None, PrismArt.HairlineWidth(scale));
        var plus = frame.Width * PlateNumeralShare;
        var size = PrismType.Measure("+", PrismFace.Headline, plus);
        PrismType.Draw(drawList, frame.Center - size * 0.5f, "+",
            Palette.WithAlpha(PrismArt.Muted, PrismArt.Muted.W * (1f - fill)), PrismFace.Headline, plus);
        DrawPlateStrip(drawList, new Rect(new Vector2(frame.Min.X, frame.Max.Y + gap), new Vector2(frame.Max.X, plate.Max.Y)),
            Loc.T(L.Prism.Submit));
    }

    private static void DrawPlateStrip(ImDrawListPtr drawList, Rect strip, string label)
    {
        drawList.AddRectFilled(strip.Min, strip.Max, ImGui.GetColorU32(PrismArt.Fore), strip.Height * 0.5f);
        var labelPixels = PrismArt.LabelPixels();
        var fitted = PrismType.FitLabel(label, labelPixels, MathF.Max(1f, strip.Width - strip.Height));
        PrismType.Draw(drawList, strip.Center - PrismType.Measure(fitted, PrismFace.Label, labelPixels) * 0.5f, fitted,
            PrismArt.OnFore, PrismFace.Label, labelPixels);
    }

    private static void DrawPlateRow(ImDrawListPtr drawList, float left, float top, float width, float height, float bar)
    {
        var icon = height * PlateIconShare;
        var iconTop = top + (height - icon) * 0.5f;
        drawList.AddRect(new Vector2(left, iconTop), new Vector2(left + icon, iconTop + icon),
            ImGui.GetColorU32(PrismArt.Faint), 0f, ImDrawFlags.None, PrismArt.HairlineWidth(UiScale.Current));
        var barLeft = left + icon + height * PlateIconShare * 0.4f;
        var barHeight = height * PlateBarShare * 2f;
        var barTop = top + height * 0.5f - barHeight;
        drawList.AddRectFilled(new Vector2(barLeft, barTop), new Vector2(barLeft + (left + width - barLeft) * bar,
            barTop + barHeight), ImGui.GetColorU32(PrismArt.Fore));
        drawList.AddRectFilled(new Vector2(barLeft, barTop + barHeight * 1.6f),
            new Vector2(barLeft + (left + width - barLeft) * bar * PlateBarShortShare, barTop + barHeight * 2.4f),
            ImGui.GetColorU32(PrismArt.Faint));
    }

    private void DrawTryPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var gap = Metrics.Space.Md * scale;
        var stripHeight = plate.Height * PlateStripShare;
        var height = plate.Height - stripHeight - gap;
        var frameWidth = MathF.Min(height * PrismStore.EntryRatio, (plate.Width - gap) * 0.5f);
        var groupLeft = plate.Center.X - frameWidth - gap * 0.5f;
        var frame = new Rect(new Vector2(groupLeft, plate.Min.Y),
            new Vector2(groupLeft + frameWidth, plate.Min.Y + frameWidth / PrismStore.EntryRatio));
        var worn = PrismMotion.At(screenAge, GuideFillDelay, Motion.Sheet);
        PrismArt.Placeholder(drawList, frame, false, scale);
        drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Back, 1f - worn)));
        drawList.AddRect(frame.Min, frame.Max, ImGui.GetColorU32(PrismArt.Faint), 0f,
            ImDrawFlags.None, PrismArt.HairlineWidth(scale));
        var rowsLeft = frame.Max.X + gap;
        var rowHeight = frame.Height / (PlateRows + 1);
        for (var index = 0; index < PlateRows; index++)
        {
            DrawPlateRow(drawList, rowsLeft, frame.Min.Y + rowHeight * (index + 0.5f), frameWidth, rowHeight,
                1f - index * 0.18f);
        }

        DrawPlateStrip(drawList, new Rect(new Vector2(frame.Min.X, frame.Max.Y + gap),
            new Vector2(rowsLeft + frameWidth, frame.Max.Y + gap + stripHeight)), Loc.T(L.Prism.TryOnGlam));
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
            var frame = new Rect(min, min + new Vector2(photoWidth, photoHeight));
            PrismArt.Placeholder(drawList, frame, false, scale);
            var hidden = 1f - Math.Clamp(shown - index, 0f, 1f);
            drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Back, hidden)));
            drawList.AddRect(frame.Min, frame.Max, ImGui.GetColorU32(PrismArt.Faint), 0f,
                ImDrawFlags.None, PrismArt.HairlineWidth(scale));
        }

        var labelPixels = PrismArt.LabelPixels();
        var rowHeight = (plate.Max.Y - plate.Min.Y - photoHeight - gap * 2f) / PlateRows;
        var chevron = labelPixels;
        for (var index = 0; index < PlateRows; index++)
        {
            var top = plate.Min.Y + photoHeight + gap * 2f + rowHeight * index;
            DrawPlateRow(drawList, left, top, rowWidth - chevron - gap, rowHeight, 0.8f - index * 0.15f);
            PhoneIcon.Draw(drawList, new Vector2(left + rowWidth - chevron * 0.5f, top + rowHeight * 0.5f),
                PhoneIcons.ChevronRight, PrismArt.Muted, chevron);
        }
    }

    private static void DrawWardrobePlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var tileHeight = (plate.Height - gap) * 0.5f;
        var tileWidth = MathF.Min(tileHeight * PrismStore.EntryRatio, (plate.Width - gap) * 0.5f);
        tileHeight = tileWidth / PrismStore.EntryRatio;
        var left = plate.Center.X - tileWidth - gap * 0.5f;
        var icon = tileWidth * PlateTileIconShare;
        var pad = tileWidth * PlateTilePadShare;
        for (var index = 0; index < PlateTiles; index++)
        {
            var min = new Vector2(left + index % 2 * (tileWidth + gap), plate.Min.Y + index / 2 * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            PrismArt.Placeholder(drawList, tile, false, scale);
            for (var piece = 0; piece < PlatePieces; piece++)
            {
                var iconMin = new Vector2(min.X + pad + piece % 2 * (icon + pad * 0.5f),
                    min.Y + pad + piece / 2 * (icon + pad * 0.5f));
                drawList.AddRect(iconMin, iconMin + new Vector2(icon, icon),
                    ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, PlateBarFaint)), 0f,
                        ImDrawFlags.None, PrismArt.HairlineWidth(scale));
            }

            var barHeight = tileHeight * PlateBarShare * 0.6f;
            drawList.AddRectFilled(new Vector2(min.X + pad, tile.Max.Y - pad - barHeight),
                new Vector2(min.X + pad + (tileWidth - pad * 2f) * (0.7f - index * 0.1f), tile.Max.Y - pad),
                ImGui.GetColorU32(PrismArt.Fore));
        }
    }

    private void DrawJudgePlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var gap = Metrics.Space.Md * scale;
        var rowHeight = plate.Height * PlateRowShare;
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height - rowHeight - labelLine - gap * 2f);
        PrismArt.Placeholder(drawList, frame, false, scale);
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

    private static void DrawFinalPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var minGap = Metrics.Space.Sm * scale;
        var heatWidth = MathF.Min(plate.Height * PlateHeatShare * PrismStore.EntryRatio,
            (plate.Width - minGap * (HeatLetters.Length + 1)) / HeatLetters.Length);
        var heatHeight = heatWidth / PrismStore.EntryRatio;
        var gap = (plate.Width - heatWidth * HeatLetters.Length) / (HeatLetters.Length + 1);
        var finalTop = plate.Min.Y + heatHeight + labelLine + plate.Height * PlateLinkShare;
        var final = Portrait(plate.Center.X, finalTop, plate.Max.Y - finalTop);
        var link = ImGui.GetColorU32(PrismArt.Faint);
        for (var index = 0; index < HeatLetters.Length; index++)
        {
            var x = plate.Min.X + gap + index * (heatWidth + gap);
            var heat = new Rect(new Vector2(x, plate.Min.Y), new Vector2(x + heatWidth, plate.Min.Y + heatHeight));
            PrismArt.Placeholder(drawList, heat, false, scale);
            var letter = HeatLetters[index];
            var size = PrismType.Measure(letter, PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(heat.Center.X - size.X * 0.5f, heat.Max.Y), letter, PrismArt.Muted,
                PrismFace.Label, labelPixels);
            drawList.AddLine(new Vector2(heat.Center.X, heat.Max.Y + labelLine),
                new Vector2(final.Center.X, final.Min.Y), link, PrismArt.HairlineWidth(scale));
        }

        PrismArt.Placeholder(drawList, final, false, scale);
        var pad = Metrics.Space.Sm * scale;
        PrismType.Draw(drawList, new Vector2(final.Min.X + pad, final.Max.Y - labelLine - pad),
            Loc.T(L.Prism.FinalTitle), PrismArt.Fore, PrismFace.Label, labelPixels);
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
            PrismArt.Placeholder(drawList, frame, false, scale);
            var numeralPixels = width * PlateNumeralShare;
            PrismType.Draw(drawList,
                new Vector2(frame.Min.X + gap, frame.Max.Y - PrismType.Snap(PrismFace.Numerals, numeralPixels)),
                Plain[place], PrismArt.Fore, PrismFace.Numerals, numeralPixels);
            var prize = PrizeLine(prizes, place - 1);
            if (prize.Length > 0)
            {
                var line = PrismType.LineHeight(PrismFace.Label, labelPixels);
                var glyph = line * CurrencyGlyph.GlyphFraction;
                CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
                    new Vector2(frame.Min.X + glyph * 0.5f, bottom + gap + line * 0.5f), glyph);
                PrismType.Draw(drawList, new Vector2(frame.Min.X + CurrencyGlyph.Reserve(line), bottom + gap), prize,
                    PrismArt.Fore, PrismFace.Label, labelPixels);
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

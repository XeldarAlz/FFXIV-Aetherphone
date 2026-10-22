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
    private const float GuideTitleUnits = PrismType.TitleUnits;
    private const float GuideTitleMinUnits = PrismType.HeadingUnits;
    private const int GuideTitleLines = 2;
    private const int GuideCopyLines = 3;
    private const float GuidePlateDelay = 0.04f;
    private const float GuideCopyDelay = 0.12f;
    private const float GuideFillDelay = 0.45f;
    private const float PlateStripShare = 0.14f;
    private const float PlateRowShare = 0.18f;
    private const float PlateDigitShare = 0.45f;
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

    private static readonly string[] GuideCounters = BuildGuideCounters();
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
        if (guideReturn == PrismTab.Week)
        {
            introDelay = IntroSeconds;
        }
    }

    private void DrawGuide(ImDrawListPtr drawList, Rect content, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var width = right - left;
        var last = guidePage == GuideTitles.Length - 1;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        PrismArt.StoryBars(drawList, left, right, content.Min.Y + StoryBarGapUnits * scale, GuideTitles.Length,
            guidePage, 1f, scale);
        var row = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale;
        if (PrismArt.Link(drawList, new Vector2(left, row), Loc.T(L.Prism.Back), PrismArt.Muted, scale))
        {
            TurnGuidePage(-1);
            return;
        }

        var skip = Loc.T(L.Prism.Skip);
        if (!last && PrismArt.Link(drawList,
                new Vector2(right - PrismType.Measure(skip, PrismFace.Label, labelPixels).X, row), skip,
                PrismArt.Muted, scale))
        {
            FinishGuide();
            return;
        }

        var strip = StripRect(content, scale);
        if (PrismArt.Strip(drawList, strip, Loc.T(last ? L.Prism.GetStarted : L.Common.Next), Arrow, true))
        {
            TurnGuidePage(1);
            return;
        }

        var title = Loc.T(GuideTitles[guidePage]);
        var pixels = PrismType.Fit(title, PrismFace.Headline, width, GuideTitleUnits * scale,
            GuideTitleMinUnits * scale, GuideTitleLines, out var lines);
        var copy = PrismType.Sentences(Loc.T(GuideBodies[guidePage]), TextStyles.Subheadline, width);
        var copyWidth = PrismType.BalancedWidth(copy, TextStyles.Subheadline, width);
        var copyHeight = MathF.Max(Typography.MeasureWrappedBlock(copy, TextStyles.Subheadline, copyWidth).Y,
            Typography.LineHeight(TextStyles.Subheadline) * GuideCopyLines);
        var headlineLine = PrismType.LineHeight(PrismFace.Headline, pixels);
        var textTop = strip.Min.Y - Metrics.Space.Xxl * scale - copyHeight - Metrics.Space.Md * scale -
                      lines.Length * headlineLine - Metrics.Space.Sm * scale - labelLine;
        var plate = new Rect(new Vector2(left, row + labelLine + Metrics.Space.Xxl * scale),
            new Vector2(right, textTop - Metrics.Space.Xxl * scale));
        if (plate.Height > 0f)
        {
            var mark = PrismMotion.Mark(drawList);
            DrawGuidePlate(drawList, plate, scale);
            PrismMotion.Center(drawList, mark, plate);
            PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, GuidePlateDelay, Motion.PageSettle),
                PrismMotion.RiseUnits * scale);
        }

        var text = PrismMotion.Mark(drawList);
        var top = textTop;
        PrismType.Draw(drawList, new Vector2(left, top), GuideCounters[guidePage], PrismArt.Muted, PrismFace.Label,
            labelPixels);
        top += labelLine + Metrics.Space.Sm * scale;
        top += PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore, PrismFace.Headline,
            pixels) + Metrics.Space.Md * scale;
        Typography.DrawWrappedLeft(new Vector2(left, top), copy, PrismArt.Muted, TextStyles.Subheadline, copyWidth);
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
        PrismArt.Swatch(drawList, frame, 0);
        var pad = Metrics.Space.Md * scale;
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, frame.Min + new Vector2(pad, pad), DisplayName, PrismArt.PhotoFore, PrismFace.Label,
            labelPixels);
        var rule = frame.Min.Y + pad + PrismType.LineHeight(PrismFace.Label, labelPixels);
        PrismArt.SpectrumRule(drawList, frame.Min.X + pad, frame.Max.X - pad, rule, scale);
        var bar = frame.Height * PlateBarShare;
        var strip = new Rect(new Vector2(frame.Min.X + pad, frame.Max.Y - pad - bar * PlateStripBars),
            new Vector2(frame.Max.X - pad, frame.Max.Y - pad));
        drawList.AddRectFilled(strip.Min, strip.Max, ImGui.GetColorU32(PrismArt.PhotoFore));
        var line = strip.Min.Y - pad - bar * 0.5f;
        drawList.AddRectFilled(new Vector2(strip.Min.X, line),
            new Vector2(strip.Min.X + strip.Width * PlateBylineShare, line + bar * 0.5f),
            ImGui.GetColorU32(Palette.WithAlpha(PrismArt.PhotoFore, PlateBarFaint)));
        line -= bar * PlateTitleGapBars;
        drawList.AddRectFilled(new Vector2(strip.Min.X, line),
            new Vector2(strip.Min.X + strip.Width * PlateTitleShare, line + bar),
            ImGui.GetColorU32(PrismArt.PhotoFore));
    }

    private void DrawEnterPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var stripHeight = plate.Height * PlateStripShare;
        var gap = Metrics.Space.Md * scale;
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height - stripHeight - gap);
        var fill = PrismMotion.At(screenAge, GuideFillDelay, Motion.Sheet);
        PrismArt.Swatch(drawList, frame, 1);
        drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Back, 1f - fill)));
        drawList.AddRect(frame.Min, frame.Max, ImGui.GetColorU32(PrismArt.Faint), 0f,
            ImDrawFlags.None, PrismArt.HairlineWidth(scale));
        var plus = frame.Width * PlateNumeralShare;
        var size = PrismType.Measure("+", PrismFace.Headline, plus);
        PrismType.Draw(drawList, frame.Center - size * 0.5f, "+",
            Palette.WithAlpha(PrismArt.Muted, PrismArt.Muted.W * (1f - fill)), PrismFace.Headline, plus);
        DrawPlateStrip(drawList, new Rect(new Vector2(frame.Min.X, frame.Max.Y + gap), new Vector2(frame.Max.X, plate.Max.Y)),
            Loc.T(L.Prism.Submit), Arrow);
    }

    private static void DrawPlateStrip(ImDrawListPtr drawList, Rect strip, string label, string right)
    {
        drawList.AddRectFilled(strip.Min, strip.Max, ImGui.GetColorU32(PrismArt.Fore));
        var labelPixels = PrismArt.LabelPixels();
        var inset = strip.Height * 0.32f;
        var top = strip.Center.Y - labelPixels * 0.5f;
        PrismType.Draw(drawList, new Vector2(strip.Min.X + inset, top),
            PrismType.FitLabel(label, labelPixels, strip.Width - inset * 2f), PrismArt.OnFore, PrismFace.Label, labelPixels);
        if (right.Length > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(strip.Max.X - inset, top), right, PrismArt.OnFore, PrismFace.Label,
                labelPixels);
        }
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
        PrismArt.Swatch(drawList, frame, 4);
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
            new Vector2(rowsLeft + frameWidth, frame.Max.Y + gap + stripHeight)), Loc.T(L.Prism.TryOnGlam), string.Empty);
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
            PrismArt.Swatch(drawList, frame, index + 5);
            var hidden = 1f - Math.Clamp(shown - index, 0f, 1f);
            drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Back, hidden)));
            drawList.AddRect(frame.Min, frame.Max, ImGui.GetColorU32(PrismArt.Faint), 0f,
                ImDrawFlags.None, PrismArt.HairlineWidth(scale));
        }

        var labelPixels = PrismArt.LabelPixels();
        var rowHeight = (plate.Max.Y - plate.Min.Y - photoHeight - gap * 2f) / PlateRows;
        var arrowWidth = PrismType.Measure(Arrow, PrismFace.Label, labelPixels).X;
        for (var index = 0; index < PlateRows; index++)
        {
            var top = plate.Min.Y + photoHeight + gap * 2f + rowHeight * index;
            DrawPlateRow(drawList, left, top, rowWidth - arrowWidth - gap, rowHeight, 0.8f - index * 0.15f);
            PrismType.DrawRight(drawList, new Vector2(left + rowWidth, top + (rowHeight - labelPixels) * 0.5f), Arrow,
                PrismArt.Muted, PrismFace.Label, labelPixels);
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
        for (var index = 0; index < GridColumns * 2; index++)
        {
            var min = new Vector2(left + index % 2 * (tileWidth + gap), plate.Min.Y + index / 2 * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            PrismArt.Swatch(drawList, tile, index * 3);
            for (var piece = 0; piece < CollagePieces; piece++)
            {
                var iconMin = new Vector2(min.X + pad + piece % 2 * (icon + pad * 0.5f),
                    min.Y + pad + piece / 2 * (icon + pad * 0.5f));
                drawList.AddRect(iconMin, iconMin + new Vector2(icon, icon),
                    ImGui.GetColorU32(Palette.WithAlpha(PrismArt.PhotoFore, PlateBarFaint)), 0f,
                        ImDrawFlags.None, PrismArt.HairlineWidth(scale));
            }

            var barHeight = tileHeight * PlateBarShare * 0.6f;
            drawList.AddRectFilled(new Vector2(min.X + pad, tile.Max.Y - pad - barHeight),
                new Vector2(min.X + pad + (tileWidth - pad * 2f) * (0.7f - index * 0.1f), tile.Max.Y - pad),
                ImGui.GetColorU32(PrismArt.PhotoFore));
        }
    }

    private void DrawJudgePlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var gap = Metrics.Space.Md * scale;
        var rowHeight = plate.Height * PlateRowShare;
        var frame = Portrait(plate.Center.X, plate.Min.Y, plate.Height - rowHeight - labelLine - gap * 2f);
        PrismArt.Swatch(drawList, frame, 2);
        var rowWidth = MathF.Min(plate.Width, rowHeight * PrismArt.Stars * 1.2f);
        var row = new Rect(new Vector2(plate.Center.X - rowWidth * 0.5f, frame.Max.Y + gap),
            new Vector2(plate.Center.X + rowWidth * 0.5f, frame.Max.Y + gap + rowHeight));
        var cellGap = Metrics.Space.Xs * scale;
        var cellWidth = (row.Width - cellGap * (PrismArt.Stars - 1)) / PrismArt.Stars;
        var digitPixels = row.Height * PlateDigitShare;
        var fill = PrismMotion.At(screenAge, GuideFillDelay, Motion.Release);
        for (var index = 0; index < PrismArt.Stars; index++)
        {
            var min = new Vector2(row.Min.X + index * (cellWidth + cellGap), row.Min.Y);
            var max = new Vector2(min.X + cellWidth, row.Max.Y);
            var share = index + 1 == GuideChosenScore ? fill : 0f;
            drawList.AddRect(min, max, ImGui.GetColorU32(share > 0f ? PrismArt.Fore : PrismArt.Faint), 0f,
                ImDrawFlags.None, PrismArt.HairlineWidth(scale));
            if (share > 0f)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, share)));
            }

            var digit = Plain[index + 1];
            var size = PrismType.Measure(digit, PrismFace.Headline, digitPixels);
            PrismType.Draw(drawList, (min + max) * 0.5f - size * 0.5f, digit,
                Vector4.Lerp(PrismArt.Fore, PrismArt.OnFore, share), PrismFace.Headline, digitPixels);
        }

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
            PrismArt.Swatch(drawList, heat, index + 3);
            var letter = HeatLetters[index];
            var size = PrismType.Measure(letter, PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(heat.Center.X - size.X * 0.5f, heat.Max.Y), letter, PrismArt.Muted,
                PrismFace.Label, labelPixels);
            drawList.AddLine(new Vector2(heat.Center.X, heat.Max.Y + labelLine),
                new Vector2(final.Center.X, final.Min.Y), link, PrismArt.HairlineWidth(scale));
        }

        PrismArt.Swatch(drawList, final, 0);
        var pad = Metrics.Space.Sm * scale;
        PrismType.Draw(drawList, new Vector2(final.Min.X + pad, final.Max.Y - labelLine - pad),
            Loc.T(L.Prism.FinalTitle), PrismArt.PhotoFore, PrismFace.Label, labelPixels);
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
            PrismArt.Swatch(drawList, frame, place * 2);
            var numeralPixels = width * PlateNumeralShare;
            PrismType.Draw(drawList,
                new Vector2(frame.Min.X + gap, frame.Max.Y - PrismType.Snap(PrismFace.Numerals, numeralPixels)),
                Plain[place], PrismArt.PhotoFore, PrismFace.Numerals, numeralPixels);
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

    private static string[] BuildGuideCounters()
    {
        var counters = new string[GuideTitles.Length];
        var total = counters.Length.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        for (var index = 0; index < counters.Length; index++)
        {
            counters[index] = string.Concat((index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                " / ", total);
        }

        return counters;
    }
}

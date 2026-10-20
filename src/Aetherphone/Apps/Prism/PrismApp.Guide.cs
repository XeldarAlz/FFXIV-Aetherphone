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
    private const float GuideTitleUnits = 32f;
    private const float GuideTitleMinUnits = 22f;
    private const int GuideTitleLines = 2;
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

    private static readonly LocString[] GuideTitles =
    {
        L.Prism.GuideIntroTitle, L.Prism.GuideEnterTitle, L.Prism.JudgeHeat, L.Prism.FinalTitle,
        L.Prism.GuideRevealTitle,
    };

    private static readonly LocString[] GuideBodies =
    {
        L.Prism.GuideIntroBody, L.Prism.GuideEnterBody, L.Prism.GuideJudgeBody, L.Prism.GuideFinalBody,
        L.Prism.GuideRevealBody,
    };

    private static readonly string[] GuideCounters = BuildGuideCounters();
    private static readonly string[] HeatLetters = { "A", "B", "C" };
    private static readonly int[] PodiumSlots = { 2, 1, 3 };

    private readonly CachedText[] prizeTexts = new CachedText[PodiumPlaces];
    private int guidePage;

    private void StartGuide()
    {
        guidePage = 0;
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
        Go(PrismScreen.Cover, -1);
        introDelay = IntroSeconds;
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
        if (PrismArt.Strip(drawList, strip, Loc.T(last ? L.Prism.GetStarted : L.Common.Next), "→", true))
        {
            TurnGuidePage(1);
            return;
        }

        var title = Loc.T(GuideTitles[guidePage]);
        var pixels = PrismType.Fit(title, PrismFace.Headline, width, GuideTitleUnits * scale,
            GuideTitleMinUnits * scale, GuideTitleLines, out var lines);
        var copy = PrismType.Sentences(Loc.T(GuideBodies[guidePage]), TextStyles.Subheadline, width);
        var copyWidth = PrismType.BalancedWidth(copy, TextStyles.Subheadline, width);
        var copyHeight = Typography.MeasureWrappedBlock(copy, TextStyles.Subheadline, copyWidth).Y;
        var headlineLine = PrismType.LineHeight(PrismFace.Headline, pixels);
        var textTop = strip.Min.Y - Metrics.Space.Xxl * scale - copyHeight - Metrics.Space.Md * scale -
                      lines.Length * headlineLine - Metrics.Space.Sm * scale - labelLine;
        var plate = new Rect(new Vector2(left, row + labelLine + Metrics.Space.Xxl * scale),
            new Vector2(right, textTop - Metrics.Space.Xxl * scale));
        if (plate.Height > 0f)
        {
            var mark = PrismMotion.Mark(drawList);
            DrawGuidePlate(drawList, plate, scale);
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
                DrawEnterPlate(drawList, plate, scale);
                break;
            case 2:
                DrawJudgePlate(drawList, plate, scale);
                break;
            case 3:
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
        var strip = new Rect(new Vector2(frame.Min.X + pad, frame.Max.Y - pad - bar * 1.6f),
            new Vector2(frame.Max.X - pad, frame.Max.Y - pad));
        drawList.AddRectFilled(strip.Min, strip.Max, ImGui.GetColorU32(PrismArt.PhotoFore));
        var line = strip.Min.Y - pad - bar * 0.5f;
        drawList.AddRectFilled(new Vector2(strip.Min.X, line),
            new Vector2(strip.Min.X + strip.Width * 0.45f, line + bar * 0.5f),
            ImGui.GetColorU32(Palette.WithAlpha(PrismArt.PhotoFore, PlateBarFaint)));
        line -= bar * 1.4f;
        drawList.AddRectFilled(new Vector2(strip.Min.X, line), new Vector2(strip.Min.X + strip.Width * 0.8f, line + bar),
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
        drawList.AddRect(frame.Min, frame.Max, ImGui.GetColorU32(PrismArt.Faint), 0f, ImDrawFlags.None, 1f);
        var plus = frame.Width * PlateNumeralShare;
        var size = PrismType.Measure("+", PrismFace.Headline, plus);
        PrismType.Draw(drawList, frame.Center - size * 0.5f, "+",
            Palette.WithAlpha(PrismArt.Muted, PrismArt.Muted.W * (1f - fill)), PrismFace.Headline, plus);
        var strip = new Rect(new Vector2(frame.Min.X, frame.Max.Y + gap), new Vector2(frame.Max.X, plate.Max.Y));
        drawList.AddRectFilled(strip.Min, strip.Max, ImGui.GetColorU32(PrismArt.Fore));
        var labelPixels = PrismArt.LabelPixels();
        var inset = strip.Height * 0.32f;
        var top = strip.Center.Y - labelPixels * 0.5f;
        PrismType.Draw(drawList, new Vector2(strip.Min.X + inset, top), Loc.T(L.Prism.Submit), PrismArt.OnFore,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(strip.Max.X - inset, top), "→", PrismArt.OnFore, PrismFace.Label,
            labelPixels);
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
                ImDrawFlags.None, 1f);
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
        PrismType.Draw(drawList, new Vector2(row.Min.X, anchors), Loc.T(L.Prism.ScoreLow), PrismArt.ScoreLowInk,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(row.Max.X, anchors), Loc.T(L.Prism.ScoreHigh),
            PrismArt.ScoreHighInk, PrismFace.Label, labelPixels);
    }

    private static void DrawFinalPlate(ImDrawListPtr drawList, Rect plate, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var heatHeight = plate.Height * PlateHeatShare;
        var heatWidth = heatHeight * PrismStore.EntryRatio;
        var gap = MathF.Max(0f, (plate.Width - heatWidth * HeatLetters.Length) / (HeatLetters.Length + 1));
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
                new Vector2(final.Center.X, final.Min.Y), link, 1f);
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
                PrismType.Draw(drawList, new Vector2(frame.Min.X, bottom + gap), prize, PrismArt.Fore,
                    PrismFace.Label, labelPixels);
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

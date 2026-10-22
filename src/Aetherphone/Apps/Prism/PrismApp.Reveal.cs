using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float RevealNumeralUnits = 200f;
    private const float PodiumNumeralUnits = 48f;
    private const float PodiumShadeShare = 0.45f;
    private const float RowThumbUnits = 36f;
    private const float CardThumbUnits = 48f;
    private const float RankColumnUnits = 32f;
    private const int PodiumSize = 3;
    private const float PrintNumeralUnits = 64f;
    private const float PrintWidthShare = 0.78f;
    private const float PrintPadUnits = 12f;
    private const float PrintRiseUnits = 40f;
    private const float RevealZoom = 0.12f;
    private const float RevealDim = 0.6f;
    private const float FlashAlpha = 0.5f;
    private const float NumeralDelay = 0.15f;
    private const float RevealPageSeconds = 5f;
    private const float RevealHoldSeconds = 0.25f;
    private const float RevealBackShare = 0.33f;
    private const float RevealTapTopUnits = 56f;
    private const float StoryBarGapUnits = 8f;
    private const float StoryRowGapUnits = 12f;
    private const float SectionIndexUnits = 36f;
    private const float RuleRevealDelay = 0.25f;
    private const float CreditDelay = 0.3f;
    private const float RefractionDelay = 0.05f;
    private const float PrintDelay = 0.05f;
    private const float HintDelay = 0.5f;
    private const float RevealPitchTop = 1.3f;
    private const float RevealPitchStep = 0.12f;

    private readonly List<PrismPlacingDto> finalOrder = new();
    private readonly List<PrismPlacingDto> heatOrder = new();

    private static readonly Vector4 PodiumShade = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 PodiumClear = new(0f, 0f, 0f, 0f);

    private PrismResultsDto? orderedFor;
    private int revealIndex;
    private int soundedPage = -1;
    private float revealPageSeconds;
    private float revealHoldSeconds;
    private CachedText placingText;
    private CachedText printLine;
    private CachedText heatFact;
    private CachedText rankFact;
    private CachedText topFact;
    private CachedText coinFact;

    private void StartReveal()
    {
        revealIndex = 0;
        revealPageSeconds = 0f;
        soundedPage = -1;
        Go(PrismScreen.Reveal, 0);
    }

    private void TurnRevealPage(int delta, int pages)
    {
        revealIndex = Math.Clamp(revealIndex + delta, 0, pages - 1);
        revealPageSeconds = 0f;
        Go(PrismScreen.Reveal, 0);
    }

    private void DrawReveal(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (store.Results is not { } results)
        {
            Go(PrismScreen.Cover);
            return;
        }

        SyncOrder(results);
        var pages = finalOrder.Count + 1;
        revealIndex = Math.Min(revealIndex, pages - 1);
        var summary = revealIndex == pages - 1;
        if (soundedPage != revealIndex)
        {
            soundedPage = revealIndex;
            PlayRevealSound(summary ? 0 : finalOrder[finalOrder.Count - 1 - revealIndex].Rank);
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var held = !summary && ImGui.IsMouseDown(ImGuiMouseButton.Left) && UiInteract.Hover(content.Min, content.Max);
        revealHoldSeconds = held ? revealHoldSeconds + delta : 0f;
        if (!summary && revealHoldSeconds < RevealHoldSeconds)
        {
            revealPageSeconds += delta;
        }

        if (!summary && revealPageSeconds >= RevealPageSeconds)
        {
            TurnRevealPage(1, pages);
            return;
        }

        if (summary)
        {
            DrawRevealSummary(drawList, content, results, scale);
        }
        else
        {
            DrawRevealPlacing(drawList, full, content, results, finalOrder[finalOrder.Count - 1 - revealIndex], scale);
            DrawRevealTaps(drawList, content, pages, scale);
        }

        var gutter = PrismArt.Gutter(scale);
        PrismArt.OverPhoto(false);
        PrismArt.StoryBars(drawList, content.Min.X + gutter, content.Max.X - gutter,
            content.Min.Y + StoryBarGapUnits * scale, pages, revealIndex,
            summary ? 1f : revealPageSeconds / RevealPageSeconds, scale);
        DrawRevealTop(drawList, content, results, summary, scale);
    }

    private void DrawRevealTaps(ImDrawListPtr drawList, Rect content, int pages, float scale)
    {
        var top = content.Min.Y + RevealTapTopUnits * scale;
        var split = content.Min.X + content.Width * RevealBackShare;
        var backZone = new Rect(new Vector2(content.Min.X, top), new Vector2(split, content.Max.Y));
        var nextZone = new Rect(new Vector2(split, top), content.Max);
        var tap = revealHoldSeconds < RevealHoldSeconds;
        if (UiInteract.Click(backZone.Min, backZone.Max, UiInteract.Hover(backZone.Min, backZone.Max)) && tap)
        {
            TurnRevealPage(-1, pages);
            return;
        }

        if (UiInteract.Click(nextZone.Min, nextZone.Max, UiInteract.Hover(nextZone.Min, nextZone.Max)) && tap)
        {
            TurnRevealPage(1, pages);
            return;
        }

        if (revealIndex != 0 || configuration.PrismRevealedWeek.Length > 0)
        {
            return;
        }

        var hint = PrismMotion.Mark(drawList);
        var label = Loc.T(L.Prism.TapHint);
        var labelPixels = PrismArt.LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(content.Max.X - PrismArt.Gutter(scale) - size.X, content.Center.Y),
            label, PrismArt.Fore, PrismFace.Label, labelPixels);
        PrismMotion.Rise(drawList, hint, PrismMotion.At(screenAge, HintDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
    }

    private void DrawRevealSummary(ImDrawListPtr drawList, Rect content, PrismResultsDto results, float scale)
    {
        var top = content.Min.Y + RevealTapTopUnits * scale;
        var stripsTop = StripRect(content, scale, 1).Min.Y - Metrics.Space.Lg * scale;
        if (results.MyHeatRank > 0)
        {
            var card = PrismMotion.Mark(drawList);
            var width = content.Width * PrintWidthShare;
            var frame = PrintFrame(Vector2.Zero, width, scale);
            var fit = MathF.Min(1f, (stripsTop - top) / frame.Height);
            var printWidth = width * fit;
            var origin = new Vector2(content.Center.X - printWidth * 0.5f,
                top + (stripsTop - top - frame.Height * fit) * 0.5f);
            DrawPrint(drawList, PrintFrame(origin, printWidth, scale), results, scale);
            PrismMotion.Rise(drawList, card, PrismMotion.At(screenAge, PrintDelay, Motion.Sheet),
                PrintRiseUnits * scale);
        }
        else
        {
            var note = PrismMotion.Mark(drawList);
            DrawNotice(drawList, content, results.Title, CountLine(results.EntryCount), scale, 2);
            PrismMotion.Rise(drawList, note, PrismMotion.At(screenAge, PrintDelay, Motion.PageSettle),
                PrismMotion.RiseUnits * scale);
        }

        var strips = PrismMotion.Mark(drawList);
        if (PrismArt.Strip(drawList, StripRect(content, scale, 1), Loc.T(L.Prism.FinalTitle), Arrow,
                true))
        {
            FinishReveal(results);
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.ReplayReveal),
                string.Empty, false))
        {
            StartReveal();
        }

        PrismMotion.Rise(drawList, strips, PrismMotion.At(screenAge, HintDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
    }

    private void DrawRevealTop(ImDrawListPtr drawList, Rect content, PrismResultsDto results, bool summary,
        float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var labelPixels = PrismArt.LabelPixels();
        var top = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale;
        var label = Loc.T(summary ? L.Prism.YourPlacing : L.Prism.FinalTitle);
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, top), label, PrismArt.Fore, PrismFace.Label,
            labelPixels);
        var issue = IssueLine(results.Issue);
        var close = Loc.T(L.Prism.Back);
        var closeWidth = PrismType.Measure(close, PrismFace.Label, labelPixels).X;
        if (issue.Length > 0)
        {
            var issueWidth = PrismType.Measure(issue, PrismFace.Label, labelPixels).X;
            PrismType.Draw(drawList, new Vector2(content.Max.X - gutter - closeWidth - Metrics.Space.Lg * scale -
                issueWidth, top), issue, PrismArt.Muted, PrismFace.Label, labelPixels);
        }

        if (PrismArt.Link(drawList, new Vector2(content.Max.X - gutter - closeWidth, top), close, PrismArt.Muted,
                scale))
        {
            Go(PrismScreen.Cover, -1);
        }
    }

    private void DrawRevealPlacing(ImDrawListPtr drawList, Rect full, Rect content, PrismResultsDto results,
        PrismPlacingDto placing, float scale)
    {
        var settle = PrismMotion.At(screenAge, 0f, Motion.Sheet);
        DrawLook(drawList, full, placing.ImageUrl, false, 1f, 1f + RevealZoom * (1f - settle));
        PrismArt.Shade(drawList, full, scale);
        PrismArt.OverPhoto(true);
        onPhoto = true;
        drawList.AddRectFilled(full.Min, full.Max,
            ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Back, RevealDim * (1f - settle))));
        if (placing.Rank == 1)
        {
            drawList.AddRectFilled(full.Min, full.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore,
                FlashAlpha * (1f - PrismMotion.At(screenAge, 0f, Motion.Release)))));
            PrismArt.Refraction(drawList, full, PrismMotion.At(screenAge, RefractionDelay, Motion.Sheet));
        }

        var gutter = PrismArt.Gutter(scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var creditTop = content.Max.Y - PrismArt.BottomMargin(scale) - labelPixels - Metrics.Space.Lg * scale - labelLine;
        var ruleY = creditTop - Metrics.Space.Sm * scale;
        var span = content.Width - gutter * 2f;
        PrismArt.Hairline(drawList, content.Min.X + gutter,
            content.Min.X + gutter + span * PrismMotion.At(screenAge, RuleRevealDelay, Motion.Sheet), ruleY,
            PrismArt.Fore, scale);
        var credit = PrismMotion.At(screenAge, CreditDelay, Motion.PageSettle);
        DrawWipe(drawList, new Vector2(content.Min.X + gutter, creditTop), placing.DisplayName,
            PrismFace.Label, labelPixels, credit, PrismArt.Fore);
        var world = PlacingDetail(placing);
        DrawWipe(drawList, new Vector2(content.Max.X - gutter - PrismType.Measure(world, PrismFace.Label, labelPixels).X,
            creditTop), world, PrismFace.Label, labelPixels, credit, PrismArt.Fore);
        var pixels = RevealNumeralUnits * scale;
        var numeral = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
        var numeralTop = creditTop - Metrics.Space.Lg * scale - PrismType.Snap(PrismFace.Numerals, pixels);
        DrawWipe(drawList, new Vector2(content.Min.X + gutter, numeralTop), numeral, PrismFace.Numerals, pixels,
            PrismMotion.At(screenAge, NumeralDelay, Motion.PageSettle), PrismArt.Fore);
        DrawWipe(drawList, new Vector2(content.Min.X + gutter, numeralTop - labelLine), Loc.T(L.Prism.PlaceLabel),
            PrismFace.Label, labelPixels, credit, PrismArt.Muted);
    }

    private static void PlayRevealSound(int rank)
    {
        if (rank == 1)
        {
            UiFeedback.Play(UiSound.GameWin);
            return;
        }

        if (rank > 1)
        {
            UiFeedback.PlayPitched(UiSound.Tap, RevealPitchTop - RevealPitchStep * rank);
            return;
        }

        UiFeedback.Play(UiSound.Shutter);
    }

    private void FinishReveal(PrismResultsDto results)
    {
        configuration.PrismRevealedWeek = results.WeekId;
        configuration.PrismRevealedRevision = results.Revision;
        configuration.Save();
        Go(PrismScreen.Final);
    }

    private void DrawFinal(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (store.Results is not { } results)
        {
            Go(PrismScreen.Cover);
            return;
        }

        SyncOrder(results);
        var top = DrawHeader(drawList, content, string.Empty, IssueLine(results.Issue), true, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = origin.Y;
        cursor = DrawSectionTitle(list, left, cursor, width, results.Title, scale);
        if (finalOrder.Count == 0 && heatOrder.Count == 0 && results.MyHeatRank <= 0)
        {
            var empty = Loc.T(L.Prism.NoPodium);
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), empty, PrismArt.Muted,
                TextStyles.Subheadline, PrismType.BalancedWidth(empty, TextStyles.Subheadline, width)) +
                      Metrics.Space.Xl * scale;
        }

        if (finalOrder.Count > 0)
        {
            cursor = DrawPodium(list, left, cursor, width, scale);
            if (HasOutfit(finalOrder[0].Outfit) || finalOrder[0].GlamId.Length > 0)
            {
                if (PrismArt.Link(list, new Vector2(left, cursor), Loc.T(L.Prism.WinnersOutfit), PrismArt.Muted, scale))
                {
                    OpenGlam(finalOrder[0]);
                }

                cursor += Metrics.Size.TapTarget * scale;
            }
        }

        if (results.MyHeatRank > 0)
        {
            cursor = DrawPlacingCard(list, left, cursor, width, results, scale);
        }

        var section = 0;
        if (finalOrder.Count > PodiumSize)
        {
            cursor = DrawSectionLabel(list, left, cursor, width, section++, Loc.T(L.Prism.FinalTitle), scale);
            for (var index = PodiumSize; index < finalOrder.Count; index++)
            {
                cursor = DrawRankRow(list, left, cursor, width, finalOrder[index], scale);
            }

            cursor += Metrics.Space.Xl * scale;
        }

        if (heatOrder.Count > 0)
        {
            cursor = DrawSectionLabel(list, left, cursor, width, section, Loc.T(L.Prism.YourHeat), scale);
            for (var index = 0; index < heatOrder.Count; index++)
            {
                cursor = DrawRankRow(list, left, cursor, width, heatOrder[index], scale);
            }

            cursor += Metrics.Space.Xl * scale;
        }

        if (finalOrder.Count > 0 && PrismArt.Link(list, new Vector2(left, cursor),
                Loc.T(L.Prism.ReplayReveal), PrismArt.Muted, scale))
        {
            StartReveal();
        }

        cursor += finalOrder.Count > 0 ? Metrics.Size.TapTarget * scale : 0f;
        if (PrismArt.Link(list, new Vector2(left, cursor), Loc.T(L.Prism.WhatWinnersGet), PrismArt.Muted, scale))
        {
            OpenRewards();
        }

        cursor += Metrics.Size.TapTarget * scale + PrismArt.BottomMargin(scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
    }

    private static float DrawSectionTitle(ImDrawListPtr drawList, float left, float top, float width, string title,
        float scale)
    {
        var pixels = PrismType.Fit(title, PrismFace.Headline, width, NoticeMaxUnits * scale, NoticeMinUnits * scale,
            NoticeLines, out var lines);
        return top + PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore,
            PrismFace.Headline, pixels) + Metrics.Space.Xl * scale;
    }

    private static float DrawSectionLabel(ImDrawListPtr drawList, float left, float top, float width, int index,
        string label, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, new Vector2(left, top), Padded[Math.Min(index + 1, Padded.Length - 1)],
            PrismArt.Muted, PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(left + SectionIndexUnits * scale, top), label,
            PrismArt.Fore, PrismFace.Label, labelPixels);
        var rule = top + PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale;
        PrismArt.Hairline(drawList, left, left + width, rule, PrismArt.Faint, scale);
        return rule + Metrics.Space.Md * scale;
    }

    private float DrawPodium(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (PodiumSize - 1)) / PodiumSize;
        var photoHeight = column / PrismStore.EntryRatio;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var numeralPixels = PodiumNumeralUnits * scale;
        var count = Math.Min(PodiumSize, finalOrder.Count);
        for (var index = 0; index < count; index++)
        {
            var placing = finalOrder[index];
            var x = left + index * (column + gap);
            var photo = new Rect(new Vector2(x, top), new Vector2(x + column, top + photoHeight));
            DrawLook(drawList, photo, placing.ImageUrl, false);
            TapForOutfit(photo.Min, photo.Max, placing);
            var shade = new Vector2(photo.Min.X, photo.Max.Y - photoHeight * PodiumShadeShare);
            drawList.AddRectFilledMultiColor(shade, photo.Max, ImGui.GetColorU32(PodiumClear),
                ImGui.GetColorU32(PodiumClear), ImGui.GetColorU32(PodiumShade), ImGui.GetColorU32(PodiumShade));
            PrismType.Draw(drawList, new Vector2(photo.Min.X + Metrics.Space.Sm * scale, photo.Max.Y - numeralPixels),
                Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)], PrismArt.PhotoFore, PrismFace.Numerals,
                numeralPixels);
            var creditTop = photo.Max.Y + Metrics.Space.Sm * scale;
            drawList.PushClipRect(new Vector2(x, creditTop), new Vector2(x + column, creditTop + labelLine * 2f), true);
            PrismType.Draw(drawList, new Vector2(x, creditTop), placing.DisplayName, PrismArt.Fore,
                PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(x, creditTop + labelLine), PlacingDetail(placing), PrismArt.Muted,
                PrismFace.Label, labelPixels);
            drawList.PopClipRect();
        }

        return top + photoHeight + Metrics.Space.Sm * scale + labelLine * 2f + Metrics.Space.Xl * scale;
    }

    private float DrawPlacingCard(ImDrawListPtr drawList, float left, float top, float width, PrismResultsDto results,
        float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var thumbWidth = CardThumbUnits * scale;
        var thumb = new Rect(new Vector2(left + pad, top + pad),
            new Vector2(left + pad + thumbWidth, top + pad + thumbWidth / PrismStore.EntryRatio));
        var textLeft = thumb.Max.X + pad;
        var textWidth = left + width - pad - textLeft;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var valuePixels = StatUnits * scale;
        var factsTop = top + pad + labelLine + PrismType.LineHeight(PrismFace.Headline, valuePixels);
        var factsHeight = DrawFacts(drawList, new Vector2(textLeft, factsTop), textWidth, results, PrismArt.Muted,
            false, scale);
        var card = new Rect(new Vector2(left, top),
            new Vector2(left + width, MathF.Max(thumb.Max.Y, factsTop + factsHeight) + pad));
        drawList.AddRect(card.Min, card.Max, ImGui.GetColorU32(PrismArt.Faint), 0f,
            ImDrawFlags.None, PrismArt.HairlineWidth(scale));
        DrawLook(drawList, thumb, results.MyImageUrl, false);
        var textTop = top + pad;
        PrismType.Draw(drawList, new Vector2(textLeft, textTop), Loc.T(L.Prism.YourPlacing), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(textLeft + textWidth, textTop), CoinFact(results), PrismArt.Fore,
            PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(textLeft, textTop + labelLine), PlacingLine(results), PrismArt.Fore,
            PrismFace.Headline, valuePixels);
        DrawFacts(drawList, new Vector2(textLeft, factsTop), textWidth, results, PrismArt.Muted, true, scale);
        return card.Max.Y + Metrics.Space.Xxl * scale;
    }

    private float DrawFacts(ImDrawListPtr drawList, Vector2 topLeft, float width, PrismResultsDto results,
        Vector4 color, bool draw, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var line = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var cursor = topLeft;
        DrawFact(drawList, HeatFact(results), topLeft.X, width, color, draw, ref cursor, scale);
        DrawFact(drawList, Fact(ref rankFact, L.Prism.FinalRank, results.MyFinalRank), topLeft.X, width, color, draw,
            ref cursor, scale);
        DrawFact(drawList, Fact(ref topFact, L.Prism.TopPercent, results.Percentile), topLeft.X, width, color, draw,
            ref cursor, scale);
        return cursor.Y + line - topLeft.Y;
    }

    private static void DrawFact(ImDrawListPtr drawList, string text, float left, float width, Vector4 color,
        bool draw, ref Vector2 cursor, float scale)
    {
        if (text.Length == 0)
        {
            return;
        }

        var labelPixels = PrismArt.LabelPixels();
        var textWidth = PrismType.Measure(text, PrismFace.Label, labelPixels).X;
        if (cursor.X > left && cursor.X + textWidth > left + width)
        {
            cursor = new Vector2(left, cursor.Y + PrismType.LineHeight(PrismFace.Label, labelPixels));
        }

        if (draw)
        {
            PrismType.Draw(drawList, cursor, text, color, PrismFace.Label, labelPixels);
        }

        cursor.X += textWidth + Metrics.Space.Lg * scale;
    }

    private float DrawRankRow(ImDrawListPtr drawList, float left, float top, float width, PrismPlacingDto placing,
        float scale)
    {
        var thumbWidth = RowThumbUnits * scale;
        var thumbHeight = thumbWidth / PrismStore.EntryRatio;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var middle = top + thumbHeight * 0.5f;
        PrismType.Draw(drawList, new Vector2(left, middle - labelLine * 0.5f),
            Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)], PrismArt.Fore, PrismFace.Label, labelPixels);
        var thumbLeft = left + RankColumnUnits * scale;
        TapForOutfit(new Vector2(left, top), new Vector2(left + width, top + thumbHeight), placing);
        DrawLook(drawList, new Rect(new Vector2(thumbLeft, top), new Vector2(thumbLeft + thumbWidth, top + thumbHeight)),
            placing.ImageUrl, false);
        var textLeft = thumbLeft + thumbWidth + Metrics.Space.Md * scale;
        drawList.PushClipRect(new Vector2(textLeft, top), new Vector2(left + width, top + thumbHeight), true);
        PrismType.Draw(drawList, new Vector2(textLeft, middle - labelLine), placing.DisplayName, PrismArt.Fore,
            PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(textLeft, middle), PlacingDetail(placing), PrismArt.Muted, PrismFace.Label,
            labelPixels);
        drawList.PopClipRect();
        var rule = top + thumbHeight + Metrics.Space.Md * scale;
        PrismArt.Hairline(drawList, left, left + width, rule, PrismArt.Faint, scale);
        return rule + Metrics.Space.Md * scale;
    }

    private static Rect PrintFrame(Vector2 origin, float width, float scale)
    {
        var pad = PrintPadUnits * scale;
        var photo = (width - pad * 2f) / PrismStore.EntryRatio;
        var labelLine = PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
        var height = pad * 3f + photo + labelLine * 3f + PrintNumeralUnits * scale;
        return new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
    }

    private void DrawPrint(ImDrawListPtr drawList, Rect card, PrismResultsDto results, float scale)
    {
        drawList.AddRectFilled(card.Min, card.Max, ImGui.GetColorU32(PrismArt.Bone));
        var pad = PrintPadUnits * scale;
        var inner = card.Width - pad * 2f;
        var photo = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Max.X - pad, card.Min.Y + pad + inner / PrismStore.EntryRatio));
        drawList.AddRectFilled(photo.Min, photo.Max, ImGui.GetColorU32(PrismArt.Ink));
        DrawLook(drawList, photo, results.MyImageUrl, false);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var top = photo.Max.Y + pad;
        PrismType.Draw(drawList, new Vector2(photo.Min.X, top), PrintHeading(results), PrismArt.InkMuted,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(photo.Max.X, top), CoinFact(results), PrismArt.Ink,
            PrismFace.Label, labelPixels);
        top += labelLine;
        var pixels = PrintNumeralUnits * scale;
        PrismType.Draw(drawList, new Vector2(photo.Min.X, top), PlacingLine(results), PrismArt.Ink,
            PrismFace.Numerals, pixels);
        DrawFacts(drawList, new Vector2(photo.Min.X, top + pixels), inner, results, PrismArt.InkMuted, true, scale);
    }

    private void SyncOrder(PrismResultsDto results)
    {
        if (ReferenceEquals(orderedFor, results))
        {
            return;
        }

        orderedFor = results;
        Fill(finalOrder, results.FinalPodium);
        Fill(heatOrder, results.HeatPodium);
    }

    private static void Fill(List<PrismPlacingDto> into, PrismPlacingDto[]? podium)
    {
        into.Clear();
        if (podium is not null)
        {
            into.AddRange(podium);
        }

        into.Sort(static (left, right) => left.Rank.CompareTo(right.Rank));
    }

    private string PlacingLine(PrismResultsDto results)
    {
        var key = HashCode.Combine(results.MyHeatRank, results.HeatSize);
        return placingText.IsCurrent(key)
            ? placingText.Value
            : placingText.Store(key, results.MyHeatRank + "/" + results.HeatSize);
    }

    private string PrintHeading(PrismResultsDto results)
    {
        var key = HashCode.Combine(results.WeekId, Loc.Current.Code);
        if (printLine.IsCurrent(key))
        {
            return printLine.Value;
        }

        var heading = results.Issue > 0 ? Loc.T(L.Prism.PrintHeading, DisplayName, results.Issue) : DisplayName;
        return printLine.Store(key, heading);
    }

    private static string PlacingDetail(PrismPlacingDto placing) =>
        placing.World.Length > 0 ? placing.World : placing.Handle;

    private string HeatFact(PrismResultsDto results)
    {
        var key = HashCode.Combine(results.HeatLabel, Loc.Current.Code);
        return heatFact.IsCurrent(key)
            ? heatFact.Value
            : heatFact.Store(key, results.HeatLabel.Length > 0
                ? Loc.T(L.Prism.HeatName, results.HeatLabel)
                : Loc.T(L.Prism.YourHeat));
    }

    private string CoinFact(PrismResultsDto results) => Fact(ref coinFact, L.Prism.CoinsWon, results.CoinsAwarded);

    private static string Fact(ref CachedText cache, LocString format, long value)
    {
        if (value <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(value, Loc.Current.Code);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, Loc.T(format, value));
    }
}

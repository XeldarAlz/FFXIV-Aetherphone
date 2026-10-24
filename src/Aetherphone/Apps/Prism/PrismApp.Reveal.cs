using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

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
    private const float FlashAlpha = 0.25f;
    private const float PodiumLightDelay = 0.26f;
    private const float PodiumLightStagger = 0.18f;
    private const float PodiumDimFloor = 0.26f;
    private const float PodiumRimDelay = 0.95f;
    private const float PlacedPearlAlpha = 0.42f;
    private const float NumeralDelay = 0.15f;
    private const float RevealPageSeconds = 5f;
    private const float RevealHoldSeconds = 0.25f;
    private const float RevealBackShare = 0.33f;
    private const float RevealTapTopUnits = 56f;
    private const float StoryBarGapUnits = 8f;
    private const float StoryRowGapUnits = 12f;
    private const float CreditDelay = 0.3f;
    private const float CreditWorldShare = 0.4f;
    private const float RefractionDelay = 0.05f;
    private const float PrintDelay = 0.05f;
    private const float HintDelay = 0.5f;
    private const float RevealPitchTop = 1.3f;
    private const float RevealPitchStep = 0.12f;

    private readonly List<PrismPlacingDto> finalOrder = new();
    private readonly List<PrismPlacingDto> heatOrder = new();

    private static readonly string PodiumGlyph = IconGlyph.Of(FontAwesomeIcon.Trophy);
    private static readonly Vector4 PodiumShade = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 PodiumClear = new(0f, 0f, 0f, 0f);

    private PrismResultsDto? orderedFor;
    private string profileGlamId = string.Empty;
    private int revealIndex;
    private double podiumLitAt = PrismMotion.LongAgo;
    private int soundedPage = -1;
    private float revealPageSeconds;
    private float revealHoldSeconds;
    private CachedText placingText;
    private CachedText revealTitle;
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
        if (ShownResults() is not { } results)
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

        PrismArt.OverPhoto(false);
        PrismArt.StoryBars(drawList, content.Min.X, content.Max.X, content.Min.Y + StoryBarGapUnits * scale, pages,
            revealIndex, summary ? 1f : revealPageSeconds / RevealPageSeconds, scale);
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
        PrismType.Draw(drawList, new Vector2(content.Max.X - size.X, content.Center.Y), label, PrismArt.Fore,
            PrismFace.Label, labelPixels);
        PrismMotion.Rise(drawList, hint, PrismMotion.At(screenAge, HintDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
    }

    private void DrawRevealSummary(ImDrawListPtr drawList, Rect content, PrismResultsDto results, float scale)
    {
        var top = content.Min.Y + RevealTapTopUnits * scale;
        var final = ActionRect(content, scale, 1);
        var card = PrismMotion.Mark(drawList);
        if (results.MyHeatRank > 0)
        {
            var stripsTop = final.Min.Y - Metrics.Space.Lg * scale;
            var width = content.Width * PrintWidthShare;
            var frame = PrintFrame(Vector2.Zero, width, scale);
            var fit = MathF.Min(1f, (stripsTop - top) / frame.Height);
            var printWidth = width * fit;
            var origin = new Vector2(content.Center.X - printWidth * 0.5f,
                top + (stripsTop - top - frame.Height * fit) * 0.5f);
            DrawPrint(drawList, PrintFrame(origin, printWidth, scale), results, scale);
        }
        else
        {
            PrismType.DrawHero(drawList, new Vector2(content.Min.X, top + Metrics.Space.Xl * scale), results.Title,
                content.Width, PrismArt.Fore);
        }

        PrismMotion.Rise(drawList, card, PrismMotion.At(screenAge, PrintDelay, Motion.Sheet), PrintRiseUnits * scale);
        var actions = PrismMotion.Mark(drawList);
        if (DrawButton(drawList, final, Loc.T(L.Prism.FinalTitle), ButtonStyle.Solid, scale))
        {
            FinishReveal(results);
        }

        var replay = Loc.T(L.Prism.ReplayReveal);
        if (DrawButton(drawList, PlainRect(ActionRect(content, scale).Center, replay, content.Width, 0.5f, scale),
                replay, ButtonStyle.Plain, scale))
        {
            StartReveal();
        }

        PrismMotion.Rise(drawList, actions, PrismMotion.At(screenAge, HintDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
    }

    private void DrawRevealTop(ImDrawListPtr drawList, Rect content, PrismResultsDto results, bool summary,
        float scale)
    {
        var top = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale;
        var line = Typography.LineHeight(PrismType.Caption);
        var glyph = PrismArt.IconUnits * scale;
        var title = RevealTitle(results, summary);
        if (title.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(content.Min.X, top),
                Typography.FitText(title, MathF.Max(1f, content.Width - glyph - Metrics.Space.Sm * scale),
                    PrismType.Caption), PrismArt.Fore, PrismType.Caption);
        }

        var close = new Vector2(content.Max.X - glyph * 0.5f, top + line * 0.5f);
        var reach = new Vector2(Metrics.Size.TapTarget * 0.5f * scale, Metrics.Size.TapTarget * 0.5f * scale);
        HoverTooltip.Show("prism.reveal.close", new Rect(close - reach, close + reach),
            Loc.T(L.Common.Close));
        if (PrismArt.Icon(drawList, close, PhoneIcons.X, scale))
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

        var left = content.Min.X;
        var span = content.Width;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var creditTop = content.Max.Y - PrismArt.BottomMargin(scale) - labelPixels - Metrics.Space.Lg * scale - labelLine;
        var credit = PrismMotion.At(screenAge, CreditDelay, Motion.PageSettle);
        var world = PrismType.FitLabel(PlacingDetail(placing), labelPixels, span * CreditWorldShare);
        var worldWidth = PrismType.Measure(world, PrismFace.Label, labelPixels).X;
        DrawWipe(drawList, new Vector2(left, creditTop),
            PrismType.FitLabel(placing.DisplayName, labelPixels, span - worldWidth - Metrics.Space.Lg * scale),
            PrismFace.Label, labelPixels, credit, PrismArt.Fore);
        DrawWipe(drawList, new Vector2(left + span - worldWidth, creditTop), world, PrismFace.Label, labelPixels,
            credit, PrismArt.Fore);
        var pixels = RevealNumeralUnits * scale;
        var numeral = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
        var numeralTop = creditTop - Metrics.Space.Lg * scale - PrismType.Snap(PrismFace.Numerals, pixels);
        DrawWipe(drawList, new Vector2(left, numeralTop), numeral, PrismFace.Numerals, pixels,
            PrismMotion.At(screenAge, NumeralDelay, Motion.PageSettle), PrismArt.Fore);
        DrawWipe(drawList, new Vector2(left, numeralTop - labelLine), Loc.T(L.Prism.PlaceLabel),
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
        podiumLitAt = ImGui.GetTime();
        Go(PrismScreen.Final);
    }

    private void DrawFinal(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (ShownResults() is not { } results)
        {
            Go(PrismScreen.Cover);
            return;
        }

        FollowPlacingProfile();
        SyncOrder(results);
        if (finalOrder.Count == 0 && heatOrder.Count == 0 && results.MyHeatRank <= 0)
        {
            DrawNoPodium(drawList, full, content, results, scale);
            return;
        }

        var top = DrawBackHeader(drawList, content, results.Title, scale);
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = content.Width;
        var left = origin.X + content.Min.X - full.Min.X;
        var right = left + width;
        var cursor = origin.Y + Metrics.Space.Sm * scale;
        if (finalOrder.Count > 0)
        {
            cursor = DrawSectionTitle(list, left, right, cursor, Loc.T(L.Prism.FinalTitle), string.Empty, scale) +
                     Metrics.Space.Md * scale;
            cursor = DrawPodium(list, left, cursor, width, scale);
            if (HasOutfit(finalOrder[0].Outfit) || finalOrder[0].GlamId.Length > 0)
            {
                var outfit = Loc.T(L.Prism.WinnersOutfit);
                var button = new Rect(new Vector2(left, cursor),
                    new Vector2(left + ButtonWidth([outfit], width, scale), cursor + Button.LargeHeight * scale));
                if (DrawButton(list, button, outfit, ButtonStyle.Plain, scale))
                {
                    OpenGlam(finalOrder[0]);
                }

                cursor = button.Max.Y + Metrics.Space.Lg * scale;
            }

            for (var index = PodiumSize; index < finalOrder.Count; index++)
            {
                cursor = DrawRankRow(list, left, cursor, width, finalOrder[index], scale);
            }

            cursor += Metrics.Space.Xl * scale;
        }

        if (results.MyHeatRank > 0)
        {
            cursor = DrawPlacingRow(list, left, right, cursor, results, scale) + Metrics.Space.Xl * scale;
        }

        if (heatOrder.Count > 0)
        {
            cursor = DrawSectionTitle(list, left, right, cursor, Loc.T(L.Prism.YourHeat),
                results.HeatLabel.Length > 0 ? HeatFact(results) : string.Empty, scale) + Metrics.Space.Sm * scale;
            for (var index = 0; index < heatOrder.Count; index++)
            {
                cursor = DrawRankRow(list, left, cursor, width, heatOrder[index], scale);
            }

            cursor += Metrics.Space.Lg * scale;
        }

        var tap = Metrics.Size.TapTarget * scale;
        var replay = Loc.T(L.Prism.ReplayReveal);
        if (finalOrder.Count > 0)
        {
            if (DrawButton(list, PlainRect(new Vector2(left, cursor + tap * 0.5f), replay, width, 0f, scale), replay,
                    ButtonStyle.Plain, scale))
            {
                StartReveal();
            }

            cursor += tap;
        }

        var winners = Loc.T(L.Prism.WhatWinnersGet);
        if (DrawButton(list, PlainRect(new Vector2(left, cursor + tap * 0.5f), winners, width, 0f, scale), winners,
                ButtonStyle.Plain, scale))
        {
            OpenRewards();
        }

        cursor += tap + PrismArt.BottomMargin(scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
    }

    private void DrawNoPodium(ImDrawListPtr drawList, Rect full, Rect content, PrismResultsDto results, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        PrismLight.Week(drawList,
            new Rect(full.Min, new Vector2(full.Max.X, content.Min.Y + tap + Metrics.Space.Sm * scale)),
            results.WeekId, PrismArt.Page, PrismArt.Fore, 0f, 0f);
        var top = DrawBackHeader(drawList, content, results.Title, scale);
        var gallery = results.EntryCount > 0;
        var links = (gallery ? 2f : 1f) * tap + PrismArt.BottomMargin(scale);
        var area = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, content.Max.Y - bottomChrome - links));
        var bottom = DrawEmpty(drawList, area, PodiumGlyph, Loc.T(L.Prism.NoPodiumTitle), Loc.T(L.Prism.NoPodium),
            ContestAction(store.Week), ButtonStyle.Solid, scale, out var acted) + Metrics.Space.Sm * scale;
        if (acted)
        {
            OpenTab(PrismTab.Week);
        }

        if (gallery && CenteredLink(drawList, content, ref bottom, Loc.T(L.Prism.AllGlams), scale))
        {
            OpenGallery(results.WeekId, store.Track, results.Title);
        }

        if (CenteredLink(drawList, content, ref bottom, Loc.T(L.Prism.WhatWinnersGet), scale))
        {
            OpenRewards();
        }
    }

    private bool CenteredLink(ImDrawListPtr drawList, Rect content, ref float top, string label, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var rect = PlainRect(new Vector2(content.Center.X, top + tap * 0.5f), label, content.Width, 0.5f, scale);
        top += tap;
        return DrawButton(drawList, rect, label, ButtonStyle.Plain, scale);
    }

    private static string ContestAction(PrismWeekDto? week) => week is not { Id.Length: > 0 } ? string.Empty
        : week.Phase switch
        {
            PrismPhases.Entering when week.MyEntry is null => Loc.T(L.Prism.EnterThisWeek),
            PrismPhases.Entering => Loc.T(L.Prism.SeeYourGlam),
            PrismPhases.Judging => Loc.T(L.Prism.JudgeHeat),
            PrismPhases.Final => Loc.T(L.Prism.JudgeFinal),
            _ => string.Empty,
        };

    private float DrawPodium(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (PodiumSize - 1)) / PodiumSize;
        var photoHeight = column / PrismStore.EntryRatio;
        var nameLine = Typography.LineHeight(PrismType.Headline);
        var numeralPixels = PodiumNumeralUnits * scale;
        var count = Math.Min(PodiumSize, finalOrder.Count);
        var age = (float)(ImGui.GetTime() - podiumLitAt);
        for (var index = 0; index < count; index++)
        {
            var placing = finalOrder[index];
            var x = left + index * (column + gap);
            var photo = new Rect(new Vector2(x, top), new Vector2(x + column, top + photoHeight));
            var mark = PrismMotion.Mark(drawList);
            DrawLook(drawList, photo, placing.ImageUrl, false);
            TapForOutfit(photo.Min, photo.Max, placing);
            var shade = new Vector2(photo.Min.X, photo.Max.Y - photoHeight * PodiumShadeShare);
            drawList.AddRectFilledMultiColor(shade, photo.Max, ImGui.GetColorU32(PodiumClear),
                ImGui.GetColorU32(PodiumClear), ImGui.GetColorU32(PodiumShade), ImGui.GetColorU32(PodiumShade));
            PrismType.Draw(drawList, new Vector2(photo.Min.X + Metrics.Space.Sm * scale, photo.Max.Y - numeralPixels),
                Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)], PrismArt.PhotoFore, PrismFace.Numerals,
                numeralPixels);
            var creditTop = photo.Max.Y + Metrics.Space.Sm * scale;
            var names = new Rect(new Vector2(x, photo.Max.Y), new Vector2(x + column,
                creditTop + nameLine + Typography.LineHeight(PrismType.Caption) + Metrics.Space.Sm * scale));
            TapForProfile(drawList, names, placing, scale);
            Typography.Draw(drawList, new Vector2(x, creditTop),
                Typography.FitText(placing.DisplayName, column, PrismType.Headline), NameInk(placing),
                PrismType.Headline);
            Typography.Draw(drawList, new Vector2(x, creditTop + nameLine),
                Typography.FitText(PlacingDetail(placing), column, PrismType.Caption), PrismArt.Muted,
                PrismType.Caption);
            var lit = PrismMotion.At(age, PodiumLightDelay + PodiumLightStagger * (count - 1 - index), Motion.Sheet);
            if (lit < PrismMotion.Settled)
            {
                PrismMotion.Shift(drawList, mark, Vector2.Zero, PodiumDimFloor + (1f - PodiumDimFloor) * lit);
            }

            var rim = PrismMotion.At(age, PodiumRimDelay, Motion.Sheet);
            if (placing.Rank == 1)
            {
                PrismLight.Edge(drawList, photo, 0f, PrismGlow.Spectrum, PrismLight.EdgeAlpha * rim, scale);
            }
            else if (placing.Rank is > 1 and <= PodiumSize)
            {
                PrismLight.Edge(drawList, photo, 0f, PrismGlow.Pearl, PlacedPearlAlpha * rim, scale);
            }
        }

        return top + photoHeight + Metrics.Space.Sm * scale + nameLine + Typography.LineHeight(PrismType.Caption) +
               Metrics.Space.Lg * scale;
    }

    private float DrawPlacingRow(ImDrawListPtr drawList, float left, float right, float top, PrismResultsDto results,
        float scale)
    {
        top = DrawSectionTitle(drawList, left, right, top, Loc.T(L.Prism.YourPlacing), CoinFact(results), scale) +
              Metrics.Space.Sm * scale;
        var thumbWidth = CardThumbUnits * scale;
        var thumb = new Rect(new Vector2(left, top),
            new Vector2(left + thumbWidth, top + thumbWidth / PrismStore.EntryRatio));
        DrawLook(drawList, thumb, results.MyImageUrl, false);
        var textLeft = thumb.Max.X + Metrics.Space.Md * scale;
        var placing = PlacingLine(results);
        Typography.Draw(drawList, new Vector2(textLeft, top), placing, PrismArt.Fore, PrismType.Title);
        var factsHeight = DrawFacts(drawList, new Vector2(textLeft, top + Typography.LineHeight(PrismType.Title)),
            right - textLeft, results, PrismArt.Muted, scale);
        return MathF.Max(thumb.Max.Y, top + Typography.LineHeight(PrismType.Title) + factsHeight);
    }

    private float DrawFacts(ImDrawListPtr drawList, Vector2 topLeft, float width, PrismResultsDto results,
        Vector4 color, float scale)
    {
        var cursor = topLeft;
        DrawFact(drawList, HeatFact(results), topLeft.X, width, color, ref cursor, scale);
        DrawFact(drawList, Fact(ref rankFact, L.Prism.FinalRank, results.MyFinalRank), topLeft.X, width, color,
            ref cursor, scale);
        DrawFact(drawList, Fact(ref topFact, L.Prism.TopPercent, results.Percentile), topLeft.X, width, color,
            ref cursor, scale);
        return cursor.Y + Typography.LineHeight(PrismType.Caption) - topLeft.Y;
    }

    private static void DrawFact(ImDrawListPtr drawList, string text, float left, float width, Vector4 color,
        ref Vector2 cursor, float scale)
    {
        if (text.Length == 0)
        {
            return;
        }

        var fitted = Typography.FitText(text, width, PrismType.Caption);
        var textWidth = Typography.Measure(fitted, PrismType.Caption).X;
        if (cursor.X > left && cursor.X + textWidth > left + width)
        {
            cursor = new Vector2(left, cursor.Y + Typography.LineHeight(PrismType.Caption));
        }

        Typography.Draw(drawList, cursor, fitted, color, PrismType.Caption);
        cursor.X += textWidth + Metrics.Space.Lg * scale;
    }

    private float DrawRankRow(ImDrawListPtr drawList, float left, float top, float width, PrismPlacingDto placing,
        float scale)
    {
        var thumbWidth = RowThumbUnits * scale;
        var thumbHeight = thumbWidth / PrismStore.EntryRatio;
        var pad = Metrics.Space.Sm * scale;
        var bottom = top + thumbHeight + pad * 2f;
        var middle = (top + bottom) * 0.5f;
        var rank = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
        Typography.Draw(drawList, new Vector2(left, middle - Typography.LineHeight(PrismType.Headline) * 0.5f), rank,
            PrismArt.Fore, PrismType.Headline);
        var thumbLeft = left + RankColumnUnits * scale;
        DrawLook(drawList,
            new Rect(new Vector2(thumbLeft, top + pad), new Vector2(thumbLeft + thumbWidth, bottom - pad)),
            placing.ImageUrl, false);
        var textLeft = thumbLeft + thumbWidth + Metrics.Space.Md * scale;
        var outfit = new Rect(new Vector2(left, top), new Vector2(thumbLeft + thumbWidth, bottom));
        if (HasOutfit(placing.Outfit) || placing.GlamId.Length > 0)
        {
            HoverWash(drawList, outfit, outfit, Metrics.Radius.Sm * scale);
        }

        TapForOutfit(outfit.Min, outfit.Max, placing);
        TapForProfile(drawList, new Rect(new Vector2(outfit.Max.X, top), new Vector2(left + width, bottom)), placing,
            scale);
        var room = MathF.Max(1f, left + width - textLeft);
        var nameLine = Typography.LineHeight(PrismType.Headline);
        var textTop = middle - (nameLine + Typography.LineHeight(PrismType.Caption)) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(placing.DisplayName, room, PrismType.Headline), NameInk(placing), PrismType.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameLine),
            Typography.FitText(PlacingDetail(placing), room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        return bottom;
    }

    private void TapForProfile(ImDrawListPtr drawList, Rect hit, PrismPlacingDto placing, float scale)
    {
        if (placing.GlamId.Length == 0 || profileGlamId.Length > 0)
        {
            return;
        }

        if (UiInteract.Click(hit.Min, hit.Max, HoverWash(drawList, hit, hit, Metrics.Radius.Sm * scale)))
        {
            profileGlamId = placing.GlamId;
            glams.Open(new GlamDto(placing.GlamId));
        }
    }

    private Vector4 NameInk(PrismPlacingDto placing) =>
        profileGlamId.Length > 0 && profileGlamId == placing.GlamId ? PrismArt.Muted : PrismArt.Fore;

    private void FollowPlacingProfile()
    {
        if (profileGlamId.Length == 0 || glams.DetailPending)
        {
            return;
        }

        var author = glams.Detail is { } glam && glam.Id == profileGlamId ? glam.Author : null;
        profileGlamId = string.Empty;
        if (author is { Id.Length: > 0 })
        {
            OpenProfile(author);
        }
    }

    private static Rect PrintFrame(Vector2 origin, float width, float scale)
    {
        var pad = PrintPadUnits * scale;
        var photo = (width - pad * 2f) / PrismStore.EntryRatio;
        var height = pad * 3f + photo + PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) +
                     Typography.LineHeight(PrismType.Caption) * 2f + PrintNumeralUnits * scale;
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
        var top = photo.Max.Y + pad;
        var coinsWidth = PrismType.DrawRight(drawList, new Vector2(photo.Max.X, top), CoinFact(results), PrismArt.Ink,
            PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(photo.Min.X, top),
            PrismType.FitLabel(results.Title, labelPixels, inner - coinsWidth - Metrics.Space.Sm * scale),
            PrismArt.InkMuted, PrismFace.Label, labelPixels);
        top += PrismType.LineHeight(PrismFace.Label, labelPixels);
        var pixels = PrintNumeralUnits * scale;
        PrismType.Draw(drawList, new Vector2(photo.Min.X, top), PlacingLine(results), PrismArt.Ink,
            PrismFace.Numerals, pixels);
        DrawFacts(drawList, new Vector2(photo.Min.X, top + pixels), inner, results, PrismArt.InkMuted, scale);
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

    private string RevealTitle(PrismResultsDto results, bool summary)
    {
        if (summary && results.MyHeatRank <= 0)
        {
            return string.Empty;
        }

        var label = Loc.T(summary ? L.Prism.YourPlacing : L.Prism.FinalTitle);
        return results.Title.Length == 0 ? label : FactLine(ref revealTitle, label, results.Title);
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

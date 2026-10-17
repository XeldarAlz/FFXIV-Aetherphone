using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float RevealNumeralUnits = 200f;
    private const float ListNumeralUnits = 88f;
    private const float PrintNumeralUnits = 64f;
    private const float PrintWidthShare = 0.78f;
    private const float PrintPadUnits = 12f;
    private const float RefractionDone = 0.995f;

    private readonly List<PrismPlacingDto> finalOrder = new();
    private readonly List<PrismPlacingDto> heatOrder = new();

    private PrismResultsDto? orderedFor;
    private int revealIndex;
    private Spring refraction = new(1f);
    private CachedText placingText;
    private CachedText printLine;
    private CachedText printDetail;
    private CachedText nextText;

    private int RevealPageCount(PrismResultsDto results) => finalOrder.Count + (results.MyHeatRank > 0 ? 1 : 0);

    private void StartReveal()
    {
        revealIndex = 0;
        refraction.SnapTo(1f);
        Go(PrismScreen.Reveal);
    }

    private void DrawReveal(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (store.Results is not { } results)
        {
            Go(PrismScreen.Cover);
            return;
        }

        SyncOrder(results);
        var pages = RevealPageCount(results);
        if (revealIndex >= pages)
        {
            FinishReveal(results);
            return;
        }

        var placingPage = revealIndex < finalOrder.Count;
        if (placingPage)
        {
            DrawRevealPlacing(drawList, full, content, results, finalOrder[finalOrder.Count - 1 - revealIndex], scale);
        }
        else
        {
            DrawHeader(drawList, content, PrismType.Upper(Loc.T(L.Prism.YourPlacing)), IssueLine(results.Issue), false,
                scale);
            DrawPrint(drawList, PrintRect(content, scale), results, scale);
        }

        var next = NextLine(revealIndex + 1, pages);
        var mono = PrismArt.MonoPixels();
        PrismType.DrawRight(drawList, new Vector2(content.Max.X - PrismArt.Gutter(scale),
            content.Max.Y - PrismArt.Gutter(scale) - mono), next, PrismArt.Muted, EditorialFace.Mono, mono);
        if (UiInteract.HoverClick(content.Min, content.Max))
        {
            revealIndex++;
            screenFade.SnapTo(0f);
            var nextIsWinner = revealIndex < finalOrder.Count && finalOrder[finalOrder.Count - 1 - revealIndex].Rank == 1;
            refraction.SnapTo(nextIsWinner ? 0f : 1f);
        }
    }

    private void DrawRevealPlacing(ImDrawListPtr drawList, Rect full, Rect content, PrismResultsDto results,
        PrismPlacingDto placing, float scale)
    {
        DrawLook(drawList, full, placing.ImageUrl, false);
        PrismArt.Shade(drawList, full, scale);
        PrismArt.Refraction(drawList, full, refraction.Value >= RefractionDone ? 1f
            : refraction.Step(1f, Motion.Sheet, ImGui.GetIO().DeltaTime));
        DrawHeader(drawList, content, PrismType.Upper(Loc.T(L.Prism.FinalTitle)), IssueLine(results.Issue), false,
            scale);
        var gutter = PrismArt.Gutter(scale);
        var mono = PrismArt.MonoPixels();
        var monoLine = PrismType.LineHeight(EditorialFace.Mono, mono);
        var creditTop = content.Max.Y - gutter - mono - Metrics.Space.Lg * scale - monoLine;
        PrismArt.Hairline(drawList, content.Min.X + gutter, content.Max.X - gutter, creditTop - Metrics.Space.Sm * scale,
            PrismArt.White, scale);
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, creditTop), PrismType.Upper(placing.DisplayName),
            PrismArt.White, EditorialFace.Mono, mono);
        PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, creditTop), PrismType.Upper(placing.World),
            PrismArt.White, EditorialFace.Mono, mono);
        var pixels = RevealNumeralUnits * scale;
        var numeral = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, creditTop - Metrics.Space.Lg * scale - pixels),
            numeral, PrismArt.White, EditorialFace.Numerals, pixels);
    }

    private void FinishReveal(PrismResultsDto results)
    {
        configuration.PrismRevealedWeek = results.WeekId;
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
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = origin.Y;
        cursor = DrawSectionTitle(list, left, cursor, width, QuotedTitle(results.Title), scale);
        for (var index = 0; index < finalOrder.Count; index++)
        {
            cursor = DrawListedLook(list, left, cursor, width, finalOrder[index], scale);
        }

        if (results.MyHeatRank > 0)
        {
            var printWidth = width * PrintWidthShare;
            var print = PrintFrame(new Vector2(left + (width - printWidth) * 0.5f, cursor), printWidth, scale);
            DrawPrint(list, print, results, scale);
            cursor = print.Max.Y + Metrics.Space.Xl * scale;
        }

        if (heatOrder.Count > 0)
        {
            cursor = DrawSectionTitle(list, left, cursor, width, PrismType.Upper(Loc.T(L.Prism.YourHeat)), scale);
            var half = (width - Metrics.Space.Sm * scale) * 0.5f;
            for (var index = 0; index < heatOrder.Count; index++)
            {
                var column = index % 2;
                var x = left + column * (half + Metrics.Space.Sm * scale);
                var bottom = DrawListedLook(list, x, cursor, half, heatOrder[index], scale);
                if (column == 1 || index == heatOrder.Count - 1)
                {
                    cursor = bottom;
                }
            }
        }

        if (finalOrder.Count > 0 && PrismArt.Link(list, new Vector2(left, cursor),
                PrismType.Upper(Loc.T(L.Prism.ReplayReveal)), PrismArt.Muted, scale))
        {
            StartReveal();
        }

        cursor += Metrics.Size.TapTarget * scale + gutter;
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
    }

    private static float DrawSectionTitle(ImDrawListPtr drawList, float left, float top, float width, string title,
        float scale)
    {
        var pixels = PrismType.Fit(title, EditorialFace.Headline, width, NoticeMaxUnits * scale, NoticeMinUnits * scale,
            NoticeLines, out var lines);
        return top + PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.White,
            EditorialFace.Headline, pixels) + Metrics.Space.Lg * scale;
    }

    private float DrawListedLook(ImDrawListPtr drawList, float left, float top, float width, PrismPlacingDto placing,
        float scale)
    {
        var photo = new Rect(new Vector2(left, top), new Vector2(left + width, top + width / PrismStore.EntryRatio));
        DrawLook(drawList, photo, placing.ImageUrl, false);
        var pixels = MathF.Min(ListNumeralUnits * scale, width * 0.5f);
        var numeral = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
        PrismType.Draw(drawList, new Vector2(left + Metrics.Space.Sm * scale, photo.Max.Y - pixels * 0.9f), numeral,
            PrismArt.White, EditorialFace.Numerals, pixels);
        var mono = PrismArt.MonoPixels();
        var creditTop = photo.Max.Y + Metrics.Space.Sm * scale;
        PrismType.Draw(drawList, new Vector2(left, creditTop), PrismType.Upper(placing.DisplayName), PrismArt.White,
            EditorialFace.Mono, mono);
        PrismType.Draw(drawList, new Vector2(left, creditTop + PrismType.LineHeight(EditorialFace.Mono, mono)),
            PrismType.Upper(placing.World), PrismArt.Muted, EditorialFace.Mono, mono);
        return creditTop + PrismType.LineHeight(EditorialFace.Mono, mono) * 2f + Metrics.Space.Xl * scale;
    }

    private Rect PrintRect(Rect content, float scale)
    {
        var width = content.Width * PrintWidthShare;
        var frame = PrintFrame(Vector2.Zero, width, scale);
        var origin = new Vector2(content.Center.X - width * 0.5f, content.Center.Y - frame.Height * 0.5f);
        return new Rect(origin, origin + frame.Max);
    }

    private static Rect PrintFrame(Vector2 origin, float width, float scale)
    {
        var pad = PrintPadUnits * scale;
        var photo = (width - pad * 2f) / PrismStore.EntryRatio;
        var mono = PrismType.LineHeight(EditorialFace.Mono, PrismArt.MonoPixels());
        var height = pad * 3f + photo + mono * 3f + PrintNumeralUnits * scale;
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
        var mono = PrismArt.MonoPixels();
        var monoLine = PrismType.LineHeight(EditorialFace.Mono, mono);
        var top = photo.Max.Y + pad;
        PrismType.Draw(drawList, new Vector2(photo.Min.X, top), PrintHeading(results), PrismArt.InkMuted,
            EditorialFace.Mono, mono);
        top += monoLine;
        var pixels = PrintNumeralUnits * scale;
        PrismType.Draw(drawList, new Vector2(photo.Min.X, top), PlacingLine(results), PrismArt.Ink,
            EditorialFace.Numerals, pixels);
        top += pixels;
        var detail = PrismType.Wrap(PrintDetail(results), EditorialFace.Mono, mono, inner);
        PrismType.DrawLines(drawList, new Vector2(photo.Min.X, top), detail, PrismArt.InkMuted, EditorialFace.Mono,
            mono);
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

        var issue = IssueLine(results.Issue);
        var heading = PrismType.Upper(DisplayName) + (issue.Length > 0 ? " " + issue : string.Empty);
        return printLine.Store(key, heading);
    }

    private string PrintDetail(PrismResultsDto results)
    {
        var key = HashCode.Combine(results.WeekId, results.MyFinalRank, results.CoinsAwarded, Loc.Current.Code);
        if (printDetail.IsCurrent(key))
        {
            return printDetail.Value;
        }

        var heat = results.HeatLabel.Length > 0 ? Loc.T(L.Prism.HeatName, results.HeatLabel) : Loc.T(L.Prism.YourHeat);
        var parts = heat;
        if (results.MyFinalRank > 0)
        {
            parts += " · " + Loc.T(L.Prism.FinalRank, results.MyFinalRank);
        }

        if (results.Percentile > 0)
        {
            parts += " · " + Loc.T(L.Prism.TopPercent, results.Percentile);
        }

        if (results.CoinsAwarded > 0)
        {
            parts += " · " + Loc.T(L.Prism.CoinsWon, results.CoinsAwarded);
        }

        return printDetail.Store(key, PrismType.Upper(parts));
    }

    private string NextLine(int page, int pages)
    {
        var key = HashCode.Combine(page, pages, Loc.Current.Code);
        return nextText.IsCurrent(key)
            ? nextText.Value
            : nextText.Store(key, PrismType.Upper(Loc.T(L.Common.Next)) + " " + page + "/" + pages + " →");
    }
}

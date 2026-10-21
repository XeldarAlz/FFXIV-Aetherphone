using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed record NpcSpot(string Name, uint TerritoryId, uint MapId, int RawX, int RawY);

internal sealed partial class PrismApp
{
    // The Fashion Report NPC, matched by her English name in the game's ENpcResident sheet.
    private const string MaskedRose = "Masked Rose";

    private static readonly LocString[] SlotNames =
    {
        L.Inventory.SlotMainHand, L.Inventory.SlotOffHand, L.Inventory.SlotHead, L.Inventory.SlotBody,
        L.Inventory.SlotHands, L.Inventory.SlotWaist, L.Inventory.SlotLegs, L.Inventory.SlotFeet,
        L.Inventory.SlotEars, L.Inventory.SlotNeck, L.Inventory.SlotWrists, L.Inventory.SlotRings,
        L.Inventory.SlotRings,
    };

    private FashionReportDto? fashionFor;
    private GlamGearDto[][] fashionSets = Array.Empty<GlamGearDto[]>();
    private string[] fashionKeys = Array.Empty<string>();
    private GlamGearDto[][] fashionHintGear = Array.Empty<GlamGearDto[]>();
    private GlamGearDto?[][] fashionPieceGear = Array.Empty<GlamGearDto?[]>();
    private bool roseSearched;
    private int hintsMet = -1;
    private int dyesMet;
    private int emptySlots;
    private CachedText scoreText;
    private CachedText emptyText;
    private volatile NpcSpot? roseSpot;
    private volatile bool roseLooking;
    private CachedText fashionWeekText;
    private CachedText roseText;
    private CachedText sourceText;
    private CachedText[] setTexts = Array.Empty<CachedText>();

    private void OpenFashionReport()
    {
        Push(PrismScreen.FashionReport);
        glams.RefreshFashion(Loc.Current.Code);
        FindMaskedRose();
    }

    private void FindMaskedRose()
    {
        if (roseSearched || roseLooking)
        {
            return;
        }

        roseSearched = true;
        roseLooking = true;
        _ = Task.Run(() =>
        {
            try
            {
                if (gameData.TryFindNpc(MaskedRose, out var name, out var territoryId, out var mapId, out var rawX,
                        out var rawY))
                {
                    roseSpot = new NpcSpot(name, territoryId, mapId, rawX, rawY);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Prism] could not find the Masked Rose");
            }
            finally
            {
                roseLooking = false;
            }
        });
    }

    private void RefreshFashionOwned(FashionReportDto report)
    {
        var storage = OwnedItems.StorageLoaded;
        if (ReferenceEquals(fashionFor, report) && statsStorage == storage)
        {
            return;
        }

        fashionFor = report;
        statsStorage = storage;
        hintsMet = -1;
        statsFor = null;
        ownedIds.Clear();
        var hints = report.Hints ?? Array.Empty<FashionHintDto>();
        fashionHintGear = new GlamGearDto[hints.Length][];
        for (var index = 0; index < hints.Length; index++)
        {
            var items = hints[index].Items ?? Array.Empty<uint>();
            fashionHintGear[index] = new GlamGearDto[items.Length];
            for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                fashionHintGear[index][itemIndex] = new GlamGearDto(hints[index].Slot, items[itemIndex]);
                if (OwnedItems.Owns(items[itemIndex]))
                {
                    ownedIds.Add(items[itemIndex]);
                }
            }
        }

        var sets = report.Sets ?? Array.Empty<FashionSetDto>();
        fashionSets = new GlamGearDto[sets.Length][];
        fashionKeys = new string[sets.Length];
        fashionPieceGear = new GlamGearDto?[sets.Length][];
        setTexts = new CachedText[sets.Length];
        for (var index = 0; index < sets.Length; index++)
        {
            var pieces = sets[index].Pieces ?? Array.Empty<FashionPieceDto>();
            var gear = new List<GlamGearDto>(pieces.Length);
            fashionPieceGear[index] = new GlamGearDto?[pieces.Length];
            for (var pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
            {
                var piece = pieces[pieceIndex];
                if (piece.ItemId == 0)
                {
                    continue;
                }

                var worn = new GlamGearDto(piece.Slot, piece.ItemId, piece.StainId);
                fashionPieceGear[index][pieceIndex] = worn;
                gear.Add(worn);
                if (OwnedItems.Owns(piece.ItemId))
                {
                    ownedIds.Add(piece.ItemId);
                }
            }

            fashionSets[index] = gear.ToArray();
            fashionKeys[index] = string.Concat("fashion:", report.Week.ToString(Loc.Culture), ":",
                index.ToString(Loc.Culture));
        }
    }

    private void DrawFashionReport(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var report = glams.Fashion;
        if (report is not null)
        {
            RefreshFashionOwned(report);
        }

        var gutter = PrismArt.Gutter(scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var cursor = origin.Y;
        PrismType.Draw(list, new Vector2(left, cursor), FashionWeekLine(report?.Week ?? 0), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        cursor += labelLine + Metrics.Space.Sm * scale;
        if (report is null)
        {
            var failed = glams.FashionFailed && !glams.FashionLoading;
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor),
                Loc.T(glams.FashionLoading ? L.Common.Loading : failed ? L.Common.LoadFailedHint : L.Prism.FashionMissing),
                PrismArt.Muted, TextStyles.Subheadline, width) + Metrics.Space.Md * scale;
            if (failed && PrismArt.Link(list, new Vector2(left, cursor), Loc.T(L.Common.Retry), PrismArt.Fore, scale))
            {
                glams.RefreshFashion(Loc.Current.Code);
            }

            ImGui.Dummy(new Vector2(width, cursor - origin.Y + Metrics.Size.TapTarget * scale));
            return;
        }

        cursor = DrawSectionTitle(list, left, cursor, width, report.Theme, scale);
        cursor = DrawRoseLink(list, left, cursor, scale);
        cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.FillEverySlot), PrismArt.Muted,
            TextStyles.Footnote, width) + Metrics.Space.Sm * scale;
        if (!OwnedItems.StorageLoaded)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.OpenStorage), PrismArt.Muted,
                TextStyles.Footnote, width) + Metrics.Space.Sm * scale;
        }

        cursor = DrawWearingCheck(list, left, cursor, width, report, scale) + Metrics.Space.Xl * scale;
        var section = 0;
        cursor = DrawFashionSets(list, left, cursor, width, report, ref section, scale);
        cursor = DrawFashionHints(list, left, cursor, width, report, ref section, scale);
        cursor = DrawFashionDyes(list, left, cursor, width, report, section, scale);
        if (report.Source.Length > 0)
        {
            var key = HashCode.Combine(report.Source, Loc.Current.Code);
            var credit = sourceText.IsCurrent(key)
                ? sourceText.Value
                : sourceText.Store(key, Loc.T(L.Prism.SolutionsFrom, report.Source));
            cursor += Metrics.Space.Lg * scale;
            if (report.SourceUrl.Length > 0 && PrismArt.Link(list, new Vector2(left, cursor),
                    PrismType.FitLabel(credit, labelPixels, width), PrismArt.Muted, scale))
            {
                UrlActions.AskThenOpen(report.SourceUrl);
            }
            else if (report.SourceUrl.Length == 0)
            {
                PrismType.Draw(list, new Vector2(left, cursor), PrismType.FitLabel(credit, labelPixels, width),
                    PrismArt.Muted, PrismFace.Label, labelPixels);
            }

            cursor += labelLine;
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private string FashionWeekLine(int week)
    {
        var key = HashCode.Combine(week, Loc.Current.Code);
        return fashionWeekText.IsCurrent(key)
            ? fashionWeekText.Value
            : fashionWeekText.Store(key, week > 0
                ? Loc.T(L.Prism.FashionWeek, Loc.T(L.Prism.FashionReport), week)
                : Loc.T(L.Prism.FashionReport));
    }

    private float DrawRoseLink(ImDrawListPtr drawList, float left, float top, float scale)
    {
        if (roseSpot is not { } spot)
        {
            return top;
        }

        var key = HashCode.Combine(spot.Name, Loc.Current.Code);
        var label = roseText.IsCurrent(key) ? roseText.Value : roseText.Store(key, Loc.T(L.Prism.FindOnMap, spot.Name));
        if (PrismArt.Link(drawList, new Vector2(left, top), label, PrismArt.Fore, scale))
        {
            GameLinkActions.OpenMap(spot.TerritoryId, spot.MapId, spot.RawX, spot.RawY);
        }

        return top + PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) + Metrics.Space.Lg * scale;
    }

    private float DrawFashionSets(ImDrawListPtr drawList, float left, float top, float width, FashionReportDto report,
        ref int section, float scale)
    {
        var sets = report.Sets ?? Array.Empty<FashionSetDto>();
        for (var index = 0; index < sets.Length && index < fashionSets.Length; index++)
        {
            var set = sets[index];
            var key = HashCode.Combine(set.Points, Loc.Current.Code);
            var title = setTexts[index].IsCurrent(key)
                ? setTexts[index].Value
                : setTexts[index].Store(key, Loc.T(L.Prism.EasySet, set.Points));
            top = DrawSectionLabel(drawList, left, top, width, section++, title, scale);
            var pieces = set.Pieces ?? Array.Empty<FashionPieceDto>();
            var worn = fashionPieceGear[index];
            for (var pieceIndex = 0; pieceIndex < pieces.Length && pieceIndex < worn.Length; pieceIndex++)
            {
                top = worn[pieceIndex] is { } piece
                    ? DrawGearRow(drawList, left, top, width, piece, true, scale)
                    : DrawAnyDyeableRow(drawList, left, top, width, pieces[pieceIndex], scale);
            }

            var gear = fashionSets[index];
            if (gear.Length > 0)
            {
                var strip = new Rect(new Vector2(left, top), new Vector2(left + width, top + PrismArt.StripUnits * scale));
                var wearKey = fashionKeys[index];
                var wearing = Wearing(wearKey);
                if (PrismArt.Strip(drawList, strip, Loc.T(wearing ? L.Prism.TakeOff : L.Prism.TryOnSet), string.Empty,
                        true))
                {
                    if (wearing)
                    {
                        glamour.Revert();
                    }
                    else
                    {
                        glamour.TryOn(gear);
                        StartWearing(wearKey, report.Theme);
                    }
                }

                top = DrawSetWardrobeLink(drawList, left, strip.Max.Y + Metrics.Space.Sm * scale, gear, wearKey,
                    report.Week, scale);
            }

            top += Metrics.Space.Xl * scale;
        }

        return top;
    }

    private float DrawSetWardrobeLink(ImDrawListPtr drawList, float left, float top, GlamGearDto[] gear,
        string wearKey, int week, float scale)
    {
        wardrobe.Ensure();
        if (wardrobe.Loading)
        {
            return top;
        }

        var saved = wardrobe.FindByGlam(wearKey);
        if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(saved is null ? L.Prism.AddToWardrobe : L.Prism.InWardrobe),
                PrismArt.Muted, scale, PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) + Metrics.Space.Sm * scale))
        {
            if (saved is null)
            {
                wardrobe.Add(new WardrobeOutfit(Guid.NewGuid().ToString("N"), FashionWeekLine(week), string.Empty,
                    Guid.Empty, gear, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), wearKey));
            }
            else
            {
                Push(PrismScreen.WardrobeOutfit);
                outfitId = saved.Id;
                renaming = false;
            }
        }

        return top + PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
    }

    private float DrawWearingCheck(ImDrawListPtr drawList, float left, float top, float width, FashionReportDto report,
        float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Prism.CheckWearing), PrismArt.Fore, scale))
        {
            CheckWearing(report);
        }

        top += labelLine + Metrics.Space.Sm * scale;
        if (hintsMet < 0)
        {
            return top;
        }

        var hints = report.Hints?.Length ?? 0;
        var dyes = report.Dyes?.Length ?? 0;
        var key = HashCode.Combine(hintsMet, hints, dyesMet, dyes, Loc.Current.Code);
        var score = scoreText.IsCurrent(key)
            ? scoreText.Value
            : scoreText.Store(key, Loc.T(L.Prism.WearingScore, hintsMet, hints, dyesMet, dyes));
        PrismType.Draw(drawList, new Vector2(left, top), PrismType.FitLabel(score, labelPixels, width), PrismArt.Fore,
            PrismFace.Label, labelPixels);
        top += labelLine;
        if (emptySlots != 0)
        {
            var emptyKey = HashCode.Combine(emptySlots, Loc.Current.Code);
            var empty = emptyText.IsCurrent(emptyKey) ? emptyText.Value : emptyText.Store(emptyKey, EmptyLine(emptySlots));
            top += Typography.DrawWrappedLeft(new Vector2(left, top), empty, PrismArt.Muted, TextStyles.Subheadline,
                width);
        }

        return top + Typography.DrawWrappedLeft(new Vector2(left, top + Metrics.Space.Xs * scale),
            Loc.T(L.Prism.JudgedOnGlamour), PrismArt.Muted, TextStyles.Footnote, width);
    }

    private void CheckWearing(FashionReportDto report)
    {
        var worn = EquippedOutfit.Read();
        hintsMet = 0;
        dyesMet = 0;
        var hints = report.Hints ?? Array.Empty<FashionHintDto>();
        for (var index = 0; index < hints.Length; index++)
        {
            var items = hints[index].Items ?? Array.Empty<uint>();
            if (Matches(worn, hints[index].Slot, items) ||
                IsRing(hints[index].Slot) && (Matches(worn, (int)GearSlot.RingRight, items) ||
                                              Matches(worn, (int)GearSlot.RingLeft, items)))
            {
                hintsMet++;
            }
        }

        var dyes = report.Dyes ?? Array.Empty<FashionDyeDto>();
        for (var index = 0; index < dyes.Length; index++)
        {
            var shade = gameData.StainShade(dyes[index].StainId);
            if (shade != 0 && Worn(worn, dyes[index].Slot) is { } piece && gameData.StainShade(piece.Stain0) == shade)
            {
                dyesMet++;
            }
        }

        emptySlots = 0;
        for (var index = 0; index < GearSlots.All.Length; index++)
        {
            var slot = (int)GearSlots.All[index];
            if (slot != (int)GearSlot.OffHand && Worn(worn, slot) is null)
            {
                emptySlots |= 1 << slot;
            }
        }
    }

    private static string EmptyLine(int slots)
    {
        var names = new List<string>();
        for (var slot = 0; slot < SlotNames.Length; slot++)
        {
            var name = Loc.T(SlotNames[slot]);
            if ((slots & 1 << slot) != 0 && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return Loc.T(L.Prism.StillEmpty, string.Join(", ", names));
    }

    private static bool IsRing(int slot) => slot is (int)GearSlot.RingRight or (int)GearSlot.RingLeft;

    private static bool Matches(GlamGearDto[] worn, int slot, uint[] items) =>
        Worn(worn, slot) is { } piece && Array.IndexOf(items, piece.ItemId) >= 0;

    private static GlamGearDto? Worn(GlamGearDto[] worn, int slot)
    {
        for (var index = 0; index < worn.Length; index++)
        {
            if (worn[index].Slot == slot && worn[index].ItemId != 0)
            {
                return worn[index];
            }
        }

        return null;
    }

    private float DrawAnyDyeableRow(ImDrawListPtr drawList, float left, float top, float width, FashionPieceDto piece,
        float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = GearIconUnits * scale;
        drawList.AddRect(new Vector2(left, top), new Vector2(left + icon, top + icon), ImGui.GetColorU32(PrismArt.Faint),
            0f, ImDrawFlags.None, 1f);
        var textLeft = left + icon + Metrics.Space.Md * scale;
        var room = width - (textLeft - left);
        PrismType.Draw(drawList, new Vector2(textLeft, top),
            PrismType.FitLabel(Loc.T(SlotNames[Math.Clamp(piece.Slot, 0, SlotNames.Length - 1)]), labelPixels, room),
            PrismArt.Fore, PrismFace.Label, labelPixels);
        var line = piece.StainId > 0 ? DyeLine(piece.StainId, 0) : Loc.T(L.Prism.AnyDyeable);
        PrismType.Draw(drawList, new Vector2(textLeft, top + labelLine),
            PrismType.FitLabel(line, labelPixels, room), PrismArt.Muted, PrismFace.Label, labelPixels);
        var bottom = top + MathF.Max(icon, labelLine * 2f) + Metrics.Space.Sm * scale;
        PrismArt.Hairline(drawList, left, left + width, bottom, PrismArt.Faint, scale);
        return bottom + Metrics.Space.Md * scale;
    }

    private float DrawFashionHints(ImDrawListPtr drawList, float left, float top, float width, FashionReportDto report,
        ref int section, float scale)
    {
        var hints = report.Hints ?? Array.Empty<FashionHintDto>();
        var labelPixels = PrismArt.LabelPixels();
        for (var index = 0; index < hints.Length; index++)
        {
            var hint = hints[index];
            var slot = Loc.T(SlotNames[Math.Clamp(hint.Slot, 0, SlotNames.Length - 1)]);
            var slotWidth = PrismType.Measure(slot, PrismFace.Label, labelPixels).X;
            PrismType.DrawRight(drawList, new Vector2(left + width, top), slot, PrismArt.Muted, PrismFace.Label,
                labelPixels);
            top = DrawSectionLabel(drawList, left, top, width, section++,
                PrismType.FitLabel(hint.Hint, labelPixels, width - SectionIndexUnits * scale - slotWidth -
                    Metrics.Space.Md * scale), scale);
            var gear = index < fashionHintGear.Length ? fashionHintGear[index] : Array.Empty<GlamGearDto>();
            for (var itemIndex = 0; itemIndex < gear.Length; itemIndex++)
            {
                top = DrawGearRow(drawList, left, top, width, gear[itemIndex], true, scale);
            }

            top += Metrics.Space.Xl * scale;
        }

        return top;
    }

    private float DrawFashionDyes(ImDrawListPtr drawList, float left, float top, float width, FashionReportDto report,
        int section, float scale)
    {
        var dyes = report.Dyes ?? Array.Empty<FashionDyeDto>();
        if (dyes.Length == 0)
        {
            return top;
        }

        var labelPixels = PrismArt.LabelPixels();
        var row = PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale;
        top = DrawSectionLabel(drawList, left, top, width, section, Loc.T(L.Prism.DyeBonus), scale);
        var column = width / 3f;
        var gap = Metrics.Space.Md * scale;
        for (var index = 0; index < dyes.Length; index++)
        {
            var dye = dyes[index];
            PrismType.Draw(drawList, new Vector2(left, top),
                PrismType.FitLabel(Loc.T(SlotNames[Math.Clamp(dye.Slot, 0, SlotNames.Length - 1)]), labelPixels,
                    column - gap), PrismArt.Muted, PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(left + column, top),
                PrismType.FitLabel(dye.Colour, labelPixels, column - gap), PrismArt.Fore, PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(left + column * 2f, top),
                PrismType.FitLabel(DyeLine(dye.StainId, 0), labelPixels, column), PrismArt.Muted, PrismFace.Label,
                labelPixels);
            top += row;
        }

        return top;
    }
}

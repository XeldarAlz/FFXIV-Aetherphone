using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float GearIconUnits = 40f;
    private const int GearSearchLimit = 40;
    private const int GearSearchLength = 40;
    private const float DyeSwatchUnits = 6f;
    private const int DyeSegments = 16;
    private const int DyeChannels = 2;
    private const string OutfitPrimaryId = "prism.outfit.primary";

    private static readonly LocString[] SlotNames =
    {
        L.Inventory.SlotMainHand, L.Inventory.SlotOffHand, L.Inventory.SlotHead, L.Inventory.SlotBody,
        L.Inventory.SlotHands, L.Inventory.SlotLegs, L.Inventory.SlotFeet, L.Inventory.SlotEars, L.Inventory.SlotNeck,
        L.Inventory.SlotWrists, L.Prism.SlotRing, L.Prism.SlotRing,
    };

    private static readonly string[] RemoveIds = SlotIds("prism.remove.");

    private readonly uint[] draftItems = new uint[GearSlots.All.Length];
    private readonly byte[] draftStain0 = new byte[GearSlots.All.Length];
    private readonly byte[] draftStain1 = new byte[GearSlots.All.Length];
    private readonly GearPick[] gearResults = new GearPick[GearSearchLimit];
    private readonly GearCatalog gearCatalog = new(Plugin.DataManager);
    private readonly HashSet<string> entryDrops = new(StringComparer.Ordinal);
    private PrismScreen outfitReturn = PrismScreen.Cover;
    private string gearQuery = string.Empty;
    private string gearResultsQuery = string.Empty;
    private string entryDropsWeek = string.Empty;
    private int gearResultsSlot = -1;
    private int gearResultCount;
    private int editingSlot;
    private int editingDye;
    private float outfitScroll;
    private bool focusGearSearch;
    private CachedText dyeChannelText;

    private static bool HasOutfit(GlamGearDto[]? outfit) => outfit is { Length: > 0 };

    private static string[] SlotIds(string prefix)
    {
        var ids = new string[GearSlots.All.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = prefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ids;
    }

    private bool EditingEntryOutfit =>
        outfitReturn == PrismScreen.Entry && store.Week is { Phase: PrismPhases.Entering, MyEntry: not null };

    private void OpenOutfit()
    {
        outfitReturn = screen;
        if (!VanillaEntry)
        {
            captured = glamour.Capture();
            if (screen == PrismScreen.Entry)
            {
                KeepEntryDrops();
            }
        }
        else if (screen == PrismScreen.Entry && store.Week?.MyEntry is { } entry)
        {
            LoadDraftGear(entry.Outfit);
        }

        gearCatalog.EnsureBuilt();
        Go(PrismScreen.Outfit);
    }

    private void ResetEntryOutfit()
    {
        captured = VanillaEntry ? Array.Empty<GlamGearDto>() : glamour.Capture();
        KeepEntryDrops();
        LoadDraftGear(store.Week?.MyEntry?.Outfit);
        gearCatalog.EnsureBuilt();
    }

    private void KeepEntryDrops()
    {
        droppedMods.Clear();
        var week = store.Week;
        if (VanillaEntry || week?.MyEntry is not { } entry)
        {
            return;
        }

        if (entryDropsWeek == week.Id)
        {
            droppedMods.UnionWith(entryDrops);
            return;
        }

        var sent = new HashSet<string>(StringComparer.Ordinal);
        var outfit = entry.Outfit ?? Array.Empty<GlamGearDto>();
        for (var index = 0; index < outfit.Length; index++)
        {
            var mods = outfit[index].Mods ?? Array.Empty<GlamModDto>();
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                sent.Add(PostedMods.Key(mods[modIndex]));
            }
        }

        SyncCapturedMods();
        for (var index = 0; index < capturedMods.Length; index++)
        {
            var key = capturedMods[index].Key;
            if (!sent.Contains(key))
            {
                droppedMods.Add(key);
            }
        }
    }

    private void RememberEntryDrops()
    {
        if (VanillaEntry)
        {
            return;
        }

        entryDrops.Clear();
        entryDrops.UnionWith(droppedMods);
        entryDropsWeek = store.Week?.Id ?? string.Empty;
    }

    private GlamGearDto[]? EntryOutfit() => VanillaEntry ? DraftOutfit() : EntryMods();

    private int DraftPieces()
    {
        var count = 0;
        for (var index = 0; index < draftItems.Length; index++)
        {
            if (draftItems[index] != 0)
            {
                count++;
            }
        }

        return count;
    }

    private void LoadDraftGear(GlamGearDto[]? outfit)
    {
        Array.Clear(draftItems);
        Array.Clear(draftStain0);
        Array.Clear(draftStain1);
        var gear = outfit ?? Array.Empty<GlamGearDto>();
        for (var index = 0; index < gear.Length; index++)
        {
            var slot = SlotIndex(gear[index].Slot);
            if (slot < 0 || gear[index].ItemId == 0)
            {
                continue;
            }

            draftItems[slot] = gear[index].ItemId;
            draftStain0[slot] = gear[index].Stain0;
            draftStain1[slot] = gear[index].Stain1;
        }
    }

    private static int SlotIndex(int slot)
    {
        for (var index = 0; index < GearSlots.All.Length; index++)
        {
            if ((int)GearSlots.All[index] == slot)
            {
                return index;
            }
        }

        return -1;
    }

    private GlamGearDto[] DraftOutfit()
    {
        var gear = new GlamGearDto[DraftPieces()];
        var next = 0;
        for (var index = 0; index < draftItems.Length; index++)
        {
            if (draftItems[index] != 0)
            {
                gear[next++] = new GlamGearDto((int)GearSlots.All[index], draftItems[index], draftStain0[index],
                    draftStain1[index]);
            }
        }

        return gear;
    }

    private GlamGearDto[]? EntryMods()
    {
        var gear = PostedMods.Without(captured, droppedMods);
        var kept = new List<GlamModDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < gear.Length; index++)
        {
            var mods = gear[index].Mods ?? Array.Empty<GlamModDto>();
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                if (seen.Add(PostedMods.Key(mods[modIndex])))
                {
                    kept.Add(mods[modIndex]);
                }
            }
        }

        return kept.Count == 0 ? null : new[] { new GlamGearDto(GearSlots.None, Mods: kept.ToArray()) };
    }

    private void OpenGlam(PrismPlacingDto placing)
    {
        var outfit = placing.Outfit ?? Array.Empty<GlamGearDto>();
        OpenGlam(new GlamDto(
            Id: placing.GlamId,
            Author: new GlamAuthorDto(DisplayName: placing.DisplayName, Handle: placing.Handle),
            Title: store.Results?.Title ?? string.Empty,
            Photos: new[] { new GlamPhotoDto(placing.ImageUrl, placing.Width, placing.Height) },
            Gear: outfit,
            Vanilla: GlamourService.IsVanilla(outfit),
            WeekId: store.Results?.WeekId ?? string.Empty,
            Placing: placing.Rank));
    }

    private void TapForOutfit(Vector2 min, Vector2 max, PrismPlacingDto placing)
    {
        if (!HasOutfit(placing.Outfit) && placing.GlamId.Length == 0)
        {
            return;
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            OpenGlam(placing);
        }
    }

    private void DrawOutfit(ImDrawListPtr drawList, Rect content, float scale)
    {
        var vanilla = VanillaEntry;
        var top = DrawBackHeader(drawList, content, store.Week?.Title ?? string.Empty, scale) +
                  Metrics.Space.Sm * scale;
        var row = ActionRect(content, scale);
        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, row.Min.Y - Metrics.Space.Lg * scale));
        using (BeginBody(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = origin.X;
            var cursor = DrawSectionTitle(list, left, left + width, origin.Y,
                Loc.T(vanilla ? L.Prism.YourGear : L.Prism.YourMods), string.Empty, scale) + Metrics.Space.Xs * scale;
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor),
                Loc.T(vanilla ? L.Prism.GearNote : L.Prism.ModsNote), PrismArt.Muted, PrismType.Body, width) +
                      Metrics.Space.Lg * scale;
            cursor = vanilla
                ? DrawGearSlots(list, left, cursor, width, scale)
                : DrawEntryMods(list, left, cursor, width, scale);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        }

        var editing = EditingEntryOutfit;
        var ready = !store.Busy && (!vanilla || !editing || DraftPieces() > 0);
        if (DrawButton(drawList, row, Loc.T(editing ? L.Prism.Save : L.Prism.Done), ButtonStyle.Solid, scale,
                enabled: ready, id: OutfitPrimaryId))
        {
            if (editing)
            {
                RememberEntryDrops();
                store.ShareOutfit(EntryOutfit());
            }

            Go(outfitReturn, -1);
        }
    }

    private float DrawEntryMods(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        SyncCapturedMods();
        if (capturedMods.Length > 0)
        {
            return DrawModRows(drawList, left, top, width, scale);
        }

        return top + Typography.DrawWrappedLeft(new Vector2(left, top),
            Loc.T(glamour.HasPenumbra ? L.Prism.NoModsFound : L.Prism.ModsNeedPenumbra), PrismArt.Muted,
            PrismType.Body, width);
    }

    private float DrawGearSlots(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var icon = GearIconUnits * scale;
        var gap = Metrics.Space.Md * scale;
        var textLeft = left + icon + gap;
        var remove = Loc.T(L.Prism.RemovePiece);
        var removeWidth = ButtonWidth([remove], width * 0.5f, scale);
        var captionLine = Typography.LineHeight(PrismType.Caption);
        var nameLine = Typography.LineHeight(PrismType.Headline);
        for (var index = 0; index < draftItems.Length; index++)
        {
            var itemId = draftItems[index];
            var pick = default(GearPick);
            var known = itemId != 0 && gearCatalog.TryGet(itemId, out pick);
            if (!known && itemId != 0 && GearItem(itemId, out var itemName, out var itemIcon))
            {
                pick = new GearPick(itemId, itemName, itemIcon, DyeChannels);
            }

            var dyes = itemId == 0 ? 0 : known ? Math.Min((int)pick.Dyes, DyeChannels) : DyeChannels;
            var head = MathF.Max(tap, icon);
            var rowBottom = top + head + (dyes > 0 ? tap : 0f);
            if (!ImGui.IsRectVisible(new Vector2(left, top), new Vector2(left + width, rowBottom)))
            {
                top = rowBottom + Metrics.Space.Sm * scale;
                continue;
            }

            var tapRight = itemId != 0 ? left + width - removeWidth - Metrics.Space.Sm * scale : left + width;
            var hovered = UiInteract.Hover(new Vector2(left, top), new Vector2(tapRight, top + head));
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(new Vector2(left, top), new Vector2(tapRight, top + head), hovered))
            {
                OpenGearSearch(index);
            }

            var iconTop = top + (head - icon) * 0.5f;
            var tile = new Rect(new Vector2(left, iconTop), new Vector2(left + icon, iconTop + icon));
            if (pick.IconId != 0)
            {
                GameIconTile.Draw(drawList, textures, pick.IconId, tile.Min, tile.Max, 0f, scale);
            }
            else
            {
                PrismArt.SlotTile(drawList, tile, (int)GearSlots.All[index], 0u, scale, true);
            }

            var textTop = top + (head - captionLine - nameLine) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, textTop), Loc.T(SlotNames[index]), PrismArt.Muted,
                PrismType.Caption);
            var name = itemId == 0 ? Loc.T(L.Prism.AddPiece) : pick.Name ?? string.Empty;
            Typography.Draw(drawList, new Vector2(textLeft, textTop + captionLine),
                Typography.FitText(name, MathF.Max(1f, tapRight - textLeft), PrismType.Headline),
                itemId != 0 || hovered ? PrismArt.Fore : PrismArt.Muted, PrismType.Headline);
            if (itemId != 0 && DrawButton(drawList,
                    new Rect(new Vector2(left + width - removeWidth, top + (head - tap) * 0.5f),
                        new Vector2(left + width, top + (head + tap) * 0.5f)), remove, ButtonStyle.Plain, scale,
                    id: RemoveIds[index]))
            {
                draftItems[index] = 0;
                draftStain0[index] = 0;
                draftStain1[index] = 0;
            }

            var dyeWidth = (left + width - textLeft) / DyeChannels;
            for (var channel = 0; channel < dyes; channel++)
            {
                var dyeLeft = textLeft + channel * dyeWidth;
                DrawDyeTarget(drawList,
                    new Rect(new Vector2(dyeLeft, top + head), new Vector2(dyeLeft + dyeWidth, rowBottom)), index,
                    channel, scale);
            }

            top = rowBottom + Metrics.Space.Sm * scale;
        }

        return top;
    }

    private void DrawDyeTarget(ImDrawListPtr drawList, Rect target, int slot, int channel, float scale)
    {
        var stain = channel == 0 ? draftStain0[slot] : draftStain1[slot];
        var hovered = UiInteract.Hover(target.Min, target.Max);
        var radius = DyeSwatchUnits * scale;
        var center = new Vector2(target.Min.X + radius, target.Center.Y);
        DrawDyeSwatch(drawList, center, radius, stain, scale);
        var textLeft = center.X + radius + Metrics.Space.Xs * scale;
        var label = Typography.FitText(stain == 0 ? Loc.T(L.Prism.AddDye) : gearCatalog.DyeName(stain),
            MathF.Max(1f, target.Max.X - Metrics.Space.Sm * scale - textLeft), PrismType.Body);
        Typography.Draw(drawList,
            new Vector2(textLeft, target.Center.Y - Typography.LineHeight(PrismType.Body) * 0.5f), label,
            stain != 0 || hovered ? PrismArt.Fore : PrismArt.Muted, PrismType.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(target.Min, target.Max, hovered))
        {
            OpenDyePick(slot, channel);
        }
    }

    private void DrawDyeSwatch(ImDrawListPtr drawList, Vector2 center, float radius, byte stain, float scale)
    {
        var rgb = gameData.StainColor(stain);
        if (rgb == 0)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(PrismArt.Muted), DyeSegments,
                Metrics.Stroke.Thin * scale);
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.FromRgb(rgb)), DyeSegments);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(PrismArt.Faint), DyeSegments,
            Metrics.Stroke.Hairline * scale);
    }

    private void OpenGearSearch(int slot)
    {
        editingSlot = slot;
        gearQuery = string.Empty;
        gearResultsSlot = -1;
        focusGearSearch = true;
        outfitScroll = bodyScroll;
        gearCatalog.EnsureBuilt();
        Go(PrismScreen.GearSearch);
    }

    private void OpenDyePick(int slot, int channel)
    {
        editingSlot = slot;
        editingDye = channel;
        outfitScroll = bodyScroll;
        gearCatalog.EnsureBuilt();
        Go(PrismScreen.DyePick);
    }

    private void BackToOutfit()
    {
        Go(PrismScreen.Outfit, -1);
        pendingScroll = outfitScroll;
    }

    private void DrawGearSearch(ImDrawListPtr drawList, Rect content, float scale)
    {
        gearCatalog.EnsureBuilt();
        var top = DrawBackHeader(drawList, content, Loc.T(SlotNames[editingSlot]), scale) + Metrics.Space.Sm * scale;
        if (focusGearSearch)
        {
            ImGui.SetKeyboardFocusHere();
            focusGearSearch = false;
        }

        top = DrawInput(drawList, content.Min.X, top, content.Width, Loc.T(L.Common.Search), "##gearSearch",
            ref gearQuery, GearSearchLength, scale) + Metrics.Space.Md * scale;
        if (gearCatalog.Ready && (gearResultsSlot != editingSlot ||
                                  !string.Equals(gearResultsQuery, gearQuery, StringComparison.Ordinal)))
        {
            gearResultsSlot = editingSlot;
            gearResultsQuery = gearQuery;
            gearResultCount = gearCatalog.Search(gearQuery, GearSlots.All[editingSlot], gearResults);
        }

        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var cursor = origin.Y;
        if (!gearCatalog.Ready || gearResultCount == 0)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor),
                Loc.T(gearCatalog.Ready ? L.Prism.NoMatches : L.Common.Loading), PrismArt.Muted, PrismType.Body,
                width);
        }

        var icon = GearIconUnits * scale;
        var pad = Metrics.Space.Xs * scale;
        for (var index = 0; index < gearResultCount && gearCatalog.Ready; index++)
        {
            var row = new Rect(new Vector2(left, cursor), new Vector2(left + width, cursor + icon + pad * 2f));
            cursor = row.Max.Y;
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var pick = gearResults[index];
            var hovered = UiInteract.Hover(row.Min, row.Max);
            GameIconTile.Draw(list, textures, pick.IconId, new Vector2(left, row.Min.Y + pad),
                new Vector2(left + icon, row.Min.Y + pad + icon), 0f, scale);
            var textLeft = left + icon + Metrics.Space.Md * scale;
            var nameTop = row.Center.Y - Typography.LineHeight(PrismType.Headline) * 0.5f;
            Typography.Draw(list, new Vector2(textLeft, nameTop),
                Typography.FitText(pick.Name, MathF.Max(1f, row.Max.X - textLeft), PrismType.Headline), PrismArt.Fore,
                PrismType.Headline);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                ChooseGear(pick);
            }
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private void ChooseGear(GearPick pick)
    {
        draftItems[editingSlot] = pick.ItemId;
        if (pick.Dyes < DyeChannels)
        {
            draftStain1[editingSlot] = 0;
        }

        if (pick.Dyes == 0)
        {
            draftStain0[editingSlot] = 0;
        }

        BackToOutfit();
    }

    private void DrawDyePick(ImDrawListPtr drawList, Rect content, float scale)
    {
        gearCatalog.EnsureBuilt();
        var key = HashCode.Combine(editingDye, Loc.Current.Code);
        var title = dyeChannelText.IsCurrent(key)
            ? dyeChannelText.Value
            : dyeChannelText.Store(key, Loc.T(L.Prism.DyeChannel, editingDye + 1));
        var top = DrawBackHeader(drawList, content, title, scale) + Metrics.Space.Sm * scale;
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var current = editingDye == 0 ? draftStain0[editingSlot] : draftStain1[editingSlot];
        var cursor = DrawDyeRow(list, left, origin.Y, width, 0, Loc.T(L.Prism.NoDye), current == 0, scale);
        var dyes = gearCatalog.Dyes;
        for (var index = 0; index < dyes.Length; index++)
        {
            cursor = DrawDyeRow(list, left, cursor, width, dyes[index].StainId, dyes[index].Name,
                dyes[index].StainId == current, scale);
        }

        if (!gearCatalog.Ready)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Common.Loading), PrismArt.Muted,
                PrismType.Body, width);
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawDyeRow(ImDrawListPtr drawList, float left, float top, float width, byte stainId, string name,
        bool chosen, float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + Metrics.Size.TapTarget * scale));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var radius = DyeSwatchUnits * 2f * scale;
        DrawDyeSwatch(drawList, new Vector2(left + radius, row.Center.Y), radius, stainId, scale);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var markRoom = chosen ? Metrics.Size.TapTarget * scale : 0f;
        var textLeft = left + radius * 2f + Metrics.Space.Md * scale;
        var nameTop = row.Center.Y - Typography.LineHeight(PrismType.Headline) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, nameTop),
            Typography.FitText(name, MathF.Max(1f, row.Max.X - markRoom - textLeft), PrismType.Headline),
            PrismArt.Fore, PrismType.Headline);
        if (chosen)
        {
            PhoneIcon.Draw(drawList, new Vector2(row.Max.X - markRoom * 0.5f, row.Center.Y), PhoneIcons.Check,
                PrismArt.Fore, Typography.LineHeight(PrismType.Headline));
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            if (editingDye == 0)
            {
                draftStain0[editingSlot] = stainId;
            }
            else
            {
                draftStain1[editingSlot] = stainId;
            }

            BackToOutfit();
        }

        return row.Max.Y;
    }
}

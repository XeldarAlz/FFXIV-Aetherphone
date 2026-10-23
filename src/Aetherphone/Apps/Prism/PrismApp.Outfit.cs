using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float GearIconUnits = 32f;
    private const int GearSearchLimit = 40;
    private const int GearSearchLength = 40;
    private const float DyeSwatchUnits = 5f;
    private const float DyeMarkUnits = 3f;
    private const int DyeSegments = 16;
    private const int DyeChannels = 2;

    private static readonly LocString[] SlotNames =
    {
        L.Inventory.SlotMainHand, L.Inventory.SlotOffHand, L.Inventory.SlotHead, L.Inventory.SlotBody,
        L.Inventory.SlotHands, L.Inventory.SlotLegs, L.Inventory.SlotFeet, L.Inventory.SlotEars, L.Inventory.SlotNeck,
        L.Inventory.SlotWrists, L.Prism.SlotRing, L.Prism.SlotRing,
    };

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
        var top = DrawHeader(drawList, content, string.Empty, IssueLine(store.Week?.Issue ?? 0), true, scale);
        var strip = StripRect(content, scale);
        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale));
        using (BeginBody(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var gutter = PrismArt.Gutter(scale);
            var width = ScrollLayout.StableContentWidth() - gutter * 2f;
            var left = origin.X + gutter;
            var cursor = DrawSectionTitle(list, left, origin.Y, width,
                Loc.T(vanilla ? L.Prism.YourGear : L.Prism.YourMods), scale);
            var note = PrismType.Sentences(Loc.T(vanilla ? L.Prism.GearNote : L.Prism.ModsNote), TextStyles.Subheadline,
                width);
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), note, PrismArt.Muted, TextStyles.Subheadline,
                PrismType.BalancedWidth(note, TextStyles.Subheadline, width)) + Metrics.Space.Lg * scale;
            cursor = vanilla
                ? DrawGearSlots(list, left, cursor, width, scale)
                : DrawEntryMods(list, left, cursor, width, scale);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        }

        var editing = EditingEntryOutfit;
        var ready = !store.Busy && (!vanilla || !editing || DraftPieces() > 0);
        if (PrismArt.Strip(drawList, strip, Loc.T(editing ? L.Prism.Save : L.Prism.Done), string.Empty, true,
                ready))
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
            TextStyles.Subheadline, width);
    }

    private float DrawGearSlots(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = GearIconUnits * scale;
        var pad = Metrics.Space.Md * scale;
        var remove = Loc.T(L.Prism.RemovePiece);
        var removeWidth = PrismType.Measure(remove, PrismFace.Label, labelPixels).X;
        var textLeft = left + icon + pad;
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
            var lines = dyes > 0 ? 3 : 2;
            var rowTop = top + pad;
            var textTop = rowTop + MathF.Max(0f, (icon - labelLine * lines) * 0.5f);
            var rowBottom = rowTop + MathF.Max(icon, labelLine * lines) + pad;
            if (!ImGui.IsRectVisible(new Vector2(left, top), new Vector2(left + width, rowBottom)))
            {
                top = rowBottom;
                continue;
            }

            var row = new Rect(new Vector2(left, top), new Vector2(left + width, rowBottom));
            var hovered = UiInteract.Hover(row.Min, row.Max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenGearSearch(index);
            }

            if (pick.IconId != 0)
            {
                GameIconTile.Draw(drawList, textures, pick.IconId, new Vector2(left, rowTop),
                    new Vector2(left + icon, rowTop + icon), 0f, scale);
            }
            else
            {
                drawList.AddRect(new Vector2(left, rowTop), new Vector2(left + icon, rowTop + icon),
                    ImGui.GetColorU32(PrismArt.Faint), 0f, ImDrawFlags.None, PrismArt.HairlineWidth(scale));
            }

            PrismType.Draw(drawList, new Vector2(textLeft, textTop), Loc.T(SlotNames[index]), PrismArt.Muted,
                PrismFace.Label, labelPixels);
            var name = itemId == 0 ? Loc.T(L.Prism.AddPiece) : pick.Name ?? string.Empty;
            var room = left + width - textLeft - (itemId != 0 ? removeWidth + pad : 0f);
            PrismType.Draw(drawList, new Vector2(textLeft, textTop + labelLine),
                PrismType.FitLabel(name, labelPixels, room), hovered || itemId != 0 ? PrismArt.Fore : PrismArt.Muted,
                PrismFace.Label, labelPixels);
            if (itemId != 0 && PrismArt.Link(drawList, new Vector2(left + width - removeWidth, textTop + labelLine),
                    remove, PrismArt.Muted, scale, labelLine))
            {
                draftItems[index] = 0;
                draftStain0[index] = 0;
                draftStain1[index] = 0;
            }

            var x = textLeft;
            var dyeRoom = (left + width - textLeft) / MathF.Max(dyes, 1) - Metrics.Space.Lg * scale;
            for (var channel = 0; channel < dyes; channel++)
            {
                x = DrawDyeLink(drawList, x, textTop + labelLine * 2f, dyeRoom, index, channel, labelPixels, scale);
            }

            PrismArt.Hairline(drawList, left, left + width, rowBottom, PrismArt.Faint, scale);
            top = rowBottom;
        }

        return top;
    }

    private float DrawDyeLink(ImDrawListPtr drawList, float left, float top, float room, int slot, int channel,
        float labelPixels, float scale)
    {
        var stain = channel == 0 ? draftStain0[slot] : draftStain1[slot];
        var line = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var swatch = 0f;
        if (stain != 0 && DyeColor(stain) is { } color)
        {
            var radius = DyeSwatchUnits * scale;
            drawList.AddCircleFilled(new Vector2(left + radius, top + line * 0.5f), radius, color, DyeSegments);
            swatch = radius * 2f + Metrics.Space.Xs * scale;
        }

        var label = PrismType.FitLabel(stain == 0 ? Loc.T(L.Prism.AddDye) : gearCatalog.DyeName(stain), labelPixels,
            room - swatch);
        if (PrismArt.Link(drawList, new Vector2(left + swatch, top), label, stain == 0 ? PrismArt.Muted : PrismArt.Fore,
                scale, line))
        {
            OpenDyePick(slot, channel);
        }

        return left + swatch + PrismType.Measure(label, PrismFace.Label, labelPixels).X + Metrics.Space.Lg * scale;
    }

    private uint? DyeColor(byte stainId)
    {
        var dyes = gearCatalog.Dyes;
        for (var index = 0; index < dyes.Length; index++)
        {
            if (dyes[index].StainId == stainId)
            {
                return DyeInk(dyes[index].Color);
            }
        }

        return null;
    }

    private static uint DyeInk(uint rgb) =>
        0xFF000000u | ((rgb & 0xFFu) << 16) | (rgb & 0xFF00u) | ((rgb >> 16) & 0xFFu);

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
        var top = DrawHeader(drawList, content, string.Empty, Loc.T(SlotNames[editingSlot]), true, scale);
        var gutter = PrismArt.Gutter(scale);
        if (focusGearSearch)
        {
            ImGui.SetKeyboardFocusHere();
            focusGearSearch = false;
        }

        top = DrawInput(drawList, content.Min.X + gutter, top, content.Width - gutter * 2f, Loc.T(L.Common.Search),
            "##gearSearch", ref gearQuery, GearSearchLength, scale) + Metrics.Space.Md * scale;
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
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = origin.Y;
        if (!gearCatalog.Ready || gearResultCount == 0)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor),
                Loc.T(gearCatalog.Ready ? L.Prism.NoMatches : L.Common.Loading), PrismArt.Muted,
                TextStyles.Subheadline, width);
        }

        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = GearIconUnits * scale;
        var pad = Metrics.Space.Sm * scale;
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
            PrismType.Draw(list, new Vector2(textLeft, row.Center.Y - labelLine * 0.5f),
                PrismType.FitLabel(pick.Name, labelPixels, row.Max.X - textLeft), hovered ? PrismArt.Fore : PrismArt.Muted,
                PrismFace.Label, labelPixels);
            PrismArt.Hairline(list, textLeft, row.Max.X, row.Max.Y, PrismArt.Faint, scale);
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
        var top = DrawHeader(drawList, content, string.Empty, title, true, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var current = editingDye == 0 ? draftStain0[editingSlot] : draftStain1[editingSlot];
        var cursor = DrawDyeRow(list, left, origin.Y, width, 0, Loc.T(L.Prism.NoDye), null, current == 0, scale);
        var dyes = gearCatalog.Dyes;
        for (var index = 0; index < dyes.Length; index++)
        {
            cursor = DrawDyeRow(list, left, cursor, width, dyes[index].StainId, dyes[index].Name,
                DyeInk(dyes[index].Color), dyes[index].StainId == current, scale);
        }

        if (!gearCatalog.Ready)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Common.Loading), PrismArt.Muted,
                TextStyles.Subheadline, width);
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawDyeRow(ImDrawListPtr drawList, float left, float top, float width, byte stainId, string name,
        uint? color, bool chosen, float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + Metrics.Size.TapTarget * scale));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var radius = DyeSwatchUnits * 2f * scale;
        var center = new Vector2(left + radius, row.Center.Y);
        if (color is { } ink)
        {
            drawList.AddCircleFilled(center, radius, ink, DyeSegments);
        }
        else
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(PrismArt.Faint), DyeSegments,
                PrismArt.HairlineWidth(scale));
        }

        var hovered = UiInteract.Hover(row.Min, row.Max);
        PrismType.Draw(drawList, new Vector2(left + radius * 2f + Metrics.Space.Md * scale, row.Center.Y - labelLine * 0.5f),
            name, hovered || chosen ? PrismArt.Fore : PrismArt.Muted, PrismFace.Label, labelPixels);
        if (chosen)
        {
            drawList.AddCircleFilled(new Vector2(row.Max.X - DyeMarkUnits * scale, row.Center.Y), DyeMarkUnits * scale,
                ImGui.GetColorU32(PrismArt.Fore), DyeSegments);
        }

        PrismArt.Hairline(drawList, left, row.Max.X, row.Max.Y, PrismArt.Faint, scale);
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

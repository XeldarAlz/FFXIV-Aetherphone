using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal readonly record struct WornMod(GlamModDto Mod, int Slot);

internal sealed partial class PrismApp
{
    private const string OutfitNameId = "##outfitName";
    private const string OutfitModId = "##outfitMod";
    private const float EditorThumbUnits = 96f;
    private const float EditorThumbShare = 0.4f;
    private const float WornCheckUnits = 22f;

    private readonly GearDraft outfitGear = new();
    private readonly ModDraft outfitMods = new();
    private readonly List<WornMod> wornMods = new();
    private readonly List<bool> wornPicked = new();
    private readonly List<string> wornCaptions = new();
    private ModDraft? wornTarget;
    private WardrobeOutfit? editingOutfit;
    private WardrobeCover? outfitCoverDraft;
    private GlamGearDto[] outfitLoadedPieces = Array.Empty<GlamGearDto>();
    private GlamGearDto[] outfitStatePieces = Array.Empty<GlamGearDto>();
    private string outfitStateDraft = string.Empty;
    private string outfitNameDraft = string.Empty;
    private string? wornCaptionsLanguage;
    private int wornPickedCount;
    private bool coverForEditor;

    private void StartOutfitEdit(WardrobeOutfit? outfit)
    {
        wardrobe.Ensure();
        editingOutfit = outfit;
        outfitNameDraft = outfit?.Name ?? string.Empty;
        outfitCoverDraft = outfit?.Cover;
        outfitStateDraft = outfit?.State ?? string.Empty;
        var gear = outfit?.Gear ?? Array.Empty<GlamGearDto>();
        LoadGear(outfitGear, gear);
        outfitMods.Load(gear);
        outfitLoadedPieces = OutfitFrom(outfitGear);
        outfitStatePieces = outfitLoadedPieces;
        gearCatalog.EnsureBuilt();
        Push(PrismScreen.WardrobeEdit);
    }

    private void DrawOutfitEditor(ImDrawListPtr drawList, Rect content, float scale)
    {
        var filled = PieceCount(outfitGear) > 0 || outfitMods.Count > 0;
        var top = DrawActionHeader(drawList, content, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back),
            Loc.T(editingOutfit is null ? L.Prism.NewOutfit : L.Prism.EditOutfit), PhoneIcons.Check,
            Loc.T(L.Prism.Save), filled && !wardrobe.Loading, string.Empty, UiSound.Tap, scale, out var save) +
                  Metrics.Space.Sm * scale;
        if (save)
        {
            SaveOutfitDraft();
            return;
        }

        var bottom = content.Max.Y - PrismArt.BottomMargin(scale);
        if (!filled)
        {
            var notice = Loc.T(L.Prism.OutfitNeedsSomething);
            var noticeHeight = Typography.MeasureWrappedBlock(notice, PrismType.Body, content.Width).Y;
            Typography.DrawWrappedLeft(new Vector2(content.Min.X, bottom - noticeHeight), notice, PrismArt.Muted,
                PrismType.Body, content.Width);
            bottom -= noticeHeight + Metrics.Space.Lg * scale;
        }

        using (BeginBody(new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, bottom))))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = origin.X;
            var cursor = DrawEditorPhoto(list, left, origin.Y, width, scale);
            cursor = DrawField(list, left, cursor, width, Loc.T(L.Prism.OutfitNameLabel),
                editingOutfit?.Name ?? OutfitName(wardrobe.Outfits.Length + 1), OutfitNameId, ref outfitNameDraft,
                TitleMaxLength, scale);
            cursor = DrawEditorPieces(list, left, cursor + Metrics.Space.Sm * scale, width, scale);
            cursor = DrawEditorMods(list, left, cursor + Metrics.Space.Xl * scale, width, scale);
            if (editingOutfit is { } outfit)
            {
                cursor = DrawEditorManage(list, left, cursor + Metrics.Space.Xl * scale, width, outfit, scale);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        }
    }

    private float DrawEditorPhoto(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var tileWidth = MathF.Min(EditorThumbUnits * scale, width * EditorThumbShare);
        var tile = new Rect(new Vector2(left, top), new Vector2(left + tileWidth, top + tileWidth / GlamStore.PhotoRatio));
        if (!DrawOutfitCover(drawList, tile, outfitCoverDraft))
        {
            PrismArt.Placeholder(drawList, tile, false, scale);
            PhoneIcon.Draw(drawList, tile.Center, PhoneIcons.PhotoPlus, PrismArt.Muted, PrismArt.IconUnits * scale);
        }

        var tapped = UiInteract.Click(tile.Min, tile.Max, HoverWash(drawList, tile));
        var label = Loc.T(outfitCoverDraft is null ? L.Prism.AddPhoto : L.Prism.ChangePhoto);
        var buttonLeft = tile.Max.X + Metrics.Space.Md * scale;
        var buttonHeight = Button.RegularHeight * scale;
        var button = new Rect(new Vector2(buttonLeft, tile.Center.Y - buttonHeight * 0.5f),
            new Vector2(buttonLeft + ButtonWidth([label], left + width - buttonLeft, scale),
                tile.Center.Y + buttonHeight * 0.5f));
        var pressed = DrawButton(drawList, button, label, ButtonStyle.Tinted, scale, id: "prism.outfit.photo");
        if (tapped || pressed)
        {
            PickEditorCover();
        }

        return tile.Max.Y + Metrics.Space.Lg * scale;
    }

    private float DrawEditorPieces(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        top = DrawSectionTitle(drawList, left, left + width, top, Loc.T(L.Prism.PiecesHeading), string.Empty, scale) +
              Metrics.Space.Xs * scale;
        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.OutfitPiecesHint), PrismArt.Muted,
            PrismType.Body, width) + Metrics.Space.Md * scale;
        var fill = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.RegularHeight * scale));
        if (DrawButton(drawList, fill, Loc.T(L.Prism.FillFromWearing), ButtonStyle.Tinted, scale,
                id: "prism.outfit.fill"))
        {
            FillOutfitFromWearing();
        }

        return DrawGearSlots(drawList, left, fill.Max.Y + Metrics.Space.Lg * scale, width, scale);
    }

    private float DrawEditorMods(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        top = DrawSectionTitle(drawList, left, left + width, top, Loc.T(L.Prism.ModsHeading), string.Empty, scale) +
              Metrics.Space.Md * scale;
        top = DrawPickModsButton(drawList, left, top, width, outfitMods, "prism.outfit.pickMods", scale);
        return DrawNamedMods(drawList, left, top, width, outfitMods, outfitGear, OutfitModId, scale);
    }

    private float DrawPickModsButton(ImDrawListPtr drawList, float left, float top, float width, ModDraft target,
        string id, float scale)
    {
        if (!glamour.HasPenumbra)
        {
            return top + Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.ModsNeedPenumbra),
                PrismArt.Muted, PrismType.Body, width) + Metrics.Space.Md * scale;
        }

        var pick = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.RegularHeight * scale));
        if (DrawButton(drawList, pick, Loc.T(L.Prism.PickMyMods), ButtonStyle.Tinted, scale, id: id))
        {
            OpenWornMods(target);
        }

        return pick.Max.Y + Metrics.Space.Md * scale;
    }

    private float DrawEditorManage(ImDrawListPtr drawList, float left, float top, float width, WardrobeOutfit outfit,
        float scale)
    {
        var height = Button.RegularHeight * scale;
        if (outfit.State.Length > 0 && glamour.HasGlamourer)
        {
            var glamourer = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
            if (DrawButton(drawList, glamourer, Loc.T(L.Prism.SaveToGlamourer), ButtonStyle.Gray, scale,
                    id: "prism.outfit.glamourer"))
            {
                SaveToGlamourer(outfit);
                editingOutfit = wardrobe.Find(outfit.Id) ?? outfit;
            }

            top = glamourer.Max.Y + Metrics.Space.Sm * scale;
        }

        var delete = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (Button.Draw(drawList, delete, Loc.T(L.Prism.DeleteOutfit), controls, ButtonStyle.Gray,
                ButtonRole.Destructive, id: "prism.outfit.delete"))
        {
            AskDeleteOutfit(outfit);
        }

        return delete.Max.Y;
    }

    private void AskDeleteOutfit(WardrobeOutfit outfit) =>
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Prism.DeleteOutfitTitle),
            Message = outfit.Name,
            ConfirmLabel = Loc.T(L.Common.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Sheet = true,
            Confirm = () =>
            {
                wardrobe.Remove(outfit.Id);
                GoBack();
            },
        });

    private void FillOutfitFromWearing()
    {
        if (!FillFromWearing(outfitGear))
        {
            return;
        }

        outfitStateDraft = glamour.ReadState();
        outfitStatePieces = OutfitFrom(outfitGear);
    }

    private bool FillFromWearing(GearDraft draft)
    {
        var gear = glamour.Capture();
        if (CountPieces(gear) == 0)
        {
            ShellToast.Show(Loc.T(L.Prism.NothingToSave));
            return false;
        }

        LoadGear(draft, gear);
        return true;
    }

    private void SaveOutfitDraft()
    {
        var pieces = OutfitFrom(outfitGear);
        var keepsState = SamePieces(pieces, outfitStatePieces);
        var keepsDesign = SamePieces(pieces, outfitLoadedPieces);
        var gear = outfitMods.AttachTo(pieces);
        var name = outfitNameDraft.Trim();
        if (editingOutfit is { } existing)
        {
            wardrobe.Update(existing with
            {
                Name = name.Length > 0 ? name : existing.Name,
                State = keepsState ? outfitStateDraft : string.Empty,
                DesignId = keepsDesign ? existing.DesignId : Guid.Empty,
                Gear = gear,
                Cover = outfitCoverDraft,
            });
        }
        else
        {
            wardrobe.Add(new WardrobeOutfit(Guid.NewGuid().ToString("N"),
                name.Length > 0 ? name : OutfitName(wardrobe.Outfits.Length + 1),
                keepsState ? outfitStateDraft : string.Empty, Guid.Empty, gear, false,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Cover: outfitCoverDraft));
        }

        GoBack();
    }

    private static bool SamePieces(GlamGearDto[] pieces, GlamGearDto[] baseline)
    {
        if (pieces.Length != baseline.Length)
        {
            return false;
        }

        for (var index = 0; index < pieces.Length; index++)
        {
            var piece = pieces[index];
            var other = baseline[index];
            if (piece.Slot != other.Slot || piece.ItemId != other.ItemId || piece.Stain0 != other.Stain0 ||
                piece.Stain1 != other.Stain1)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountPieces(GlamGearDto[] gear)
    {
        var count = 0;
        for (var index = 0; index < gear.Length; index++)
        {
            count += gear[index].ItemId != 0 ? 1 : 0;
        }

        return count;
    }

    private void PickEditorCover()
    {
        coverForEditor = true;
        Push(PrismScreen.WardrobeCover);
        composeSession.Open(true);
    }

    private void OpenWornMods(ModDraft target)
    {
        wornTarget = target;
        wornMods.Clear();
        wornPicked.Clear();
        wornPickedCount = 0;
        wornCaptionsLanguage = null;
        var gear = glamour.Capture();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < gear.Length; index++)
        {
            var mods = gear[index].Mods ?? Array.Empty<GlamModDto>();
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                var key = PostedMods.Key(mods[modIndex]);
                if (target.Contains(key) || !seen.Add(key))
                {
                    continue;
                }

                wornMods.Add(new WornMod(mods[modIndex], gear[index].Slot));
                wornPicked.Add(false);
            }
        }

        Push(PrismScreen.WardrobeMods);
    }

    private void DrawWornMods(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawActionHeader(drawList, content, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back),
            Loc.T(L.Prism.PickMyMods), PhoneIcons.Check, Loc.T(L.Prism.AddSelected), wornPickedCount > 0,
            string.Empty, UiSound.Tap, scale, out var add) + Metrics.Space.Sm * scale;
        if (add)
        {
            AddPickedMods();
            GoBack();
            return;
        }

        SyncWornCaptions();
        using var surface = BeginBody(new Rect(new Vector2(content.Min.X, top), content.Max));
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var hint = !glamour.HasPenumbra ? L.Prism.ModsNeedPenumbra
            : wornMods.Count == 0 ? L.Prism.NoModsFound
            : L.Prism.PickMyModsHint;
        var cursor = origin.Y + Typography.DrawWrappedLeft(new Vector2(left, origin.Y), Loc.T(hint), PrismArt.Muted,
            PrismType.Body, width) + Metrics.Space.Md * scale;
        for (var index = 0; index < wornMods.Count; index++)
        {
            cursor = DrawWornModRow(list, left, cursor, width, index, scale);
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawWornModRow(ImDrawListPtr drawList, float left, float top, float width, int index, float scale)
    {
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var height = MathF.Max(Metrics.Size.TapTarget * scale, nameHeight + captionHeight) + Metrics.Space.Sm * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var picked = wornPicked[index];
        var check = WornCheckUnits * scale;
        var hovered = HoverWash(drawList, row);
        PhoneIcon.Draw(drawList, new Vector2(left + check * 0.5f, row.Center.Y),
            picked ? PhoneIcons.CircleCheck : PhoneIcons.Circle, picked ? PrismArt.Fore : PrismArt.Muted, check);
        var textLeft = left + check + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, row.Max.X - textLeft);
        var textTop = row.Center.Y - (nameHeight + captionHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(wornMods[index].Mod.Name, room, PrismType.Headline), PrismArt.Fore, PrismType.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(wornCaptions[index], room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            wornPicked[index] = !picked;
            wornPickedCount += picked ? -1 : 1;
        }

        return row.Max.Y;
    }

    private void SyncWornCaptions()
    {
        if (ReferenceEquals(wornCaptionsLanguage, Loc.Current.Code) && wornCaptions.Count == wornMods.Count)
        {
            return;
        }

        wornCaptionsLanguage = Loc.Current.Code;
        wornCaptions.Clear();
        for (var index = 0; index < wornMods.Count; index++)
        {
            var mod = wornMods[index];
            var slot = ModTypeName(mod.Slot);
            wornCaptions.Add(mod.Mod.Author.Length > 0 ? string.Concat(slot, GlamMetaSeparator, mod.Mod.Author) : slot);
        }
    }

    private void AddPickedMods()
    {
        var target = wornTarget;
        if (target is null)
        {
            return;
        }

        for (var index = 0; index < wornMods.Count && target.Count < MaxNamedMods; index++)
        {
            if (wornPicked[index])
            {
                target.Add(wornMods[index].Mod, wornMods[index].Slot);
            }
        }
    }
}

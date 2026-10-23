using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal enum WardrobeAction : byte
{
    Import,
    Surprise,
    Rename,
    Photo,
    Glamourer,
    Delete,
}

internal sealed partial class PrismApp
{
    private const float BoardDesignUnits = 360f;
    private const float BoardTileUnits = 56f;
    private const float BoardAccessoryUnits = 44f;
    private const float BoardPitchUnits = 68f;
    private const float BoardColumnUnits = 96f;
    private const int BoardWeaponRow = 1;
    private const float BoardFill = 0.9f;
    private const int SheetCapacity = 4;
    private const float SaveGlyphUnits = 14f;

    private static readonly int[] BodySlots =
    {
        (int)GearSlot.Head, (int)GearSlot.Body, (int)GearSlot.Hands, (int)GearSlot.Legs, (int)GearSlot.Feet,
    };

    private static readonly int[] AccessorySlots =
    {
        (int)GearSlot.Ears, (int)GearSlot.Neck, (int)GearSlot.Wrists, (int)GearSlot.RingRight, (int)GearSlot.RingLeft,
    };

    private static readonly int[] LeftSlots = { (int)GearSlot.MainHand, (int)GearSlot.OffHand, GearSlots.None };

    private readonly WardrobeStore wardrobe;
    private readonly Random roulette = new();
    private readonly ActionSheet wardrobeSheet = new();
    private readonly ActionSheet.Item[] sheetItems = new ActionSheet.Item[SheetCapacity];
    private readonly WardrobeAction[] sheetActions = new WardrobeAction[SheetCapacity];
    private readonly ChipRail wardrobeRail = new();
    private readonly string[] wardrobeChips = new string[2];
    private readonly bool[] wardrobeChipActive = new bool[2];
    private WardrobeOutfit? fitFor;
    private string? fitLanguage;
    private string fitMeta = string.Empty;
    private string outfitId = string.Empty;
    private bool favouritesOnly;
    private CachedText outfitNameText;
    private CachedText allChipText;
    private CachedText favouriteChipText;
    private bool renaming;
    private bool focusRename;
    private string renameText = string.Empty;
    private string coverFor = string.Empty;
    private int sheetCount;
    private PrismScreen sheetScreen;
    private string sheetOutfit = string.Empty;

    private void DrawWardrobe(ImDrawListPtr drawList, Rect content, float scale)
    {
        wardrobe.Ensure();
        GateWardrobeSheet();
        var imported = wardrobe.PumpImport(glamour);
        if (imported >= 0)
        {
            ShellToast.Show(Loc.T(L.Prism.DesignsAdded, imported));
        }

        var top = DrawWardrobeHeader(drawList, content, scale);
        top = DrawWardrobeChips(content, top, scale);
        var full = FullFrom(content, scale);
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        if (wardrobe.Loading || wardrobe.Importing || CountOutfits(favouritesOnly) > 0)
        {
            DrawOutfitGrid(body, scale);
        }
        else
        {
            DrawEmpty(drawList, new Rect(new Vector2(content.Min.X, top),
                    new Vector2(content.Max.X, content.Max.Y - bottomChrome)),
                favouritesOnly ? PhoneIcons.Star : IconGlyph.Of(FontAwesomeIcon.Tshirt),
                Loc.T(favouritesOnly ? L.Prism.FavouritesEmptyTitle : L.Prism.WardrobeEmptyTitle),
                Loc.T(favouritesOnly ? L.Prism.FavouritesEmptyHint : L.Prism.WardrobeEmptyHint),
                favouritesOnly ? string.Empty : Loc.T(L.Prism.SaveWearing), ButtonStyle.Solid, scale, out var save);
            if (save)
            {
                SaveWearing();
            }
        }

        DrawWardrobeSheet(full);
    }

    private float DrawWardrobeHeader(ImDrawListPtr drawList, Rect content, float scale)
    {
        var height = Metrics.Size.TapTarget * scale;
        var top = content.Min.Y + Metrics.Space.Sm * scale;
        var middle = top + height * 0.5f;
        var left = content.Min.X;
        var right = content.Max.X;
        var canImport = glamour.HasGlamourer && !wardrobe.Importing;
        var canShuffle = wardrobe.Outfits.Length > 1;
        if (canImport || canShuffle)
        {
            if (IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Dots, Loc.T(L.Prism.More), scale))
            {
                sheetCount = 0;
                AddSheetAction(canImport, WardrobeAction.Import);
                AddSheetAction(canShuffle, WardrobeAction.Surprise);
                OpenWardrobeSheet(string.Empty);
            }

            right -= height + Metrics.Space.Xs * scale;
        }

        var save = Loc.T(L.Prism.SaveWearing);
        var heading = Loc.T(L.Prism.TabWardrobe);
        var pillHeight = Button.RegularHeight * scale;
        var labelStyle = Button.LabelStyle(pillHeight);
        var glyph = SaveGlyphUnits * scale;
        var gap = Metrics.Space.Xs * scale;
        var textWidth = Typography.Measure(save, labelStyle).X;
        var saveWidth = glyph + gap + textWidth + Metrics.Space.Md * 2f * scale;
        var labelled = left + Typography.Measure(heading, PrismType.Title).X + Metrics.Space.Md * scale <=
                       right - saveWidth;
        var saveLeft = labelled ? right - saveWidth : right - height;
        bool saved;
        if (labelled)
        {
            var hit = new Rect(new Vector2(saveLeft, top), new Vector2(right, top + height));
            var hovered = UiInteract.Hover(hit.Min, hit.Max);
            var pill = new Rect(new Vector2(saveLeft, middle - pillHeight * 0.5f),
                new Vector2(right, middle + pillHeight * 0.5f));
            var face = Button.Surface(drawList, pill, controls, ButtonStyle.Solid, ButtonRole.Normal, true, hovered,
                ImGui.GetID("prism.saveWearing"));
            var x = face.Face.Center.X - (glyph + gap + textWidth) * 0.5f;
            PhoneIcon.Draw(drawList, new Vector2(x + glyph * 0.5f, face.Face.Center.Y), PhoneIcons.Plus, face.LabelInk,
                glyph);
            Typography.Draw(drawList,
                new Vector2(x + glyph + gap, face.Face.Center.Y - Typography.LineHeight(labelStyle) * 0.5f), save,
                face.LabelInk, labelStyle);
            saved = UiInteract.Click(hit.Min, hit.Max, hovered);
        }
        else
        {
            saved = IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Plus, save, scale);
        }
        if (saved)
        {
            SaveWearing();
        }

        var title = Typography.FitText(heading, MathF.Max(1f, saveLeft - Metrics.Space.Md * scale - left),
            PrismType.Title);
        Typography.Draw(drawList, new Vector2(left, middle - Typography.LineHeight(PrismType.Title) * 0.5f), title,
            PrismArt.Fore, PrismType.Title);
        return top + height;
    }

    private float DrawWardrobeChips(Rect content, float top, float scale)
    {
        var all = wardrobe.Outfits.Length;
        var favourites = CountOutfits(true);
        var allKey = HashCode.Combine(all, Loc.Current.Code);
        wardrobeChips[0] = allChipText.IsCurrent(allKey)
            ? allChipText.Value
            : allChipText.Store(allKey, string.Concat(Loc.T(L.Prism.FilterAll), " ", CountLabel(all)));
        var favouriteKey = HashCode.Combine(favourites, Loc.Current.Code);
        wardrobeChips[1] = favouriteChipText.IsCurrent(favouriteKey)
            ? favouriteChipText.Value
            : favouriteChipText.Store(favouriteKey,
                string.Concat(Loc.T(L.Prism.Favourites), " ", CountLabel(favourites)));
        wardrobeChipActive[0] = !favouritesOnly;
        wardrobeChipActive[1] = favouritesOnly;
        var row = new Rect(new Vector2(content.Min.X, top + Metrics.Space.Xs * scale),
            new Vector2(content.Max.X, top + Metrics.Space.Xs * scale + Metrics.Size.TapTarget * scale));
        var picked = wardrobeRail.Draw(row, ui, wardrobeChips, wardrobeChipActive);
        if (picked >= 0)
        {
            favouritesOnly = picked == 1;
        }

        return row.Max.Y + Metrics.Space.Xs * scale;
    }

    private int CountOutfits(bool favourites)
    {
        var outfits = wardrobe.Outfits;
        if (!favourites)
        {
            return outfits.Length;
        }

        var count = 0;
        for (var index = 0; index < outfits.Length; index++)
        {
            if (outfits[index].Favourite)
            {
                count++;
            }
        }

        return count;
    }

    private void DrawOutfitGrid(Rect body, float scale)
    {
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = Metrics.Space.Xxs * scale;
        var gridWidth = width - gap * 2f;
        var line = GridLine(scale);
        var outfits = wardrobe.Outfits;
        var busy = wardrobe.Loading || wardrobe.Importing;
        var count = busy ? PlaceholderRows * GridColumns : outfits.Length;
        var shown = 0;
        for (var index = 0; index < count; index++)
        {
            if (!busy && favouritesOnly && !outfits[index].Favourite)
            {
                continue;
            }

            var photo = GridCell(origin.X + gap, origin.Y, gridWidth, shown++, scale);
            if (!ImGui.IsRectVisible(photo.Min, new Vector2(photo.Max.X, photo.Max.Y + line)))
            {
                continue;
            }

            if (busy)
            {
                PrismArt.Placeholder(list, photo, false, scale);
                continue;
            }

            var outfit = outfits[index];
            var mark = PrismMotion.Mark(list);
            if (!DrawOutfitCover(list, photo, outfit.Cover))
            {
                PrismArt.Placeholder(list, photo, false, scale);
                if (outfit.Cover is not { } cover || wallpaperImages.Failed(cover.Path))
                {
                    DrawOutfitBoard(list, photo, photo, outfit.Gear, false, scale);
                }
            }

            if (DrawGridTile(list, mark, photo, string.Empty, outfit.Name, string.Empty, null, string.Empty,
                    PieceLabel(outfit.Gear), scale))
            {
                OpenOutfit(outfit.Id);
            }
        }

        ImGui.Dummy(new Vector2(width, GridHeight(gridWidth, shown, scale) + PrismArt.BottomMargin(scale)));
    }

    private void OpenOutfit(string id)
    {
        Push(PrismScreen.WardrobeOutfit);
        outfitId = id;
        ResetPieces();
        renaming = false;
    }

    private string PieceLabel(GlamGearDto[] gear)
    {
        var pieces = 0;
        for (var index = 0; index < gear.Length; index++)
        {
            pieces += gear[index].ItemId != 0 ? 1 : 0;
        }

        return pieces > 0 ? CountLabel(pieces) : string.Empty;
    }

    private void DrawOutfitBoard(ImDrawListPtr drawList, Rect board, Rect area, GlamGearDto[] gear, bool showEmpty,
        float scale)
    {
        var unit = board.Width / BoardDesignUnits;
        var center = area.Center.X;
        if (!showEmpty)
        {
            var low = WearsAny(gear, LeftSlots) ? -BoardColumnUnits - BoardAccessoryUnits * 0.5f
                : -BoardTileUnits * 0.5f;
            var high = WearsAny(gear, AccessorySlots) ? BoardColumnUnits + BoardAccessoryUnits * 0.5f
                : BoardTileUnits * 0.5f;
            unit = MathF.Min(area.Width / (high - low),
                area.Height / (BoardPitchUnits * (BodySlots.Length - 1) + BoardTileUnits)) * BoardFill;
            center -= (low + high) * 0.5f * unit;
        }

        var pitch = BoardPitchUnits * unit;
        var firstRow = area.Center.Y - pitch * (BodySlots.Length - 1) * 0.5f;
        var side = BoardColumnUnits * unit;
        for (var row = 0; row < BodySlots.Length; row++)
        {
            var middle = firstRow + row * pitch;
            DrawBoardSlot(drawList, new Vector2(center, middle), BoardTileUnits * unit, BodySlots[row], gear,
                showEmpty, scale);
            DrawBoardSlot(drawList, new Vector2(center + side, middle), BoardAccessoryUnits * unit,
                AccessorySlots[row], gear, showEmpty, scale);
            var leftRow = row - BoardWeaponRow;
            if (leftRow >= 0 && leftRow < LeftSlots.Length)
            {
                DrawBoardSlot(drawList, new Vector2(center - side, middle), BoardAccessoryUnits * unit,
                    LeftSlots[leftRow], gear, showEmpty && LeftSlots[leftRow] != GearSlots.None, scale);
            }
        }
    }

    private static bool WearsAny(GlamGearDto[] gear, int[] slots)
    {
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (Array.IndexOf(slots, piece.Slot) >= 0 && (piece.ItemId != 0 || piece.Mods is { Length: > 0 }))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawBoardSlot(ImDrawListPtr drawList, Vector2 center, float size, int slot, GlamGearDto[] gear,
        bool showEmpty, float scale)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var tile = new Rect(center - half, center + half);
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            var matches = slot == GearSlots.None
                ? piece.ItemId == 0 && piece.Mods is { Length: > 0 }
                : piece.Slot == slot && piece.ItemId != 0;
            if (matches)
            {
                PrismArt.SlotTile(drawList, tile, slot, gameData.StainColor(piece.Stain0 != 0 ? piece.Stain0 : piece.Stain1),
                    scale);
                if (showEmpty)
                {
                    HoverTooltip.Show(tile, piece.ItemId == 0 ? ModCountLine()
                        : GearItem(piece.ItemId, out var name, out _) ? name : string.Empty);
                }

                return;
            }
        }

        if (showEmpty)
        {
            PrismArt.SlotTile(drawList, tile, slot, 0, scale, true);
        }
    }

    private void DrawWardrobeOutfit(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (wardrobe.Find(outfitId) is not { } outfit)
        {
            GoBack();
            return;
        }

        GateWardrobeSheet();
        SyncFit(outfit);
        var full = FullFrom(content, scale);
        var covered = outfit.Cover is { } cover && !wallpaperImages.Failed(cover.Path);
        var top = covered ? PhotoTop(full, content) : content.Min.Y;
        DrawFit(new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y)), outfit, covered,
            covered ? PhotoInset(full, content) : 0f, scale);
        DrawWardrobeSheet(full);
    }

    private void DrawFit(Rect body, WardrobeOutfit outfit, bool covered, float topInset, float scale)
    {
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var heroTop = new Vector2(origin.X, BodyTop());
        var hero = new Rect(heroTop, heroTop + new Vector2(width, width / GlamStore.PhotoRatio));
        var inset = Metrics.Space.Lg * scale;
        if (covered)
        {
            if (!DrawOutfitCover(list, hero, outfit.Cover))
            {
                PrismArt.Placeholder(list, hero, false, scale);
            }

            if (!PrismArt.Light)
            {
                PrismArt.TopFog(list, hero, scale);
            }

            if (UiInteract.Click(hero.Min, hero.Max, piecesOpen && UiInteract.Hover(hero.Min, hero.Max)))
            {
                piecesOpen = false;
            }

            DrawPiecesLayer(list, hero, topInset, outfit.Gear, scale);
            DrawPiecesChip(list, hero, scale);
        }
        else
        {
            PrismArt.Placeholder(list, hero, false, scale);
            var add = Loc.T(L.Prism.AddPhoto);
            var half = ButtonWidth([add], width - inset * 2f, scale) * 0.5f;
            var addTop = hero.Max.Y - inset - Button.LargeHeight * scale;
            var area = new Rect(new Vector2(hero.Min.X, hero.Min.Y + (Metrics.Size.TapTarget + Metrics.Space.Sm) * scale),
                new Vector2(hero.Max.X, addTop - Metrics.Space.Sm * scale));
            DrawOutfitBoard(list, hero, area, outfit.Gear, true, scale);
            if (DrawButton(list, new Rect(new Vector2(hero.Center.X - half, addTop),
                    new Vector2(hero.Center.X + half, addTop + Button.LargeHeight * scale)), add, ButtonStyle.Tinted,
                    scale))
            {
                PickOutfitCover(outfit);
            }
        }

        DrawPhotoBack(list, hero, topInset, scale);
        if (DrawPhotoButton(list, hero, topInset, 1, PhoneIcons.Dots, Loc.T(L.Prism.More), scale))
        {
            OpenFitSheet(outfit);
        }

        var left = origin.X + inset;
        var right = origin.X + width - inset;
        var cursor = DrawOutfitName(list, left, right, hero.Max.Y + inset, outfit, scale) + Metrics.Space.Xxs * scale;
        Typography.Draw(list, new Vector2(left, cursor), Typography.FitText(fitMeta, right - left, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        cursor += Typography.LineHeight(PrismType.Caption) + inset;
        cursor = DrawFitActions(list, left, right, cursor, outfit, scale);
        if (!glamour.HasGlamourer)
        {
            cursor += Metrics.Space.Sm * scale + Typography.DrawWrappedLeft(new Vector2(left, cursor + Metrics.Space.Sm * scale),
                Loc.T(L.Prism.TryOnWithoutGlamourer), PrismArt.Muted, PrismType.Caption, right - left);
        }

        cursor = DrawPieceSection(list, left, right, cursor + Metrics.Space.Xl * scale, outfit.Gear, scale);
        cursor = DrawModSection(list, left, right, cursor, outfit.Gear, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawFitActions(ImDrawListPtr drawList, float left, float right, float top, WardrobeOutfit outfit,
        float scale)
    {
        var reach = Metrics.Size.TapTarget * scale;
        var height = Button.LargeHeight * scale;
        var middle = top + height * 0.5f;
        var wearing = Wearing(outfit.Id);
        var wear = new Rect(new Vector2(left, top),
            new Vector2(right - reach * 2f - Metrics.Space.Sm * scale, top + height));
        if (DrawButton(drawList, wear, Loc.T(wearing ? L.Prism.TakeOff : L.Prism.PutOn), ButtonStyle.Solid, scale,
                id: "prism.putOn"))
        {
            if (wearing)
            {
                glamour.Revert();
            }
            else
            {
                WearOutfit(outfit);
            }
        }

        if (IconButton(drawList, new Vector2(right - reach * 1.5f, middle),
                outfit.Favourite ? PhoneIcons.StarFilled : PhoneIcons.Star,
                Loc.T(outfit.Favourite ? L.Prism.Unfavourite : L.Prism.Favourite), scale))
        {
            wardrobe.Update(outfit with { Favourite = !outfit.Favourite });
        }

        if (IconButton(drawList, new Vector2(right - reach * 0.5f, middle), PhoneIcons.Pencil, Loc.T(L.Prism.Edit), scale))
        {
            OpenFitSheet(outfit);
        }

        return top + height;
    }

    private void SyncFit(WardrobeOutfit outfit)
    {
        RefreshPieces(outfit.Id, outfit.Gear);
        if (ReferenceEquals(outfit, fitFor) && ReferenceEquals(fitLanguage, Loc.Current.Code))
        {
            return;
        }

        fitFor = outfit;
        fitLanguage = Loc.Current.Code;
        var saved = Loc.T(L.Prism.SavedOn, TimeText.MonthDay(outfit.CreatedAtUnix));
        fitMeta = totalPieces == 0 && totalMods == 0 ? saved
            : totalMods == 0 ? string.Concat(PieceCountLine(), GlamMetaSeparator, saved)
            : totalPieces == 0 ? string.Concat(ModCountLine(), GlamMetaSeparator, saved)
            : string.Concat(PieceCountLine(), GlamMetaSeparator, ModCountLine(), GlamMetaSeparator, saved);
    }

    private float DrawOutfitName(ImDrawListPtr drawList, float left, float right, float top, WardrobeOutfit outfit,
        float scale)
    {
        if (!renaming)
        {
            return top + PrismType.DrawHero(drawList, new Vector2(left, top), outfit.Name, right - left, PrismArt.Fore);
        }

        if (focusRename)
        {
            ImGui.SetKeyboardFocusHere();
            focusRename = false;
        }

        var done = Loc.T(L.Prism.Done);
        var doneWidth = ButtonWidth([done], (right - left) * 0.4f, scale);
        var bottom = DrawInput(drawList, left, top, right - left - doneWidth - Metrics.Space.Sm * scale, outfit.Name,
            "##outfitName", ref renameText, TitleMaxLength, scale);
        if (!DrawButton(drawList, new Rect(new Vector2(right - doneWidth, top), new Vector2(right, bottom)), done,
                ButtonStyle.Plain, scale))
        {
            return bottom;
        }

        renaming = false;
        var name = renameText.Trim();
        if (name.Length > 0 && name != outfit.Name)
        {
            wardrobe.Update(outfit with { Name = name });
        }

        return bottom;
    }

    private bool DrawOutfitCover(ImDrawListPtr drawList, Rect rect, WardrobeCover? cover)
    {
        if (cover is null || wallpaperImages.Get(cover.Path) is not { } texture)
        {
            return false;
        }

        var (uv0, uv1) = new WallpaperCrop(cover.Zoom, cover.CenterX, cover.CenterY)
            .ComputeUv(texture.Size, rect.Width / MathF.Max(1f, rect.Height));
        drawList.AddImage(texture.Handle, rect.Min, rect.Max, uv0, uv1);
        return true;
    }

    private void PickOutfitCover(WardrobeOutfit outfit)
    {
        Push(PrismScreen.WardrobeCover);
        coverFor = outfit.Id;
        composeSession.Open(true);
    }

    private void OpenFitSheet(WardrobeOutfit outfit)
    {
        sheetCount = 0;
        AddSheetAction(true, WardrobeAction.Rename);
        AddSheetAction(true, WardrobeAction.Photo);
        AddSheetAction(outfit.State.Length > 0 && glamour.HasGlamourer, WardrobeAction.Glamourer);
        AddSheetAction(true, WardrobeAction.Delete);
        OpenWardrobeSheet(outfit.Id);
    }

    private void AddSheetAction(bool shown, WardrobeAction action)
    {
        if (shown && sheetCount < SheetCapacity)
        {
            sheetActions[sheetCount++] = action;
        }
    }

    private void OpenWardrobeSheet(string outfit)
    {
        sheetScreen = screen;
        sheetOutfit = outfit;
        wardrobeSheet.Open();
    }

    private void GateWardrobeSheet()
    {
        if (sheetScreen == screen)
        {
            wardrobeSheet.Gate();
        }
        else if (wardrobeSheet.IsOpen)
        {
            wardrobeSheet.Close();
        }
    }

    private void DrawWardrobeSheet(Rect full)
    {
        if (sheetCount == 0 || sheetScreen != screen || !wardrobeSheet.CapturesPointer)
        {
            return;
        }

        for (var index = 0; index < sheetCount; index++)
        {
            var action = sheetActions[index];
            sheetItems[index] = new ActionSheet.Item(Loc.T(SheetLabel(action)), Danger: action == WardrobeAction.Delete);
        }

        var picked = wardrobeSheet.Draw(full, ActionSheetStyle.From(ui),
            new ReadOnlySpan<ActionSheet.Item>(sheetItems, 0, sheetCount), Loc.T(L.Common.Cancel), false);
        if (picked >= 0)
        {
            RunSheetAction(sheetActions[picked]);
        }
    }

    private LocString SheetLabel(WardrobeAction action) => action switch
    {
        WardrobeAction.Import => L.Prism.ImportDesigns,
        WardrobeAction.Surprise => L.Prism.SurpriseMe,
        WardrobeAction.Rename => L.Prism.Rename,
        WardrobeAction.Photo => wardrobe.Find(sheetOutfit)?.Cover is null ? L.Prism.AddPhoto : L.Prism.ChangePhoto,
        WardrobeAction.Glamourer => L.Prism.SaveToGlamourer,
        _ => L.Prism.DeleteOutfit,
    };

    private void RunSheetAction(WardrobeAction action)
    {
        if (action == WardrobeAction.Import)
        {
            if (wardrobe.StartImport(glamour) == 0)
            {
                ShellToast.Show(Loc.T(L.Prism.DesignsAdded, 0));
            }

            return;
        }

        if (action == WardrobeAction.Surprise)
        {
            var outfits = wardrobe.Outfits;
            if (outfits.Length > 0)
            {
                WearOutfit(outfits[roulette.Next(outfits.Length)]);
            }

            return;
        }

        if (wardrobe.Find(sheetOutfit) is not { } outfit)
        {
            return;
        }

        switch (action)
        {
            case WardrobeAction.Rename:
                renaming = true;
                focusRename = true;
                renameText = outfit.Name;
                break;
            case WardrobeAction.Photo:
                PickOutfitCover(outfit);
                break;
            case WardrobeAction.Glamourer:
                SaveToGlamourer(outfit);
                break;
            default:
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
                break;
        }
    }

    private void DrawWardrobeCover(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (wardrobe.Find(coverFor) is not { } outfit)
        {
            GoBack();
            return;
        }

        composeSession.ConsumePendingImport();
        var use = Loc.T(L.Prism.UsePhoto);
        var top = DrawActionHeader(drawList, content, Loc.T(L.Common.Cancel), Loc.T(L.Prism.AddPhoto), use, use,
            composeSession.HasSelection, UiSound.Tap, scale, out var chosen) + Metrics.Space.Sm * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, GlamStore.PhotoRatio, false, true, Loc.T(L.Prism.AddPhoto));
        if (chosen)
        {
            var crop = composeSession.CropsArray()[0];
            wardrobe.Update(outfit with
            {
                Cover = new WardrobeCover(composeSession.FirstSelected, crop.Zoom, crop.CenterX, crop.CenterY),
            });
            GoBack();
            return;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, content.Max.Y - PrismArt.BottomMargin(scale)));
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            composeSession.DrawPickGrid(gridRect, scale, PickStyle, false, Loc.T(L.Common.ImportFromPc),
                Loc.T(L.Prism.AddPhoto));
        }
    }

    private void WearOutfit(WardrobeOutfit outfit)
    {
        StartWearing(outfit.Id, outfit.Name);
        if (outfit.State.Length > 0 && glamour.ApplyState(outfit.State, outfit.Gear))
        {
            return;
        }

        if (outfit.DesignId != Guid.Empty && glamour.ApplyDesign(outfit.DesignId))
        {
            return;
        }

        glamour.TryOn(outfit.Gear);
    }

    private void SaveToGlamourer(WardrobeOutfit outfit)
    {
        if (outfit.DesignId != Guid.Empty && glamour.HasDesign(outfit.DesignId))
        {
            ShellToast.Show(Loc.T(L.Prism.Saved));
            return;
        }

        var designId = glamour.SaveDesign(outfit.State, outfit.Name);
        if (designId == Guid.Empty)
        {
            return;
        }

        wardrobe.Update(outfit with { DesignId = designId });
        ShellToast.Show(Loc.T(L.Prism.Saved));
    }

    private void SaveWearing()
    {
        wardrobe.Ensure();
        if (wardrobe.Loading)
        {
            return;
        }

        var gear = glamour.Capture();
        if (gear.Length == 0)
        {
            ShellToast.Show(Loc.T(L.Prism.NothingToSave));
            return;
        }

        var outfit = new WardrobeOutfit(Guid.NewGuid().ToString("N"), OutfitName(wardrobe.Outfits.Length + 1),
            glamour.ReadState(), Guid.Empty, gear, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        wardrobe.Add(outfit);
        OpenOutfit(outfit.Id);
        renaming = true;
        focusRename = true;
        renameText = outfit.Name;
    }

    private string OutfitName(int number)
    {
        var key = HashCode.Combine(number, Loc.Current.Code);
        return outfitNameText.IsCurrent(key)
            ? outfitNameText.Value
            : outfitNameText.Store(key, Loc.T(L.Prism.OutfitNumber, number));
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
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

internal readonly record struct FitPiece(GlamGearDto Piece, string Name, uint Icon, string Dyes, uint Dye0, uint Dye1,
    bool Owned);

internal readonly record struct FitMod(GlamModDto Mod, int Slot, string Author, bool Allowed, bool Installed);

internal sealed partial class PrismApp
{
    private const float BoardDesignUnits = 360f;
    private const float BoardTileUnits = 56f;
    private const float BoardAccessoryUnits = 44f;
    private const float BoardPitchUnits = 68f;
    private const float BoardColumnUnits = 96f;
    private const int BoardWeaponRow = 1;
    private const float PieceIconUnits = 40f;
    private const float PieceRowUnits = 56f;
    private const float OwnedMarkUnits = 4f;
    private const float DyeDotUnits = 4.5f;
    private const int SheetCapacity = 4;
    private const int CollagePieces = 4;

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
    private readonly List<FitPiece> fitPieces = new();
    private readonly List<FitMod> fitMods = new();
    private readonly HashSet<string> fitModKeys = new(StringComparer.Ordinal);
    private WardrobeOutfit? fitFor;
    private string? fitLanguage;
    private bool fitStorage;
    private bool fitMaybe;
    private string fitMeta = string.Empty;
    private string fitPiecesTitle = string.Empty;
    private string fitOwnedLine = string.Empty;
    private string fitModsTitle = string.Empty;
    private string fitModsLine = string.Empty;
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
            var inset = Metrics.Space.Lg * scale;
            var resume = ImGui.GetCursorScreenPos();
            EmptyState.Draw(new Rect(new Vector2(content.Min.X + inset, top), new Vector2(content.Max.X - inset,
                    content.Max.Y - bottomChrome)), ui, favouritesOnly ? FontAwesomeIcon.Star : FontAwesomeIcon.Tshirt,
                Loc.T(favouritesOnly ? L.Prism.FavouritesEmptyTitle : L.Prism.WardrobeEmptyTitle),
                Loc.T(favouritesOnly ? L.Prism.FavouritesEmptyHint : L.Prism.WardrobeEmptyHint));
            ImGui.SetCursorScreenPos(resume);
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
            if (IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Dots, TipMore,
                    Loc.T(L.Prism.More), scale))
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
        var saveWidth = ButtonWidth([save], float.MaxValue, scale);
        var labelled = left + Typography.Measure(heading, PrismType.Title).X + Metrics.Space.Md * scale <=
                       right - saveWidth;
        var saveLeft = labelled ? right - saveWidth : right - height;
        var saved = labelled
            ? DrawButton(drawList, new Rect(new Vector2(saveLeft, top), new Vector2(right, top + height)), save,
                ButtonStyle.Solid, scale)
            : IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Plus, TipSave, save, scale);
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
        var column = (width - gap * (TileColumns + 1)) / TileColumns;
        var height = column / GlamStore.PhotoRatio;
        var pitch = height + GridLine(scale) + gap;
        var outfits = wardrobe.Outfits;
        var busy = wardrobe.Loading || wardrobe.Importing;
        var count = busy ? SkeletonTileRows * TileColumns : outfits.Length;
        var shown = 0;
        for (var index = 0; index < count; index++)
        {
            if (!busy && favouritesOnly && !outfits[index].Favourite)
            {
                continue;
            }

            var min = new Vector2(origin.X + gap + shown % TileColumns * (column + gap), origin.Y + shown / TileColumns * pitch);
            var photo = new Rect(min, min + new Vector2(column, height));
            shown++;
            if (!ImGui.IsRectVisible(photo.Min, new Vector2(photo.Max.X, photo.Max.Y + GridLine(scale))))
            {
                continue;
            }

            if (busy)
            {
                PrismArt.Placeholder(list, photo, false, scale);
                continue;
            }

            var outfit = outfits[index];
            if (!DrawOutfitCover(list, photo, outfit.Cover))
            {
                PrismArt.Placeholder(list, photo, false, scale);
                if (outfit.Cover is null)
                {
                    DrawOutfitBoard(list, photo, photo, outfit.Gear, false, scale);
                }
            }

            var opened = DrawGridTile(list, photo, string.Empty, outfit.Name, string.Empty, null, string.Empty,
                PieceLabel(outfit.Gear), scale);
            if (opened)
            {
                OpenOutfit(outfit.Id);
            }
        }

        ImGui.Dummy(new Vector2(width,
            (shown + TileColumns - 1) / TileColumns * pitch + PrismArt.BottomMargin(scale)));
    }

    private void OpenOutfit(string id)
    {
        Push(PrismScreen.WardrobeOutfit);
        outfitId = id;
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
        var pitch = BoardPitchUnits * unit;
        var firstRow = area.Center.Y - pitch * (BodySlots.Length - 1) * 0.5f;
        var side = BoardColumnUnits * unit;
        for (var row = 0; row < BodySlots.Length; row++)
        {
            var middle = firstRow + row * pitch;
            DrawBoardSlot(drawList, new Vector2(area.Center.X, middle), BoardTileUnits * unit, BodySlots[row], gear,
                showEmpty, scale);
            DrawBoardSlot(drawList, new Vector2(area.Center.X + side, middle), BoardAccessoryUnits * unit,
                AccessorySlots[row], gear, showEmpty, scale);
            var leftRow = row - BoardWeaponRow;
            if (leftRow >= 0 && leftRow < LeftSlots.Length)
            {
                DrawBoardSlot(drawList, new Vector2(area.Center.X - side, middle), BoardAccessoryUnits * unit,
                    LeftSlots[leftRow], gear, showEmpty && LeftSlots[leftRow] != GearSlots.None, scale);
            }
        }
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
                    BoardTooltip(tile, piece);
                }

                return;
            }
        }

        if (showEmpty)
        {
            PrismArt.SlotTile(drawList, tile, slot, 0, scale, true);
        }
    }

    private void BoardTooltip(Rect tile, GlamGearDto piece)
    {
        for (var index = 0; index < fitPieces.Count; index++)
        {
            if (ReferenceEquals(fitPieces[index].Piece, piece))
            {
                HoverTooltip.Show(tile, fitPieces[index].Name);
                return;
            }
        }

        if (piece.ItemId == 0 && fitModsTitle.Length > 0)
        {
            HoverTooltip.Show(tile, fitModsTitle);
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

        cursor = DrawFitPieces(list, left, right, cursor, scale);
        cursor = DrawFitMods(list, left, right, cursor, scale);
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
                outfit.Favourite ? PhoneIcons.StarFilled : PhoneIcons.Star, TipFavourite,
                Loc.T(outfit.Favourite ? L.Prism.Unfavourite : L.Prism.Favourite), scale))
        {
            wardrobe.Update(outfit with { Favourite = !outfit.Favourite });
        }

        if (IconButton(drawList, new Vector2(right - reach * 0.5f, middle), PhoneIcons.Pencil, TipEdit,
                Loc.T(L.Prism.Edit), scale))
        {
            OpenFitSheet(outfit);
        }

        return top + height;
    }

    private float DrawFitPieces(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        if (fitPieces.Count == 0)
        {
            return top;
        }

        top = DrawSectionTitle(drawList, left, right, top + Metrics.Space.Xl * scale, fitPiecesTitle, fitOwnedLine,
            scale);
        if (fitMaybe)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top + Metrics.Space.Xxs * scale),
                Loc.T(L.Prism.OpenStorage), PrismArt.Muted, PrismType.Caption, right - left) + Metrics.Space.Xxs * scale;
        }

        for (var index = 0; index < fitPieces.Count; index++)
        {
            ImGui.PushID(index);
            top = DrawFitPiece(drawList, left, right, top, fitPieces[index], index, scale);
            ImGui.PopID();
        }

        return top;
    }

    private float DrawFitPiece(ImDrawListPtr drawList, float left, float right, float top, in FitPiece piece, int tip,
        float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + FitRowHeight(scale)));
        var reach = Metrics.Size.TapTarget * scale;
        var tryCenter = new Vector2(right - reach * 0.5f, row.Center.Y);
        var overTry = UiInteract.Hover(tryCenter - new Vector2(reach * 0.5f, reach * 0.5f),
            tryCenter + new Vector2(reach * 0.5f, reach * 0.5f));
        var hovered = UiInteract.Hover(row.Min, row.Max) && !overTry;
        DrawRowHover(drawList, row, hovered, scale);
        var icon = PieceIconUnits * scale;
        var iconMin = new Vector2(left, row.Center.Y - icon * 0.5f);
        GameIconTile.Draw(drawList, textures, piece.Icon, iconMin, iconMin + new Vector2(icon, icon),
            Metrics.Radius.Sm * scale, scale);
        var mark = new Vector2(iconMin.X + icon, iconMin.Y + icon);
        if (piece.Owned)
        {
            drawList.AddCircleFilled(mark, (OwnedMarkUnits + Metrics.Stroke.Thin) * scale, ImGui.GetColorU32(PrismArt.Back));
            drawList.AddCircleFilled(mark, OwnedMarkUnits * scale, ImGui.GetColorU32(PrismArt.Fore));
        }
        else if (fitMaybe)
        {
            drawList.AddCircleFilled(mark, (OwnedMarkUnits + Metrics.Stroke.Thin) * scale, ImGui.GetColorU32(PrismArt.Back));
            drawList.AddCircle(mark, OwnedMarkUnits * scale, ImGui.GetColorU32(PrismArt.Fore), 0, Metrics.Stroke.Thin * scale);
        }

        if (IconButton(drawList, tryCenter, PhoneIcons.Wand, TipPieceFirst + tip, Loc.T(L.Prism.TryOnGlam), scale,
                PrismArt.Muted))
        {
            glamour.TryOnPiece(piece.Piece);
            StartWearing(string.Empty, piece.Name);
        }

        var textLeft = iconMin.X + icon + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, tryCenter.X - reach * 0.5f - Metrics.Space.Sm * scale - textLeft);
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var textTop = row.Center.Y - (nameHeight + (piece.Dyes.Length > 0 ? captionHeight : 0f)) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(piece.Name, room, PrismType.Headline),
            PrismArt.Fore, PrismType.Headline);
        if (piece.Dyes.Length > 0)
        {
            var dot = DyeDotUnits * scale;
            var middle = textTop + nameHeight + captionHeight * 0.5f;
            var x = textLeft + dot;
            x = DrawDyeDot(drawList, x, middle, dot, piece.Dye0, scale);
            x = DrawDyeDot(drawList, x, middle, dot, piece.Dye1, scale);
            var dyeLeft = x - dot + Metrics.Space.Xs * scale;
            Typography.Draw(drawList, new Vector2(dyeLeft, textTop + nameHeight),
                Typography.FitText(piece.Dyes, MathF.Max(1f, textLeft + room - dyeLeft), PrismType.Caption),
                PrismArt.Muted, PrismType.Caption);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenItemGlams(piece.Piece.ItemId);
        }

        return row.Max.Y;
    }

    private static float DrawDyeDot(ImDrawListPtr drawList, float x, float middle, float radius, uint rgb, float scale)
    {
        if (rgb == 0)
        {
            return x;
        }

        drawList.AddCircleFilled(new Vector2(x, middle), radius, ImGui.GetColorU32(Palette.FromRgb(rgb)));
        drawList.AddCircle(new Vector2(x, middle), radius, ImGui.GetColorU32(PrismArt.Faint), 0,
            Metrics.Stroke.Hairline * scale);
        return x + radius * 2f + Metrics.Space.Xxs * scale;
    }

    private float DrawFitMods(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        if (fitMods.Count == 0)
        {
            return top;
        }

        top = DrawSectionTitle(drawList, left, right, top + Metrics.Space.Xl * scale, fitModsTitle, fitModsLine, scale);
        var icon = PieceIconUnits * scale;
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        for (var index = 0; index < fitMods.Count; index++)
        {
            var mod = fitMods[index];
            var row = new Rect(new Vector2(left, top), new Vector2(right, top + FitRowHeight(scale)));
            var hovered = UiInteract.Hover(row.Min, row.Max);
            DrawRowHover(drawList, row, hovered, scale);
            var iconMin = new Vector2(left, row.Center.Y - icon * 0.5f);
            PrismArt.SlotTile(drawList, new Rect(iconMin, iconMin + new Vector2(icon, icon)), mod.Slot, 0, scale);
            var action = mod.Installed ? Loc.T(L.Prism.Installed) : mod.Allowed ? Loc.T(L.Prism.GetMod) : string.Empty;
            var actionWidth = Typography.Measure(action, PrismType.Caption).X;
            Typography.Draw(drawList, new Vector2(right - actionWidth, row.Center.Y - captionHeight * 0.5f), action,
                PrismArt.Muted, PrismType.Caption);
            var textLeft = iconMin.X + icon + Metrics.Space.Md * scale;
            var room = MathF.Max(1f, right - textLeft - (actionWidth > 0f ? actionWidth + Metrics.Space.Md * scale : 0f));
            var textTop = row.Center.Y - (nameHeight + (mod.Author.Length > 0 ? captionHeight : 0f)) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, textTop),
                Typography.FitText(mod.Mod.Name, room, PrismType.Headline), PrismArt.Fore, PrismType.Headline);
            Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
                Typography.FitText(mod.Author, room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                if (mod.Allowed)
                {
                    UrlActions.AskThenOpen(mod.Mod.Link);
                }
                else
                {
                    OpenModGlams(mod.Mod);
                }
            }

            top = row.Max.Y;
        }

        return top;
    }

    private static float FitRowHeight(float scale) => MathF.Max(PieceRowUnits * scale,
        Typography.LineHeight(PrismType.Headline) + Typography.LineHeight(PrismType.Caption) + Metrics.Space.Sm * scale);

    private void SyncFit(WardrobeOutfit outfit)
    {
        var storage = OwnedItems.StorageLoaded;
        if (ReferenceEquals(outfit, fitFor) && ReferenceEquals(fitLanguage, Loc.Current.Code) && storage == fitStorage)
        {
            return;
        }

        fitFor = outfit;
        fitLanguage = Loc.Current.Code;
        fitStorage = storage;
        fitPieces.Clear();
        fitMods.Clear();
        fitModKeys.Clear();
        var owned = 0;
        var gear = outfit.Gear;
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (piece.ItemId != 0 && gameData.TryGetItem(piece.ItemId, out var name, out var icon, out _))
            {
                var mine = OwnedItems.Owns(piece.ItemId);
                owned += mine ? 1 : 0;
                fitPieces.Add(new FitPiece(piece, name, icon, FitDyes(piece.Stain0, piece.Stain1),
                    gameData.StainColor(piece.Stain0), gameData.StainColor(piece.Stain1), mine));
            }

            var mods = piece.Mods ?? Array.Empty<GlamModDto>();
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                var mod = mods[modIndex];
                if (fitModKeys.Add(PostedMods.Key(mod)))
                {
                    fitMods.Add(new FitMod(mod, piece.ItemId == 0 ? GearSlots.None : piece.Slot, mod.Author,
                        ModLinks.IsAllowed(mod.Link), glamour.HasMod(mod)));
                }
            }
        }

        var installed = 0;
        for (var index = 0; index < fitMods.Count; index++)
        {
            installed += fitMods[index].Installed ? 1 : 0;
        }

        fitMaybe = !storage && owned < fitPieces.Count;
        fitPiecesTitle = Loc.Plural(L.Prism.PieceCount, fitPieces.Count);
        fitOwnedLine = Loc.Plural(L.Prism.OwnPieces, fitPieces.Count, owned);
        fitModsTitle = Loc.Plural(L.Prism.ModCount, fitMods.Count);
        fitModsLine = glamour.HasPenumbra
            ? Loc.T(L.Prism.ModsInstalled, installed, fitMods.Count)
            : Loc.T(L.Prism.ModsNeedPenumbra);
        var saved = Loc.T(L.Prism.SavedOn, TimeText.MonthDay(outfit.CreatedAtUnix));
        fitMeta = fitPieces.Count == 0 && fitMods.Count == 0 ? saved
            : fitMods.Count == 0 ? string.Concat(fitPiecesTitle, MetaSeparator, saved)
            : fitPieces.Count == 0 ? string.Concat(fitModsTitle, MetaSeparator, saved)
            : string.Concat(fitPiecesTitle, MetaSeparator, fitModsTitle, MetaSeparator, saved);
    }

    private string FitDyes(byte stain0, byte stain1)
    {
        var first = gameData.StainName(stain0);
        var second = gameData.StainName(stain1);
        return first.Length == 0 ? second : second.Length == 0 ? first : string.Concat(first, ", ", second);
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
        if (sheetCount == 0 || sheetScreen != screen)
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
        var top = DrawActionHeader(drawList, content, Loc.T(L.Prism.AddPhoto), true, use, use,
            composeSession.HasSelection, scale, out var chosen) + Metrics.Space.Sm * scale;
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

using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
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
}

internal sealed partial class PrismApp
{
    private const string FitLogoArtwork = "prism.fg";
    private const float FitLogoShare = 0.6f;
    private const float FitLogoAlpha = 0.9f;
    private const float GridLogoShare = 0.42f;
    private const float GridLogoHeightShare = 0.42f;
    private const int SheetCapacity = 2;
    private const float NewOutfitGlyphUnits = 14f;

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
    private string coverFor = string.Empty;
    private int sheetCount;
    private PrismScreen sheetScreen;

    private void DrawWardrobe(ImDrawListPtr drawList, Rect full, Rect content, float scale)
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
                favouritesOnly ? string.Empty : Loc.T(L.Prism.NewOutfit), ButtonStyle.Solid, scale, out var create);
            if (create)
            {
                StartOutfitEdit(null);
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
                OpenWardrobeSheet();
            }

            right -= height + Metrics.Space.Xs * scale;
        }

        var create = Loc.T(L.Prism.NewOutfit);
        var heading = Loc.T(L.Prism.TabWardrobe);
        var pillHeight = Button.RegularHeight * scale;
        var labelStyle = Button.LabelStyle(pillHeight);
        var glyph = NewOutfitGlyphUnits * scale;
        var gap = Metrics.Space.Xs * scale;
        var textWidth = Typography.Measure(create, labelStyle).X;
        var createWidth = glyph + gap + textWidth + Metrics.Space.Md * 2f * scale;
        var labelled = left + Typography.Measure(heading, PrismType.Title).X + Metrics.Space.Md * scale <=
                       right - createWidth;
        var createLeft = labelled ? right - createWidth : right - height;
        bool created;
        if (labelled)
        {
            var hit = new Rect(new Vector2(createLeft, top), new Vector2(right, top + height));
            var hovered = UiInteract.Hover(hit.Min, hit.Max);
            var pill = new Rect(new Vector2(createLeft, middle - pillHeight * 0.5f),
                new Vector2(right, middle + pillHeight * 0.5f));
            var face = Button.Surface(drawList, pill, controls, ButtonStyle.Solid, ButtonRole.Normal, true, hovered,
                ImGui.GetID("prism.newOutfit"));
            var x = face.Face.Center.X - (glyph + gap + textWidth) * 0.5f;
            PhoneIcon.Draw(drawList, new Vector2(x + glyph * 0.5f, face.Face.Center.Y), PhoneIcons.Plus, face.LabelInk,
                glyph);
            Typography.Draw(drawList,
                new Vector2(x + glyph + gap, face.Face.Center.Y - Typography.LineHeight(labelStyle) * 0.5f), create,
                face.LabelInk, labelStyle);
            created = UiInteract.Click(hit.Min, hit.Max, hovered);
        }
        else
        {
            created = IconButton(drawList, new Vector2(right - height * 0.5f, middle), PhoneIcons.Plus, create, scale);
        }

        if (created)
        {
            StartOutfitEdit(null);
        }

        var title = Typography.FitText(heading, MathF.Max(1f, createLeft - Metrics.Space.Md * scale - left),
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
                AppIconTextures.TryDrawArtwork(list, FitLogoArtwork,
                    new Vector2(photo.Center.X, photo.Min.Y + photo.Height * GridLogoHeightShare),
                    photo.Width * GridLogoShare, Vector4.One with { W = FitLogoAlpha });
            }

            if (DrawGridTile(list, mark, photo, string.Empty, outfit.Name, string.Empty, PieceLabel(outfit.Gear),
                    scale))
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
    }

    private string PieceLabel(GlamGearDto[] gear)
    {
        var pieces = CountPieces(gear);
        return pieces > 0 ? CountLabel(pieces) : string.Empty;
    }

    private void DrawWardrobeOutfit(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (wardrobe.Find(outfitId) is not { } outfit)
        {
            GoBack();
            return;
        }

        GateWardrobeSheet();
        SyncFit(outfit);
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
            AppIconTextures.TryDrawArtwork(list, FitLogoArtwork, hero.Center, hero.Width * FitLogoShare,
                Vector4.One with { W = FitLogoAlpha });
            var radius = Metrics.Size.TapTarget * 0.5f * scale;
            if (RoundButton.Icon(list, new Vector2(hero.Center.X, hero.Max.Y - inset - radius), radius,
                    PhoneIcons.PhotoPlus, controls, ButtonStyle.Gray, Loc.T(L.Prism.AddPhoto)))
            {
                PickOutfitCover(outfit);
            }
        }

        DrawPhotoBack(list, hero, topInset, scale);

        var left = origin.X + inset;
        var right = origin.X + width - inset;
        var nameTop = hero.Max.Y + inset;
        var cursor = nameTop + PrismType.DrawHero(list, new Vector2(left, nameTop), outfit.Name, right - left,
            PrismArt.Fore) + Metrics.Space.Xxs * scale;
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

        if (IconButton(drawList, new Vector2(right - reach * 0.5f, middle), PhoneIcons.Pencil,
                Loc.T(L.Prism.EditOutfit), scale))
        {
            StartOutfitEdit(outfit);
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
        coverForEditor = false;
        Push(PrismScreen.WardrobeCover);
        coverFor = outfit.Id;
        composeSession.Open(true);
    }

    private void AddSheetAction(bool shown, WardrobeAction action)
    {
        if (shown && sheetCount < SheetCapacity)
        {
            sheetActions[sheetCount++] = action;
        }
    }

    private void OpenWardrobeSheet()
    {
        sheetScreen = screen;
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
            sheetItems[index] = new ActionSheet.Item(Loc.T(SheetLabel(sheetActions[index])));
        }

        var picked = wardrobeSheet.Draw(full, ActionSheetStyle.From(ui),
            new ReadOnlySpan<ActionSheet.Item>(sheetItems, 0, sheetCount), Loc.T(L.Common.Cancel), false);
        if (picked >= 0)
        {
            RunSheetAction(sheetActions[picked]);
        }
    }

    private static LocString SheetLabel(WardrobeAction action) =>
        action == WardrobeAction.Import ? L.Prism.ImportDesigns : L.Prism.SurpriseMe;

    private void RunSheetAction(WardrobeAction action)
    {
        if (action == WardrobeAction.Import)
        {
            OpenDesignPicker();
            return;
        }

        var outfits = wardrobe.Outfits;
        if (outfits.Length > 0)
        {
            WearOutfit(outfits[roulette.Next(outfits.Length)]);
        }
    }

    private void DrawWardrobeCover(ImDrawListPtr drawList, Rect content, float scale)
    {
        var outfit = coverForEditor ? null : wardrobe.Find(coverFor);
        if (!coverForEditor && outfit is null)
        {
            GoBack();
            return;
        }

        if (!DrawPhotoPick(drawList, content, Loc.T(L.Prism.AddPhoto), GlamStore.PhotoRatio, scale))
        {
            return;
        }

        var crop = composeSession.CropsArray()[0];
        var cover = new WardrobeCover(composeSession.FirstSelected, crop.Zoom, crop.CenterX, crop.CenterY);
        if (outfit is null)
        {
            outfitCoverDraft = cover;
        }
        else
        {
            wardrobe.Update(outfit with { Cover = cover });
        }

        GoBack();
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

    private string OutfitName(int number)
    {
        var key = HashCode.Combine(number, Loc.Current.Code);
        return outfitNameText.IsCurrent(key)
            ? outfitNameText.Value
            : outfitNameText.Store(key, Loc.T(L.Prism.OutfitNumber, number));
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float CollageIconUnits = 40f;
    private const int CollagePieces = 4;

    private readonly WardrobeStore wardrobe;
    private readonly Random roulette = new();
    private string outfitId = string.Empty;
    private bool favouritesOnly;
    private CachedText outfitNameText;
    private bool renaming;
    private bool focusRename;
    private string renameText = string.Empty;
    private string coverFor = string.Empty;

    private void DrawWardrobe(ImDrawListPtr drawList, Rect content, float scale)
    {
        wardrobe.Ensure();
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var top = DrawMasthead(drawList, content, Loc.T(L.Prism.TabWardrobe), string.Empty, string.Empty, scale, out _);
        var save = Loc.T(L.Prism.SaveWearing);
        var height = PrismArt.StripUnits * scale;
        var saveRect = new Rect(new Vector2(left, top), new Vector2(right, top + height));
        var bottom = saveRect.Max.Y;
        if (glamour.HasGlamourer)
        {
            var import = Loc.T(L.Prism.ImportDesigns);
            var gap = Metrics.Space.Md * scale;
            var half = (right - left - gap) * 0.5f;
            var inset = height * 0.32f * 2f;
            var labelPixels = PrismArt.LabelPixels();
            var stacked = PrismType.Measure(save, PrismFace.Label, labelPixels).X + inset > half ||
                          PrismType.Measure(import, PrismFace.Label, labelPixels).X + inset > half;
            var importRect = stacked
                ? new Rect(new Vector2(left, bottom + gap), new Vector2(right, bottom + gap + height))
                : new Rect(new Vector2(left + half + gap, top), new Vector2(right, bottom));
            if (!stacked)
            {
                saveRect = new Rect(saveRect.Min, new Vector2(left + half, bottom));
            }

            bottom = importRect.Max.Y;
            if (PrismArt.Strip(drawList, importRect, import, string.Empty, false, !wardrobe.Importing) &&
                wardrobe.StartImport(glamour) == 0)
            {
                ShellToast.Show(Loc.T(L.Prism.DesignsAdded, 0));
            }
        }

        if (PrismArt.Strip(drawList, saveRect, save, "+", true))
        {
            SaveWearing();
        }

        var imported = wardrobe.PumpImport(glamour);
        if (imported >= 0)
        {
            ShellToast.Show(Loc.T(L.Prism.DesignsAdded, imported));
        }

        top = bottom + Metrics.Space.Lg * scale;
        top = DrawWardrobeFilters(drawList, left, right, top, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var cursor = DrawOutfitGrid(list, origin.X + gutter, origin.Y, width, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawWardrobeFilters(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var x = left;
        for (var index = 0; index < 2; index++)
        {
            var favourites = index == 1;
            var label = Loc.T(favourites ? L.Prism.Favourites : L.Prism.FilterAll);
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            var active = favouritesOnly == favourites;
            if (PrismArt.Link(drawList, new Vector2(x, top), label, active ? PrismArt.Fore : PrismArt.Muted, scale))
            {
                favouritesOnly = favourites;
            }

            if (active)
            {
                PrismArt.Hairline(drawList, x, x + width, top + labelLine, PrismArt.Fore, scale);
            }

            x += width + Metrics.Space.Lg * scale;
        }

        var surprise = Loc.T(L.Prism.SurpriseMe);
        if (wardrobe.Outfits.Length > 1 &&
            PrismArt.Link(drawList, new Vector2(right - PrismType.Measure(surprise, PrismFace.Label, labelPixels).X, top),
                surprise, PrismArt.Muted, scale))
        {
            var outfits = wardrobe.Outfits;
            WearOutfit(outfits[roulette.Next(outfits.Length)]);
        }

        return top + labelLine + Metrics.Space.Lg * scale;
    }

    private float DrawOutfitGrid(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var outfits = wardrobe.Outfits;
        if (wardrobe.Loading || wardrobe.Importing)
        {
            PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Common.Loading), PrismArt.Muted, PrismFace.Label,
                PrismArt.LabelPixels());
            return top + PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
        }

        if (outfits.Length == 0)
        {
            var hint = PrismType.Sentences(Loc.T(L.Prism.WardrobeEmptyHint), TextStyles.Subheadline, width);
            return top + Typography.DrawWrappedLeft(new Vector2(left, top), hint, PrismArt.Muted, TextStyles.Subheadline,
                PrismType.BalancedWidth(hint, TextStyles.Subheadline, width));
        }

        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (GridColumns - 1)) / GridColumns;
        var height = column / GlamStore.PhotoRatio;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = CollageIconUnits * scale;
        var shown = 0;
        for (var index = 0; index < outfits.Length; index++)
        {
            var outfit = outfits[index];
            if (favouritesOnly && !outfit.Favourite)
            {
                continue;
            }

            var x = left + shown % GridColumns * (column + gap);
            var y = top + shown / GridColumns * (height + gap);
            shown++;
            var min = new Vector2(x, y);
            var max = new Vector2(x + column, y + height);
            if (!ImGui.IsRectVisible(min, max))
            {
                continue;
            }

            var covered = DrawOutfitCover(drawList, new Rect(min, max), outfit.Cover);
            if (covered)
            {
                drawList.AddRectFilledMultiColor(new Vector2(x, max.Y - height * TileShadeShare), max,
                    ImGui.GetColorU32(TileClear), ImGui.GetColorU32(TileClear), ImGui.GetColorU32(TileShade),
                    ImGui.GetColorU32(TileShade));
            }
            else
            {
                PrismArt.Swatch(drawList, new Rect(min, max), (int)(outfit.CreatedAtUnix % int.MaxValue));
            }

            var placed = covered ? CollagePieces : 0;
            for (var pieceIndex = 0; pieceIndex < outfit.Gear.Length && placed < CollagePieces; pieceIndex++)
            {
                if (!GearItem(outfit.Gear[pieceIndex].ItemId, out _, out var iconId))
                {
                    continue;
                }

                var iconMin = new Vector2(min.X + gap + placed % 2 * (icon + gap), min.Y + gap + placed / 2 * (icon + gap));
                GameIconTile.Draw(drawList, textures, iconId, iconMin, iconMin + new Vector2(icon, icon), 0f, scale);
                placed++;
            }

            drawList.PushClipRect(min, max, true);
            PrismType.Draw(drawList, new Vector2(x + gap, max.Y - gap - labelLine),
                PrismType.FitLabel(outfit.Name, labelPixels, column - gap * 2f), PrismArt.PhotoFore, PrismFace.Label,
                labelPixels);
            drawList.PopClipRect();
            var hovered = UiInteract.Hover(min, max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                Push(PrismScreen.WardrobeOutfit);
                outfitId = outfit.Id;
                renaming = false;
            }
        }

        if (favouritesOnly && shown == 0)
        {
            var hint = PrismType.Sentences(Loc.T(L.Prism.FavouritesEmptyHint), TextStyles.Subheadline, width);
            return top + Typography.DrawWrappedLeft(new Vector2(left, top), hint, PrismArt.Muted, TextStyles.Subheadline,
                PrismType.BalancedWidth(hint, TextStyles.Subheadline, width));
        }

        var rows = (shown + GridColumns - 1) / GridColumns;
        return top + rows * (height + gap);
    }

    private void DrawWardrobeOutfit(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (wardrobe.Find(outfitId) is not { } outfit)
        {
            GoBack();
            return;
        }

        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var gutter = PrismArt.Gutter(scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = origin.Y;
        if (outfit.Cover is { } cover && !wallpaperImages.Failed(cover.Path))
        {
            var photo = new Rect(new Vector2(left, cursor),
                new Vector2(left + width, cursor + width / GlamStore.PhotoRatio));
            list.AddRectFilled(photo.Min, photo.Max,
                ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, PickFillAlpha)));
            DrawOutfitCover(list, photo, cover);
            cursor = photo.Max.Y + Metrics.Space.Lg * scale;
        }

        cursor = DrawOutfitName(list, left, cursor, width, outfit, scale);
        var height = PrismArt.StripUnits * scale;
        var gap = Metrics.Space.Md * scale;
        var half = (width - gap) * 0.5f;
        var wear = new Rect(new Vector2(left, cursor), new Vector2(left + half, cursor + height));
        var favourite = new Rect(new Vector2(left + half + gap, cursor), new Vector2(left + width, cursor + height));
        var wearing = Wearing(outfit.Id);
        if (PrismArt.Strip(list, wear, Loc.T(wearing ? L.Prism.TakeOff : L.Prism.PutOn), string.Empty, true))
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

        if (PrismArt.Strip(list, favourite, Loc.T(outfit.Favourite ? L.Prism.Unfavourite : L.Prism.Favourite),
                string.Empty, false))
        {
            wardrobe.Update(outfit with { Favourite = !outfit.Favourite });
        }

        cursor = wear.Max.Y + Metrics.Space.Md * scale;
        if (!glamour.HasGlamourer)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.TryOnWithoutGlamourer),
                PrismArt.Muted, TextStyles.Footnote, width);
        }

        cursor += Metrics.Space.Lg * scale;
        cursor = DrawSectionLabel(list, left, cursor, width, 0, Loc.T(L.Prism.GearHeading), scale);
        cursor = DrawGearList(list, left, cursor, width, outfit.Gear, true, scale) + Metrics.Space.Lg * scale;
        if (outfit.State.Length > 0 && glamour.HasGlamourer &&
            PrismArt.Link(list, new Vector2(left, cursor), Loc.T(L.Prism.SaveToGlamourer), PrismArt.Muted, scale))
        {
            SaveToGlamourer(outfit);
        }

        cursor += Metrics.Size.TapTarget * scale;
        if (PrismArt.Link(list, new Vector2(left, cursor), Loc.T(L.Prism.DeleteOutfit), PrismArt.Muted, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Prism.DeleteOutfitTitle),
                Message = outfit.Name,
                ConfirmLabel = Loc.T(L.Common.Delete),
                CancelLabel = Loc.T(L.Common.Cancel),
                Danger = true,
                Confirm = () =>
                {
                    wardrobe.Remove(outfit.Id);
                    GoBack();
                },
            });
        }

        cursor += PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawOutfitName(ImDrawListPtr drawList, float left, float top, float width, WardrobeOutfit outfit,
        float scale)
    {
        if (renaming)
        {
            if (focusRename)
            {
                ImGui.SetKeyboardFocusHere();
                focusRename = false;
            }

            top = DrawField(drawList, left, top, width, Loc.T(L.Prism.TitleLabel), outfit.Name, "##outfitName",
                ref renameText, TitleMaxLength, scale);
            if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Prism.Done), PrismArt.Fore, scale))
            {
                renaming = false;
                var name = renameText.Trim();
                if (name.Length > 0 && name != outfit.Name)
                {
                    wardrobe.Update(outfit with { Name = name });
                }
            }

            return top + Metrics.Size.TapTarget * scale;
        }

        var pixels = PrismType.Fit(outfit.Name, PrismFace.Headline, width, DetailTitleUnits * scale,
            DetailTitleMinUnits * scale, 2, out var lines);
        top += PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore, PrismFace.Headline, pixels) +
               Metrics.Space.Xs * scale;
        var rename = Loc.T(L.Prism.Rename);
        if (PrismArt.Link(drawList, new Vector2(left, top), rename, PrismArt.Muted, scale))
        {
            renaming = true;
            focusRename = true;
            renameText = outfit.Name;
        }

        var photoLeft = left + PrismType.Measure(rename, PrismFace.Label, PrismArt.LabelPixels()).X +
                        Metrics.Space.Xl * scale;
        if (PrismArt.Link(drawList, new Vector2(photoLeft, top),
                Loc.T(outfit.Cover is null ? L.Prism.AddPhoto : L.Prism.ChangePhoto), PrismArt.Muted, scale))
        {
            Push(PrismScreen.WardrobeCover);
            coverFor = outfit.Id;
            composeSession.Open(true);
        }

        return top + PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) + Metrics.Space.Lg * scale;
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

    private void DrawWardrobeCover(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (wardrobe.Find(coverFor) is not { } outfit)
        {
            GoBack();
            return;
        }

        composeSession.ConsumePendingImport();
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, GlamStore.PhotoRatio, false, true, Loc.T(L.Prism.AddPhoto));
        var strip = StripRect(content, scale);
        if (PrismArt.Strip(drawList, strip, Loc.T(L.Prism.UsePhoto), string.Empty, true, composeSession.HasSelection))
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
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale));
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
        Push(PrismScreen.WardrobeOutfit);
        outfitId = outfit.Id;
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

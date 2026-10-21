using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
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

    private void DrawWardrobe(ImDrawListPtr drawList, Rect content, float scale)
    {
        wardrobe.Ensure();
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var top = content.Min.Y + HeaderGapUnits * scale;
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.TabWardrobe), PrismArt.Fore, PrismFace.Headline,
            MastheadUnits * scale);
        top += PrismType.LineHeight(PrismFace.Headline, MastheadUnits * scale) + Metrics.Space.Md * scale;
        PrismArt.Hairline(drawList, left, right, top, PrismArt.Fore, scale);
        top += Metrics.Space.Lg * scale;
        var save = Loc.T(L.Prism.SaveWearing);
        var x = left;
        if (PrismArt.Button(drawList, new Vector2(x, top), save, scale, out var saveWidth))
        {
            SaveWearing();
        }

        x += saveWidth + Metrics.Space.Sm * scale;
        if (glamour.HasGlamourer)
        {
            var import = Loc.T(L.Prism.ImportDesigns);
            if (PrismArt.Button(drawList, new Vector2(x, top), import, scale, out _))
            {
                wardrobe.ImportDesigns(glamour);
            }
        }

        top += PrismArt.ButtonUnits * scale + Metrics.Space.Lg * scale;
        top = DrawWardrobeFilters(drawList, left, right, top, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
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

            PrismArt.Swatch(drawList, new Rect(min, max), index);
            var placed = 0;
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
            PrismType.Draw(drawList, new Vector2(x + gap, max.Y - gap - labelLine), outfit.Name, PrismArt.PhotoFore,
                PrismFace.Label, labelPixels);
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
            }
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
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var pixels = PrismType.Fit(outfit.Name, PrismFace.Headline, width, DetailTitleUnits * scale,
            DetailTitleMinUnits * scale, 2, out var lines);
        var cursor = origin.Y + PrismType.DrawLines(list, new Vector2(left, origin.Y), lines, PrismArt.Fore,
            PrismFace.Headline, pixels) + Metrics.Space.Lg * scale;
        var height = PrismArt.StripUnits * scale;
        var gap = Metrics.Space.Md * scale;
        var half = (width - gap) * 0.5f;
        var wear = new Rect(new Vector2(left, cursor), new Vector2(left + half, cursor + height));
        var favourite = new Rect(new Vector2(left + half + gap, cursor), new Vector2(left + width, cursor + height));
        var wearing = Wearing(outfit.Id);
        if (PrismArt.Strip(list, wear, Loc.T(wearing ? L.Prism.TakeOff : L.Prism.PutOn), Arrow, true))
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

        cursor = wear.Max.Y + Metrics.Space.Xl * scale;
        cursor = DrawSectionLabel(list, left, cursor, width, 0, Loc.T(L.Prism.GearHeading), scale);
        cursor = DrawGearList(list, left, cursor, width, outfit.Gear, true, scale) + Metrics.Space.Lg * scale;
        if (outfit.State.Length > 0 && glamour.HasGlamourer &&
            PrismArt.Link(list, new Vector2(left, cursor), Loc.T(L.Prism.SaveToGlamourer), PrismArt.Muted, scale))
        {
            glamour.SaveDesign(outfit.State, outfit.Name);
        }

        cursor += PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) + Metrics.Space.Md * scale;
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

    private void WearOutfit(WardrobeOutfit outfit)
    {
        StartWearing(outfit.Id, outfit.Name);
        if (outfit.DesignId != Guid.Empty && glamour.ApplyDesign(outfit.DesignId))
        {
            return;
        }

        if (outfit.State.Length > 0 && glamour.ApplyState(outfit.State, outfit.Gear))
        {
            return;
        }

        glamour.TryOn(outfit.Gear);
    }

    private void SaveWearing()
    {
        wardrobe.Ensure();
        var gear = glamour.Capture();
        if (gear.Length == 0)
        {
            return;
        }

        var name = OutfitName(wardrobe.Outfits.Length + 1);
        wardrobe.Add(new WardrobeOutfit(Guid.NewGuid().ToString("N"), name, glamour.ReadState(), Guid.Empty, gear, false,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    private string OutfitName(int number)
    {
        var key = HashCode.Combine(number, Loc.Current.Code);
        return outfitNameText.IsCurrent(key)
            ? outfitNameText.Value
            : outfitNameText.Store(key, Loc.T(L.Prism.OutfitNumber, number));
    }
}

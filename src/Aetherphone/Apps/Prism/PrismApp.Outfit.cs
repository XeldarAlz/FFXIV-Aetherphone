using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float GearIconUnits = 32f;
    private const float GlamPhotoShare = 0.55f;

    private readonly List<PrismGearRow> outfitRows = new();
    private readonly List<PrismGearRow> glamRows = new();
    private PrismGearDto[] wornOutfit = Array.Empty<PrismGearDto>();
    private PrismScreen outfitReturn = PrismScreen.Cover;
    private PrismPlacingDto? glamPlacing;
    private int tryOnNext = int.MaxValue;

    private void OpenOutfit()
    {
        wornOutfit = EquippedOutfit.Read();
        PrismOutfit.Fill(outfitRows, wornOutfit, gameData);
        outfitReturn = screen;
        Go(PrismScreen.Outfit);
    }

    private void OpenGlam(PrismPlacingDto placing)
    {
        glamPlacing = placing;
        PrismOutfit.Fill(glamRows, placing.Outfit, gameData);
        tryOnNext = int.MaxValue;
        Go(PrismScreen.Glam);
    }

    private void TapForOutfit(Vector2 min, Vector2 max, PrismPlacingDto placing)
    {
        if (!PrismOutfit.Has(placing.Outfit))
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
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var entry = store.Week is { Phase: PrismPhases.Entering, MyEntry: { } mine } ? mine : null;
        var shared = entry is null ? configuration.PrismShareOutfit : PrismOutfit.Has(entry.Outfit);
        var strips = shared && entry is not null ? 2 : 1;
        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, StripRect(content, scale, strips - 1).Min.Y - Metrics.Space.Lg * scale));
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var gutter = PrismArt.Gutter(scale);
            var width = ScrollLayout.StableContentWidth() - gutter * 2f;
            var left = origin.X + gutter;
            var cursor = DrawSectionTitle(list, left, origin.Y, width, Loc.T(L.Prism.OutfitTitle), scale);
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.OutfitBody), PrismArt.Muted,
                TextStyles.Subheadline, width) + Metrics.Space.Xl * scale;
            if (outfitRows.Count == 0)
            {
                PrismType.Draw(list, new Vector2(left, cursor), Loc.T(L.Prism.NothingEquipped), PrismArt.Muted,
                    PrismFace.Label, PrismArt.LabelPixels());
                cursor += Metrics.Size.TapTarget * scale;
            }

            for (var index = 0; index < outfitRows.Count; index++)
            {
                cursor = DrawGearRow(list, left, cursor, width, outfitRows[index], false, scale);
            }

            ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        }

        var canShare = outfitRows.Count > 0 && !store.Busy;
        if (!shared)
        {
            if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.ShareOutfit), "→", true, canShare))
            {
                ShareOutfit(true, wornOutfit);
            }

            return;
        }

        if (entry is not null &&
            PrismArt.Strip(drawList, StripRect(content, scale, 1), Loc.T(L.Prism.UpdateOutfit), "→", true, canShare))
        {
            ShareOutfit(true, wornOutfit);
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.StopSharingOutfit), string.Empty, false,
                !store.Busy))
        {
            ShareOutfit(false, null);
        }
    }

    private void ShareOutfit(bool share, PrismGearDto[]? outfit)
    {
        configuration.PrismShareOutfit = share;
        configuration.Save();
        if (store.Week is { Phase: PrismPhases.Entering, MyEntry: not null })
        {
            store.ShareOutfit(outfit);
        }

        Go(outfitReturn, -1);
    }

    private void DrawGlam(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (glamPlacing is not { } placing)
        {
            Go(PrismScreen.Final, -1);
            return;
        }

        if (tryOnNext < glamRows.Count)
        {
            var piece = glamRows[tryOnNext++];
            GameLinkActions.TryOn(piece.ItemId, piece.Stain0, piece.Stain1);
        }

        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var strip = StripRect(content, scale);
        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale));
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var gutter = PrismArt.Gutter(scale);
            var width = ScrollLayout.StableContentWidth() - gutter * 2f;
            var left = origin.X + gutter;
            var labelPixels = PrismArt.LabelPixels();
            var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
            var photoHeight = MathF.Min(width / PrismStore.EntryRatio, body.Height * GlamPhotoShare);
            var photo = DrawLook(list, new Rect(new Vector2(left, origin.Y),
                new Vector2(left + width, origin.Y + photoHeight)), placing.ImageUrl, true);
            var cursor = MathF.Max(photo.Max.Y, origin.Y + photoHeight) + Metrics.Space.Md * scale;
            PrismType.Draw(list, new Vector2(left, cursor), placing.DisplayName, PrismArt.Fore, PrismFace.Label,
                labelPixels);
            PrismType.Draw(list, new Vector2(left, cursor + labelLine), PlacingDetail(placing), PrismArt.Muted,
                PrismFace.Label, labelPixels);
            cursor += labelLine * 2f + Metrics.Space.Xl * scale;
            cursor = DrawSectionLabel(list, left, cursor, width, 0, Loc.T(L.Prism.Outfit), scale);
            for (var index = 0; index < glamRows.Count; index++)
            {
                cursor = DrawGearRow(list, left, cursor, width, glamRows[index], true, scale);
            }

            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.OutfitCaveat),
                PrismArt.Muted, TextStyles.Footnote, width) + PrismArt.BottomMargin(scale);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        }

        if (PrismArt.Strip(drawList, strip, Loc.T(L.Prism.TryOnAll), "→", true, glamRows.Count > 0))
        {
            tryOnNext = 0;
        }
    }

    private float DrawGearRow(ImDrawListPtr drawList, float left, float top, float width, PrismGearRow row,
        bool tryOn, float scale)
    {
        var icon = GearIconUnits * scale;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        GameIconTile.Draw(drawList, textures, row.IconId, new Vector2(left, top), new Vector2(left + icon, top + icon),
            0f, scale);
        var textLeft = left + icon + Metrics.Space.Md * scale;
        var textRight = left + width;
        if (tryOn)
        {
            var label = Loc.T(L.Linkpearl.TryOn);
            var labelWidth = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            textRight -= labelWidth + Metrics.Space.Md * scale;
            if (PrismArt.Link(drawList, new Vector2(left + width - labelWidth, top + (icon - labelLine) * 0.5f), label,
                    PrismArt.Muted, scale))
            {
                GameLinkActions.TryOn(row.ItemId, row.Stain0, row.Stain1);
            }
        }

        var middle = top + icon * 0.5f;
        var nameTop = row.Dyes.Length > 0 ? middle - labelLine : middle - labelLine * 0.5f;
        drawList.PushClipRect(new Vector2(textLeft, top), new Vector2(textRight, top + icon), true);
        PrismType.Draw(drawList, new Vector2(textLeft, nameTop), row.Name, PrismArt.Fore, PrismFace.Label, labelPixels);
        if (row.Dyes.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, middle), row.Dyes, PrismArt.Muted, PrismFace.Label,
                labelPixels);
        }

        drawList.PopClipRect();
        var rule = top + icon + Metrics.Space.Md * scale;
        PrismArt.Hairline(drawList, left, left + width, rule, PrismArt.Faint, scale);
        return rule + Metrics.Space.Md * scale;
    }
}

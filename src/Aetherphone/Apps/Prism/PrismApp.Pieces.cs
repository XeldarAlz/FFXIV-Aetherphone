using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal enum PieceGroup : byte
{
    Single,
    Accessories,
    Weapon,
    Mods,
}

internal enum Ownership : byte
{
    None,
    Owned,
    Maybe,
}

internal readonly record struct PieceTile(PieceGroup Group, int Piece, int Count);

internal sealed partial class PrismApp
{
    private const int MaxPieceTiles = 8;
    private const float PieceTileUnits = 40f;
    private const float PiecePitchUnits = 48f;
    private const float PieceSlideUnits = 6f;
    private const float PieceDim = 0.45f;
    private const float PieceCardUnits = 236f;
    private const float SwatchUnits = 5f;
    private const float OwnedDotUnits = 4f;
    private const float BadgeUnits = 16f;
    private const int RoundSegments = 16;

    private static readonly int[] SlotOrder =
    {
        (int)GearSlot.Head, (int)GearSlot.Body, (int)GearSlot.Hands, (int)GearSlot.Legs, (int)GearSlot.Feet,
        (int)GearSlot.Ears, (int)GearSlot.Neck, (int)GearSlot.Wrists, (int)GearSlot.RingRight,
        (int)GearSlot.RingLeft, (int)GearSlot.MainHand, (int)GearSlot.OffHand,
    };

    private static readonly string[] PlusCounts = { "+0", "+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8", "+9" };
    private static readonly uint TileFill = Palette.Pack(new Vector4(0f, 0f, 0f, 0.5f));
    private static readonly uint CardFill = Palette.Pack(new Vector4(0.08f, 0.08f, 0.09f, 0.94f));
    private static readonly Vector4 PhotoRim = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 PhotoRing = new(1f, 1f, 1f, 0.5f);
    private static readonly uint PhotoHover = Palette.Pack(new Vector4(1f, 1f, 1f, 0.08f));

    private readonly PieceTile[] pieceTiles = new PieceTile[MaxPieceTiles];
    private int[] pieceOrder = Array.Empty<int>();
    private int pieceTileCount;
    private bool piecesOpen;
    private Spring piecesReveal;
    private Spring cardReveal;
    private int cardTile = -1;
    private int cardPiece = -1;
    private CachedText pieceCountText;
    private CachedText modCountText;

    private static PieceGroup GroupOf(int slot) => slot switch
    {
        (int)GearSlot.Head or (int)GearSlot.Body or (int)GearSlot.Hands or (int)GearSlot.Legs or (int)GearSlot.Feet =>
            PieceGroup.Single,
        (int)GearSlot.MainHand or (int)GearSlot.OffHand => PieceGroup.Weapon,
        _ => PieceGroup.Accessories,
    };

    private void BuildPieces(GlamGearDto[] gear)
    {
        pieceOrder = new int[totalPieces];
        var filled = 0;
        for (var slotIndex = 0; slotIndex <= SlotOrder.Length; slotIndex++)
        {
            for (var index = 0; index < gear.Length; index++)
            {
                var slot = gear[index].Slot;
                var ordered = Array.IndexOf(SlotOrder, slot) >= 0;
                var wanted = slotIndex < SlotOrder.Length ? slot == SlotOrder[slotIndex] : !ordered;
                if (wanted && gear[index].ItemId != 0 && filled < pieceOrder.Length)
                {
                    pieceOrder[filled++] = index;
                }
            }
        }

        pieceTileCount = 0;
        var accessories = new PieceTile(PieceGroup.Accessories, -1, 0);
        var weapon = new PieceTile(PieceGroup.Weapon, -1, 0);
        for (var index = 0; index < pieceOrder.Length; index++)
        {
            var piece = pieceOrder[index];
            var group = GroupOf(gear[piece].Slot);
            if (group == PieceGroup.Single)
            {
                AddTile(new PieceTile(PieceGroup.Single, piece, 1));
            }
            else if (group == PieceGroup.Weapon)
            {
                weapon = weapon with { Piece = weapon.Count == 0 ? piece : weapon.Piece, Count = weapon.Count + 1 };
            }
            else
            {
                accessories = accessories with
                {
                    Piece = accessories.Count == 0 ? piece : accessories.Piece, Count = accessories.Count + 1,
                };
            }
        }

        AddGroupTile(accessories);
        AddGroupTile(weapon);
        if (totalMods > 0)
        {
            AddTile(new PieceTile(PieceGroup.Mods, -1, totalMods));
        }
    }

    private void AddGroupTile(PieceTile group)
    {
        if (group.Count > 0)
        {
            AddTile(group.Count == 1 ? group with { Group = PieceGroup.Single } : group);
        }
    }

    private void AddTile(PieceTile tile)
    {
        if (pieceTileCount < MaxPieceTiles)
        {
            pieceTiles[pieceTileCount++] = tile;
        }
    }

    private void ResetPieces()
    {
        piecesOpen = false;
        piecesReveal.SnapTo(0f);
        cardTile = -1;
        cardPiece = -1;
    }

    private Ownership OwnedState(uint itemId) =>
        ownedIds.Contains(itemId) ? Ownership.Owned : statsStorage ? Ownership.None : Ownership.Maybe;

    private string PieceCountLine()
    {
        var key = HashCode.Combine(totalPieces, Loc.Current.Code);
        return pieceCountText.IsCurrent(key)
            ? pieceCountText.Value
            : pieceCountText.Store(key, Loc.Plural(L.Prism.PieceCount, totalPieces));
    }

    private string ModCountLine()
    {
        var key = HashCode.Combine(totalMods, Loc.Current.Code);
        return modCountText.IsCurrent(key)
            ? modCountText.Value
            : modCountText.Store(key, Loc.Plural(L.Prism.ModCount, totalMods));
    }

    private void DrawPiecesChip(ImDrawListPtr drawList, Rect photo, float scale)
    {
        if (pieceTileCount == 0)
        {
            return;
        }

        var inset = Metrics.Space.Md * scale;
        if (!PrismArt.PhotoChip(drawList, new Vector2(photo.Min.X + inset, photo.Max.Y - inset), photo.Width * 0.5f,
                IconGlyph.Of(FontAwesomeIcon.LayerGroup), totalPieces > 0 ? PieceCountLine() : ModCountLine(),
                piecesOpen, scale))
        {
            return;
        }

        piecesOpen = !piecesOpen;
        if (piecesOpen)
        {
            cardTile = -1;
            marketIndex.EnsureBuilt();
        }
    }

    private void DrawPiecesLayer(ImDrawListPtr drawList, Rect photo, float topInset, GlamGearDto[] gear, float scale)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var reveal = Math.Clamp(piecesReveal.Step(piecesOpen ? 1f : 0f, Motion.Appear, delta), 0f, 1f);
        if (!piecesOpen && reveal <= 0.001f)
        {
            cardTile = -1;
            return;
        }

        drawList.AddRectFilled(photo.Min, photo.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, PieceDim * reveal)));
        var ready = piecesOpen && piecesReveal.Arrive(1f) && !UiInteract.InputBlocked;
        var tile = PieceTileUnits * scale;
        var reach = (Metrics.Size.TapTarget - PieceTileUnits) * 0.5f * scale;
        var top = photo.Min.Y + topInset + (Metrics.Space.Xs + Metrics.Size.GlassButton * 0.5f +
                                            Metrics.Size.TapTarget * 0.5f + Metrics.Space.Xs) * scale + reach;
        var bottom = photo.Max.Y - Metrics.Space.Md * scale;
        var pitch = pieceTileCount < 2
            ? 0f
            : Math.Clamp((bottom - top - tile) / (pieceTileCount - 1), Metrics.Size.TapTarget * scale,
                PiecePitchUnits * scale);
        var right = photo.Max.X - Metrics.Space.Md * scale;
        var column = new Rect(new Vector2(right - tile - reach, top - reach),
            new Vector2(right + reach, top + pitch * (pieceTileCount - 1) + tile + reach));
        var hovered = piecesOpen && UiInteract.HoverOverlay(column);
        var mark = PrismMotion.Mark(drawList);
        var named = -1;
        for (var index = 0; index < pieceTileCount; index++)
        {
            var min = new Vector2(right - tile, top + pitch * index);
            var rect = new Rect(min, min + new Vector2(tile, tile));
            var hit = new Rect(rect.Min - new Vector2(reach, reach), rect.Max + new Vector2(reach, reach));
            var over = hovered && ImGui.IsMouseHoveringRect(hit.Min, hit.Max);
            var tileMark = PrismMotion.Mark(drawList);
            DrawPieceTile(drawList, rect, pieceTiles[index], gear, index == cardTile, scale);
            if (over)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(PhotoRing), Metrics.Radius.Sm * scale,
                    ImDrawFlags.None, Metrics.Stroke.Ring * scale);
                named = index;
            }

            PrismMotion.Grow(drawList, tileMark, rect.Center, PressFx.Scale(ImGui.GetID($"prism.piece{index}"),
                ready && over && ImGui.IsMouseDown(ImGuiMouseButton.Left), PressFx.ControlPressedScale));
            if (ready && UiInteract.Click(hit.Min, hit.Max, over))
            {
                PickTile(index);
            }
        }

        if (reveal < PrismMotion.Settled)
        {
            PrismMotion.Shift(drawList, mark, new Vector2(PieceSlideUnits * scale * (1f - reveal), 0f), reveal);
        }

        if (cardTile >= 0 && cardTile < pieceTileCount)
        {
            DrawCardLayer(drawList, photo, topInset, column.Min.X, top + pitch * cardTile, gear, ready, delta, reveal,
                scale);
        }

        if (named >= 0 && cardTile < 0)
        {
            DrawTileName(drawList, new Vector2(right - tile - Metrics.Space.Xs * scale, top + pitch * named + tile * 0.5f),
                TileName(pieceTiles[named], gear), photo.Width * 0.6f, scale);
        }
    }

    private void DrawCardLayer(ImDrawListPtr drawList, Rect photo, float topInset, float columnLeft, float anchor,
        GlamGearDto[] gear, bool ready, float delta, float reveal, float scale)
    {
        var cardShow = Math.Clamp(cardReveal.Step(1f, Motion.Appear, delta), 0f, 1f);
        var cardReady = ready && cardReveal.Arrive(1f);
        var cardMark = PrismMotion.Mark(drawList);
        var limits = new Rect(new Vector2(photo.Min.X + Metrics.Space.Md * scale, photo.Min.Y + topInset),
            new Vector2(columnLeft - Metrics.Space.Xs * scale,
                photo.Max.Y - (Metrics.Space.Md + PrismArt.ChipUnits + Metrics.Space.Sm) * scale));
        DrawPieceCard(drawList, limits, anchor, pieceTiles[cardTile], gear, cardReady, scale);
        if (cardShow * reveal < PrismMotion.Settled)
        {
            PrismMotion.Shift(drawList, cardMark, Vector2.Zero, cardShow * reveal);
        }
    }

    private string TileName(PieceTile tile, GlamGearDto[] gear) => tile.Group switch
    {
        PieceGroup.Mods => ModCountLine(),
        PieceGroup.Weapon => Loc.T(L.Prism.Weapon),
        PieceGroup.Accessories => Loc.T(L.Prism.Accessories),
        _ => GearItem(gear[tile.Piece].ItemId, out var name, out _) ? name : string.Empty,
    };

    private static void DrawTileName(ImDrawListPtr drawList, Vector2 rightCenter, string name, float width,
        float scale)
    {
        var text = Typography.FitText(name, width, PrismType.Caption);
        var size = Typography.Measure(text, PrismType.Caption);
        var pad = Metrics.Space.Sm * scale;
        var half = PrismArt.ChipUnits * scale * 0.5f;
        var min = new Vector2(rightCenter.X - size.X - pad * 2f, rightCenter.Y - half);
        drawList.AddRectFilled(min, new Vector2(rightCenter.X, rightCenter.Y + half), CardFill, half);
        Typography.Draw(drawList, new Vector2(min.X + pad, rightCenter.Y - size.Y * 0.5f), text, PrismArt.PhotoFore,
            PrismType.Caption);
    }

    private void PickTile(int index)
    {
        var tile = pieceTiles[index];
        var piece = tile.Group == PieceGroup.Single ? tile.Piece : -1;
        if (cardTile == index && cardPiece == piece)
        {
            cardTile = -1;
            return;
        }

        if (cardTile < 0)
        {
            cardReveal.SnapTo(0f);
        }

        cardTile = index;
        cardPiece = piece;
    }

    private void DrawPieceTile(ImDrawListPtr drawList, Rect rect, PieceTile tile, GlamGearDto[] gear, bool selected,
        float scale)
    {
        var radius = Metrics.Radius.Sm * scale;
        var piece = tile.Group == PieceGroup.Mods ? null : gear[tile.Piece];
        drawList.AddRectFilled(rect.Min, rect.Max, TileFill, radius);
        if (piece is null)
        {
            PrismArt.SlotIcon(drawList, rect.Center, rect.Width * 0.5f, GearSlots.None, PrismArt.PhotoFore);
        }
        else if (GearItem(piece.ItemId, out _, out var iconId))
        {
            GameIconTile.Draw(drawList, textures, iconId, rect.Min, rect.Max, radius, scale);
        }

        if (selected)
        {
            drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(PrismArt.PhotoFore), radius, ImDrawFlags.None,
                Metrics.Stroke.Ring * scale);
        }

        if (piece is not null)
        {
            DrawOwnedMark(drawList, rect.Max - new Vector2(radius, radius) * 0.5f, OwnedState(piece.ItemId),
                PrismArt.PhotoFore, PhotoRim, scale);
        }

        if (tile.Group != PieceGroup.Single)
        {
            DrawBadge(drawList, rect, piece is null ? NumberText.Compact(tile.Count)
                : PlusCounts[Math.Min(tile.Count - 1, PlusCounts.Length - 1)], scale);
        }
    }

    private static void DrawBadge(ImDrawListPtr drawList, Rect tile, string text, float scale)
    {
        var size = Typography.Measure(text, TextStyles.Caption2);
        var height = BadgeUnits * scale;
        var width = MathF.Max(height, size.X + Metrics.Space.Xs * scale);
        var min = tile.Min - new Vector2(Metrics.Space.Xs, Metrics.Space.Xs) * scale;
        var max = min + new Vector2(width, height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(PrismArt.PhotoFore), height * 0.5f);
        Typography.Draw(drawList, (min + max - size) * 0.5f, text, PrismArt.Ink, TextStyles.Caption2);
    }

    private static void DrawOwnedMark(ImDrawListPtr drawList, Vector2 center, Ownership owned, Vector4 ink,
        Vector4 rim, float scale)
    {
        if (owned == Ownership.None)
        {
            return;
        }

        var radius = OwnedDotUnits * scale;
        var edge = MathF.Max(1f, Metrics.Stroke.Thin * scale);
        drawList.AddCircleFilled(center, radius + edge, ImGui.GetColorU32(rim), RoundSegments);
        if (owned == Ownership.Owned)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink), RoundSegments);
            return;
        }

        drawList.AddCircle(center, radius - edge * 0.5f, ImGui.GetColorU32(ink), RoundSegments, edge);
    }

    private static void DrawSwatch(ImDrawListPtr drawList, Vector2 center, uint rgb, Vector4 ring, float scale)
    {
        var radius = SwatchUnits * scale;
        drawList.AddCircleFilled(center, radius + PrismArt.HairlineWidth(scale),
            ImGui.GetColorU32(ring), RoundSegments);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.FromRgb(rgb)), RoundSegments);
    }

    private void DrawPieceCard(ImDrawListPtr drawList, Rect limits, float anchor, PieceTile tile, GlamGearDto[] gear,
        bool ready, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var width = MathF.Min(PieceCardUnits * scale, limits.Width);
        var inner = width - pad * 2f;
        var headline = Typography.LineHeight(PrismType.Headline);
        var caption = Typography.LineHeight(PrismType.Caption);
        var tap = Metrics.Size.TapTarget * scale;
        var single = cardPiece >= 0 ? gear[cardPiece] : null;
        var ownership = single is null ? Ownership.None : OwnedState(single.ItemId);
        var rows = tile.Group == PieceGroup.Mods ? totalMods : tile.Count;
        var note = tile.Group == PieceGroup.Mods && !glamour.HasPenumbra ? Loc.T(L.Prism.ModsNeedPenumbra)
            : string.Empty;
        var noteHeight = note.Length > 0
            ? Typography.MeasureWrappedBlock(note, PrismType.Caption, inner).Y + Metrics.Space.Xs * scale
            : 0f;
        var market = single is not null && ownership != Ownership.Owned &&
                     marketIndex.TryGet(single.ItemId, out _) && navigation.IsAvailable(MarketAppId);
        float body;
        if (single is not null)
        {
            var dyes = (single.Stain0 != 0 ? 1 : 0) + (single.Stain1 != 0 ? 1 : 0);
            body = (dyes + 1) * caption + Metrics.Space.Sm * scale + tap + (market ? tap : 0f);
        }
        else
        {
            var room = MathF.Max(1f, limits.Height - pad * 2f - headline - noteHeight - Metrics.Space.Xs * scale);
            rows = Math.Min(rows, (int)(room / tap));
            body = noteHeight + rows * tap;
        }

        var height = pad * 2f + headline + Metrics.Space.Xs * scale + body;
        var cardTop = MathF.Max(limits.Min.Y, MathF.Min(anchor, limits.Max.Y - height));
        var card = new Rect(new Vector2(limits.Max.X - width, cardTop), new Vector2(limits.Max.X, cardTop + height));
        var over = UiInteract.HoverOverlay(card);
        drawList.AddRectFilled(card.Min, card.Max, CardFill, Metrics.Radius.Card * scale);
        var left = card.Min.X + pad;
        var cursor = card.Min.Y + pad;
        var title = single is not null && GearItem(single.ItemId, out var name, out _) ? name : TileName(tile, gear);
        Typography.Draw(drawList, new Vector2(left, cursor), Typography.FitText(title, inner, PrismType.Headline),
            PrismArt.PhotoFore, PrismType.Headline);
        cursor += headline + Metrics.Space.Xs * scale;
        if (single is null)
        {
            if (note.Length > 0)
            {
                cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), note, PrismArt.PhotoMuted,
                    PrismType.Caption, inner) + Metrics.Space.Xs * scale;
            }

            DrawCardRows(drawList, new Rect(new Vector2(left, cursor), new Vector2(left + inner, card.Max.Y - pad)),
                tile, gear, rows, ready && over, scale);
            return;
        }

        cursor = DrawCardDye(drawList, left, cursor, inner, single.Stain0, scale);
        cursor = DrawCardDye(drawList, left, cursor, inner, single.Stain1, scale);
        var glyph = ownership == Ownership.Owned ? caption * 0.7f : 0f;
        if (glyph > 0f)
        {
            PhoneIcon.Draw(drawList, new Vector2(left + glyph * 0.5f, cursor + caption * 0.5f), PhoneIcons.Check,
                PrismArt.PhotoMuted, glyph);
            glyph += Metrics.Space.Xs * scale;
        }

        Typography.Draw(drawList, new Vector2(left + glyph, cursor),
            Typography.FitText(Loc.T(OwnershipLine(ownership)), inner - glyph, PrismType.Caption), PrismArt.PhotoMuted,
            PrismType.Caption);
        cursor += caption;
        var button = new Rect(new Vector2(left, cursor + Metrics.Space.Sm * scale),
            new Vector2(left + inner, cursor + Metrics.Space.Sm * scale + tap));
        if (DrawButton(drawList, button, Loc.T(L.Prism.TryOnPiece), ButtonStyle.Tinted, scale, overlay: true,
                ink: photoControls, id: "prism.tryOnPiece") && ready)
        {
            TryOnPiece(single, statsFor ?? string.Empty);
        }

        if (!market)
        {
            return;
        }

        var label = Loc.T(L.Prism.CheckMarket);
        var link = new Rect(new Vector2(left, button.Max.Y),
            new Vector2(left + ButtonWidth([label], inner, scale), button.Max.Y + tap));
        if (DrawButton(drawList, link, label, ButtonStyle.Plain, scale, overlay: true, ink: photoControls,
                id: "prism.pieceMarket") && ready)
        {
            marketLauncher.RequestItem(single.ItemId);
            navigation.Open(MarketAppId);
        }
    }

    private static LocString OwnershipLine(Ownership ownership) => ownership switch
    {
        Ownership.Owned => L.Prism.YouOwnThis,
        Ownership.Maybe => L.Prism.CheckStorage,
        _ => L.Prism.NotOwned,
    };

    private float DrawCardDye(ImDrawListPtr drawList, float left, float top, float width, byte stain, float scale)
    {
        if (stain == 0)
        {
            return top;
        }

        var line = Typography.LineHeight(PrismType.Caption);
        var swatch = SwatchUnits * scale;
        DrawSwatch(drawList, new Vector2(left + swatch, top + line * 0.5f), gameData.StainColor(stain), PhotoRing,
            scale);
        var textLeft = left + swatch * 2f + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(DyeLine(stain, 0), width - (textLeft - left), PrismType.Caption), PrismArt.PhotoFore,
            PrismType.Caption);
        return top + line;
    }

    private void DrawCardRows(ImDrawListPtr drawList, Rect area, PieceTile tile, GlamGearDto[] gear, int rows,
        bool interactive, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var drawn = 0;
        for (var index = 0; index < gear.Length && drawn < rows; index++)
        {
            if (tile.Group != PieceGroup.Mods)
            {
                var piece = pieceOrder.Length > index ? pieceOrder[index] : -1;
                if (piece < 0 || GroupOf(gear[piece].Slot) != tile.Group)
                {
                    continue;
                }

                var row = new Rect(new Vector2(area.Min.X, area.Min.Y + drawn * tap),
                    new Vector2(area.Max.X, area.Min.Y + (drawn + 1) * tap));
                if (DrawCardPieceRow(drawList, row, gear[piece], interactive, scale))
                {
                    cardPiece = piece;
                }

                drawn++;
                continue;
            }

            if (gear[index].Mods is not { } mods)
            {
                continue;
            }

            for (var modIndex = 0; modIndex < mods.Length && drawn < rows; modIndex++)
            {
                var row = new Rect(new Vector2(area.Min.X, area.Min.Y + drawn * tap),
                    new Vector2(area.Max.X, area.Min.Y + (drawn + 1) * tap));
                DrawCardModRow(drawList, row, mods[modIndex], interactive);
                drawn++;
            }
        }
    }

    private bool DrawCardPieceRow(ImDrawListPtr drawList, Rect row, GlamGearDto piece, bool interactive, float scale)
    {
        var over = CardRowHover(drawList, row, interactive);
        var icon = Metrics.Size.IconTile * scale;
        var min = new Vector2(row.Min.X, row.Center.Y - icon * 0.5f);
        var name = string.Empty;
        if (GearItem(piece.ItemId, out var found, out var iconId))
        {
            name = found;
            GameIconTile.Draw(drawList, textures, iconId, min, min + new Vector2(icon, icon),
                Metrics.Radius.Sm * 0.5f * scale, scale);
        }

        DrawOwnedMark(drawList, min + new Vector2(icon, icon), OwnedState(piece.ItemId), PrismArt.PhotoFore,
            PhotoRim, scale);
        var textLeft = min.X + icon + Metrics.Space.Sm * scale;
        DrawCentredLine(drawList, textLeft, row.Max.X, row.Center.Y, name, PrismArt.PhotoFore);
        return interactive && UiInteract.Click(row.Min, row.Max, over);
    }

    private void DrawCardModRow(ImDrawListPtr drawList, Rect row, GlamModDto mod, bool interactive)
    {
        var over = CardRowHover(drawList, row, interactive);
        var (_, allowed, installed) = ModLine(mod);
        var side = installed ? Loc.T(L.Prism.Installed) : allowed ? Loc.T(L.Prism.GetMod) : string.Empty;
        var sideWidth = side.Length > 0 ? Typography.Measure(side, PrismType.Caption).X : 0f;
        if (side.Length > 0)
        {
            Typography.Draw(drawList,
                new Vector2(row.Max.X - sideWidth, row.Center.Y - Typography.LineHeight(PrismType.Caption) * 0.5f),
                side, installed ? PrismArt.PhotoMuted : PrismArt.PhotoFore, PrismType.Caption);
            sideWidth += Metrics.Space.Sm * UiScale.Current;
        }

        DrawCentredLine(drawList, row.Min.X, row.Max.X - sideWidth, row.Center.Y, mod.Name, PrismArt.PhotoFore);
        if (interactive && UiInteract.Click(row.Min, row.Max, over))
        {
            OpenMod(mod, allowed && !installed);
        }
    }

    private static void DrawCentredLine(ImDrawListPtr drawList, float left, float right, float middle, string text,
        Vector4 ink)
    {
        var fitted = Typography.FitText(text, MathF.Max(1f, right - left), PrismType.Body);
        Typography.Draw(drawList, new Vector2(left, middle - Typography.Measure(fitted, PrismType.Body).Y * 0.5f),
            fitted, ink, PrismType.Body);
    }

    private static bool CardRowHover(ImDrawListPtr drawList, Rect row, bool interactive)
    {
        if (!interactive || !ImGui.IsMouseHoveringRect(row.Min, row.Max))
        {
            return false;
        }

        var bleed = new Vector2(Metrics.Space.Xs * UiScale.Current, 0f);
        drawList.AddRectFilled(row.Min - bleed, row.Max + bleed, PhotoHover, Metrics.Radius.Sm * UiScale.Current);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    private void OpenMod(GlamModDto mod, bool link)
    {
        if (link)
        {
            UrlActions.AskThenOpen(mod.Link);
            return;
        }

        OpenModGlams(mod);
    }

    private void TryOnPiece(GlamGearDto piece, string from)
    {
        glamour.TryOnPiece(piece);
        if (!Wearing(from))
        {
            StartWearing(string.Empty, GearItem(piece.ItemId, out var name, out _) ? name : string.Empty);
        }
    }
}

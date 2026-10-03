using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float TileHeight = 108f;
    private const float TileGap = 12f;
    private const float NavRowHeight = 60f;
    private const float DailySpinCardHeight = 64f;
    private const long SessionPillAfterSeconds = 45 * 60;

    private readonly struct FloorTileDefinition
    {
        public readonly string GameId;
        public readonly LocString Name;
        public readonly bool Playable;
        public readonly string RoomId;

        public FloorTileDefinition(string gameId, LocString name, bool playable, string roomId = "")
        {
            GameId = gameId;
            Name = name;
            Playable = playable;
            RoomId = roomId;
        }
    }

    private static readonly FloorTileDefinition[] FloorTiles =
    {
        new(CasinoGames.Blackjack, L.Casino.GameBlackjack, true),
        new(CasinoGames.Slots, L.Casino.GameSlots, true),
        new(CasinoGames.Scratch, L.Casino.GameScratch, true),
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep, true),
        new(CasinoGames.Bingo, L.Casino.GameBingo, true, Core.Casino.CasinoRoomIds.BingoHall),
        new(CasinoGames.Wheel, L.Casino.GameWheel, true, Core.Casino.CasinoRoomIds.WheelFloor),
    };

    private void DrawFloor(Rect body)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (tab)
            {
                case CasinoTab.Games:
                    DrawGamesTab(body);
                    break;
                case CasinoTab.Live:
                    DrawLiveTab(body);
                    break;
                case CasinoTab.Cashier:
                    DrawCashierTab(body);
                    break;
                default:
                    DrawLobbyTab(body);
                    break;
            }
        }

        DrawFloorTabBar(body);
    }

    private void DrawFloorTabBar(Rect area)
    {
        navTabs[0] = new TabItem(Loc.T(L.Casino.TabLobby), IconGlyph.Of(FontAwesomeIcon.DiceD20));
        navTabs[1] = new TabItem(Loc.T(L.Casino.TabGames), IconGlyph.Of(FontAwesomeIcon.Th),
            AnchorKey: "casino.tab.games");
        navTabs[2] = new TabItem(Loc.T(L.Casino.TabLive), IconGlyph.Of(FontAwesomeIcon.BroadcastTower),
            Badge: LiveHeadcount());
        navTabs[3] = new TabItem(Loc.T(L.Casino.TabCashier), IconGlyph.Of(FontAwesomeIcon.CashRegister));
        var result = bottomNav.Draw(area, ui, navTabs, (int)tab);
        if (result.Tapped < 0)
        {
            return;
        }

        tab = (CasinoTab)result.Tapped;
    }

    private int LiveHeadcount()
    {
        var total = casinoTables.SeatedAt(Core.Casino.CasinoWire.BlackjackKind);
        total += casinoRooms.OccupancyOf(Core.Casino.CasinoRoomIds.WheelFloor);
        total += casinoRooms.OccupancyOf(Core.Casino.CasinoRoomIds.BingoHall);
        return total;
    }

    private void DrawRecordsRows(float scale)
    {
        ui.SectionHeading(Loc.T(L.Casino.RecordsHeading), 4f);
        if (DrawNavRow(FontAwesomeIcon.Receipt, L.Casino.HistoryRow, L.Casino.HistoryRowHint, scale))
        {
            historyLoadFailed = false;
            history.Invalidate();
            router.Push(new CasinoRoute(CasinoScreen.History));
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (DrawNavRow(FontAwesomeIcon.ShieldAlt, L.Casino.FairnessRow, L.Casino.FairnessRowHint, scale))
        {
            router.Push(new CasinoRoute(CasinoScreen.Fairness));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.CareHeading), 4f);
        if (DrawNavRow(FontAwesomeIcon.HandHoldingHeart, L.Casino.LimitsRow, L.Casino.LimitsRowHint, scale))
        {
            router.Push(new CasinoRoute(CasinoScreen.Limits));
        }
    }

    private void DrawStakeNotice(float scale)
    {
        var state = casino.State;
        if (state is null || (!state.StakesPaused && !state.Draining))
        {
            return;
        }

        var title = state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : Loc.T(L.Casino.DrainingTitle);
        var hint = state.StakesPaused ? Loc.T(L.Casino.PausedHint) : Loc.T(L.Casino.DrainingHint);
        DrawFloorNotice(title, hint, scale);
    }

    private void DrawDailySpinCard(float scale)
    {
        var claim = Core.Casino.DailySpinStatus.Of(casinoSpin.Answer);
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = DailySpinCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        UiAnchors.Report("casino.spin", card);
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var available = Core.Casino.DailySpinStatus.OffersWheel(claim);
        if (available)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.45f)), 1f * scale);
        }

        var glyphCenter = new Vector2(card.Min.X + 34f * scale, card.Center.Y);
        drawList.AddCircleFilled(glyphCenter, 20f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, available ? 0.20f : 0.12f)), 32);
        CasinoGlyphs.Draw(drawList, CasinoGames.DailySpin, glyphCenter, 11f * scale,
            ImGui.GetColorU32(available ? ui.Accent : ui.MutedInk), ImGui.GetColorU32(ui.Palette.CardFill));

        var textLeft = card.Min.X + 62f * scale;
        var badgeWidth = available ? DrawReadyBadge(drawList, card, scale) : 0f;
        var textWidth = card.Max.X - 14f * scale - badgeWidth - textLeft;
        var title = Typography.FitText(Loc.T(L.Casino.SpinCardTitle), textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 14f * scale), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var hint = Typography.FitText(DailySpinHint(claim), textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 34f * scale), hint, ui.MutedInk,
            TextStyles.Footnote);

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenDailySpin();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private float DrawReadyBadge(ImDrawListPtr drawList, Rect card, float scale)
    {
        var label = Loc.T(L.Casino.SpinReadyBadge);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var chipHeight = labelSize.Y + 6f * scale;
        var chipMax = new Vector2(card.Max.X - 14f * scale, card.Center.Y + chipHeight * 0.5f);
        var chipMin = new Vector2(chipMax.X - labelSize.X - 16f * scale, card.Center.Y - chipHeight * 0.5f);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.Accent));
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, ui.Palette.HeaderInk,
            TextStyles.Caption1);
        return chipMax.X - chipMin.X + 10f * scale;
    }

    private string DailySpinHint(Core.Casino.DailySpinClaim claim)
    {
        var answer = casinoSpin.Answer;
        if (claim == Core.Casino.DailySpinClaim.Available || claim == Core.Casino.DailySpinClaim.Unknown)
        {
            return Loc.T(L.Casino.SpinCardHint);
        }

        if (claim == Core.Casino.DailySpinClaim.Denied && answer is not null)
        {
            return Loc.T(Core.Casino.CasinoReasons.MessageFor(answer.Reason));
        }

        return answer is not null && answer.NextSpinAtUnix > 0
            ? Loc.T(L.Casino.SpinNextAt, TimeText.FutureMoment(answer.NextSpinAtUnix))
            : Loc.T(L.Casino.SpinNextSoon);
    }

    private void DrawGameGrid(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var gap = TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = TileHeight * scale;
        var rowCount = (FloorTiles.Length + 1) / 2;
        var gridHeight = rowCount * tileHeight + (rowCount - 1) * gap;
        for (var index = 0; index < FloorTiles.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var tile = new Rect(min, new Vector2(min.X + tileWidth, min.Y + tileHeight));
            DrawGameTile(drawList, tile, FloorTiles[index], scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, gridHeight));
    }

    private void DrawGameTile(ImDrawListPtr drawList, Rect tile, in FloorTileDefinition definition, float scale)
    {
        var rounding = Metrics.Radius.Card * scale;
        var hovered = definition.Playable && UiInteract.Hover(tile.Min, tile.Max);
        ui.Card(drawList, tile.Min, tile.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, tile.Min, tile.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var contentAlpha = definition.Playable ? 1f : 0.45f;
        var glyphCenter = new Vector2(tile.Center.X, tile.Min.Y + 40f * scale);
        var glyphRadius = 20f * scale;
        drawList.AddCircleFilled(glyphCenter, glyphRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, contentAlpha)), 40);
        drawList.AddCircle(glyphCenter, glyphRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f * contentAlpha)), 40,
            Metrics.Stroke.Thin * scale);
        CasinoGlyphs.Draw(drawList, definition.GameId, glyphCenter, 11f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, contentAlpha)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, contentAlpha)));

        var name = Typography.FitText(Loc.T(definition.Name), tile.Width - 20f * scale,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawCentered(drawList, new Vector2(tile.Center.X, tile.Min.Y + 78f * scale), name,
            Palette.WithAlpha(ui.TitleInk, definition.Playable ? 1f : 0.6f), TextStyles.SubheadlineEmphasized);

        if (!definition.Playable)
        {
            DrawCornerChip(drawList, tile, Loc.T(L.Casino.Soon), ui.MutedInk, scale);
            return;
        }

        var crowd = CrowdAt(definition);
        if (crowd > 0)
        {
            DrawCornerChip(drawList, tile, CrowdLine(definition.GameId, crowd), ui.Accent, scale);
        }

        if (definition.RoomId.Length > 0)
        {
            DrawRoomClock(drawList, tile, definition.RoomId, scale);
        }

        if (UiInteract.Click(tile.Min, tile.Max, hovered))
        {
            OpenGame(definition.GameId);
        }
    }

    private int CrowdAt(in FloorTileDefinition definition)
    {
        if (string.Equals(definition.GameId, CasinoGames.Blackjack, StringComparison.Ordinal))
        {
            return casinoTables.SeatedAt(Core.Casino.CasinoWire.BlackjackKind);
        }

        return definition.RoomId.Length > 0 ? casinoRooms.OccupancyOf(definition.RoomId) : 0;
    }

    private static string CrowdLine(string gameId, int occupancy)
    {
        var count = Apps.Games.Framework.GameNumber.Label(occupancy);
        if (string.Equals(gameId, CasinoGames.Bingo, StringComparison.Ordinal))
        {
            return Loc.T(L.Casino.BingoInTheHall, count);
        }

        return string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal)
            ? Loc.T(L.Casino.BlackjackAtTheTable, count)
            : Loc.T(L.Casino.WheelAtTheRail, count);
    }

    private void DrawRoomClock(ImDrawListPtr drawList, Rect tile, string roomId, float scale)
    {
        if (!casinoRooms.TryRoomClock(roomId, out var phase, out var endsAtUnixMs) || endsAtUnixMs <= 0)
        {
            return;
        }

        var remaining = casinoRooms.Room.RemainingMilliseconds(endsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (remaining <= 0)
        {
            return;
        }

        var seconds = (int)((remaining + 999) / 1000);
        var label = phase == Core.Casino.CasinoRoomPhases.Open
            ? Loc.T(L.Casino.RoomClosesIn, TimeText.Duration(seconds))
            : Loc.T(L.Casino.RoomNextIn, TimeText.Duration(seconds));
        var fitted = Typography.FitText(label, tile.Width - 16f * scale, TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(tile.Center.X, tile.Max.Y - 14f * scale), fitted,
            ui.MutedInk, TextStyles.Caption2);
    }

    private void DrawCornerChip(ImDrawListPtr drawList, Rect tile, string label, Vector4 ink, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var horizontalPad = 7f * scale;
        var chipHeight = labelSize.Y + 5f * scale;
        var chipMax = new Vector2(tile.Max.X - 8f * scale, tile.Min.Y + 8f * scale + chipHeight);
        var chipMin = new Vector2(chipMax.X - labelSize.X - horizontalPad * 2f, tile.Min.Y + 8f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, ink, TextStyles.Caption1);
    }

    private string SessionElapsedLine()
    {
        var seenAtUnix = casino.SittingSeenAtUnix;
        if (seenAtUnix <= 0)
        {
            return string.Empty;
        }

        var elapsedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seenAtUnix;
        if (elapsedSeconds < SessionPillAfterSeconds)
        {
            return string.Empty;
        }

        return Loc.T(L.Casino.SessionPill, TimeText.Duration((int)Math.Min(elapsedSeconds, int.MaxValue)));
    }

    private void AskCashOut(Core.Aethernet.Contracts.CasinoSittingDto sitting)
    {
        var stackText = NumberText.Group(sitting.Stack);
        confirm.Ask(new Core.Confirm.ConfirmRequest
        {
            Title = Loc.T(L.Casino.CashOutConfirmTitle, stackText),
            Message = Loc.T(L.Casino.CashOutConfirmBody),
            ConfirmLabel = Loc.T(L.Casino.CashOut),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = casino.CloseSitting,
        });
    }

    private void DrawFloorNotice(string title, string hint, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + hintBlock.Y + 26f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title,
            ui.Accent, TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private static string ClientGameId(string wireKind)
    {
        const string prefix = "casino.";
        return wireKind.StartsWith(prefix, StringComparison.Ordinal) ? wireKind[prefix.Length..] : wireKind;
    }

    private bool DrawNavRow(FontAwesomeIcon icon, LocString title, LocString hint, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = NavRowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + 26f * scale, row.Center.Y);
        drawList.AddCircleFilled(iconCenter, 15f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 32);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(icon), ui.Accent, 0.85f);

        var textLeft = row.Min.X + 48f * scale;
        var chevronCenter = new Vector2(row.Max.X - 18f * scale, row.Center.Y);
        var textWidth = chevronCenter.X - 14f * scale - textLeft;
        var titleText = Typography.FitText(Loc.T(title), textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale), titleText, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var hintText = Typography.FitText(Loc.T(hint), textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale), hintText, ui.MutedInk,
            TextStyles.Footnote);
        AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk, 0.8f);

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return clicked;
    }
}

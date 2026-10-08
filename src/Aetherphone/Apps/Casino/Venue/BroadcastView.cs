using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class BroadcastView
{
    public const string AppId = "casino";

    private const int MaxSeats = 9;
    private const float CardWidthShare = 0.075f;
    private const float MinCardWidth = 44f;
    private const float CardOverlap = 0.42f;
    private const float SeatRingInset = 0.16f;

    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTextCache texts = new();
    private readonly RollLabels amounts = new();
    private readonly Vector2[] seatCenters = new Vector2[MaxSeats];

    private string roomId = string.Empty;
    private BroadcastGame game;
    private bool attached;
    private object? projectedFrom;
    private BroadcastTable table = BroadcastTable.Empty;

    public BroadcastView(CasinoRoomsStore rooms)
    {
        this.rooms = rooms;
    }

    public bool Active => roomId.Length > 0;

    public string RoomId => roomId;

    public BroadcastGame Game => game;

    public void Enter(string tableId, BroadcastGame broadcastGame)
    {
        roomId = tableId;
        game = broadcastGame;
        projectedFrom = null;
        table = BroadcastTable.Empty;
        attached = !string.Equals(rooms.Room.RoomId, tableId, StringComparison.Ordinal);
        if (attached)
        {
            rooms.Enter(tableId);
        }

        AppLandscape.Request(AppId);
    }

    public void Reset()
    {
        if (attached)
        {
            rooms.Leave();
        }

        if (roomId.Length > 0)
        {
            AppLandscape.Release(AppId);
        }

        attached = false;
        roomId = string.Empty;
        projectedFrom = null;
        table = BroadcastTable.Empty;
    }

    public CasinoStageSpec Spec()
    {
        return new CasinoStageSpec(game == BroadcastGame.Holdem ? CasinoGames.Holdem : CasinoGames.Blackjack,
            L.Venue.BroadcastTitle, Backdrop.Felt, LampPool: 1f);
    }

    public void Draw(in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Project();
        var safe = frame.Safe;
        if (table.Seats.Length == 0 && table.Waiting)
        {
            VenueArt.StateLine(drawList, Loc.T(L.Venue.BroadcastWaiting), safe.Center, safe.Width,
                CasinoColors.InkTitle, scale);
            return;
        }

        var cardWidth = MathF.Max(MinCardWidth * scale, safe.Width * CardWidthShare);
        var cardHeight = PlayingCards.HeightFor(cardWidth);
        var top = safe.Min.Y + cardHeight * 0.5f + 8f * scale;
        if (game == BroadcastGame.Blackjack)
        {
            DrawRow(drawList, table.DealerCards, new Vector2(safe.Center.X, top), cardWidth, scale);
            var label = table.DealerTotal > 0
                ? texts.Count(L.Venue.BroadcastDealerTotal, table.DealerTotal)
                : Loc.T(L.Venue.BroadcastDealer);
            VenueArt.Line(drawList, label, new Vector2(safe.Center.X, top + cardHeight * 0.5f + 16f * scale),
                safe.Width * 0.4f, CasinoColors.InkTitle, TextStyles.Headline);
        }
        else
        {
            DrawRow(drawList, table.Board, new Vector2(safe.Center.X, safe.Min.Y + safe.Height * 0.36f), cardWidth,
                scale);
            if (table.Pot > 0)
            {
                VenueArt.Line(drawList, texts.Number(L.Venue.BroadcastPot, table.Pot),
                    new Vector2(safe.Center.X, safe.Min.Y + safe.Height * 0.36f + cardHeight * 0.5f + 20f * scale),
                    safe.Width * 0.5f, CasinoColors.Money, TextStyles.Title2);
            }
        }

        DrawSeats(drawList, safe, cardWidth * 0.8f, scale);
    }

    private void Project()
    {
        var state = rooms.Room.State;
        if (state is null || !string.Equals(state.RoomId, roomId, StringComparison.Ordinal))
        {
            return;
        }

        if (game == BroadcastGame.Blackjack)
        {
            var board = state.Blackjack;
            if (board is null || ReferenceEquals(board, projectedFrom))
            {
                return;
            }

            projectedFrom = board;
            table = BroadcastProjection.FromBlackjack(board);
            return;
        }

        var gameState = state.Snapshot.GameState;
        if (ReferenceEquals(gameState, projectedFrom))
        {
            return;
        }

        projectedFrom = gameState;
        table = BroadcastProjection.FromHoldem(gameState);
    }

    private void DrawSeats(ImDrawListPtr drawList, Rect safe, float cardWidth, float scale)
    {
        var seats = table.Seats;
        var count = Math.Min(seats.Length, MaxSeats);
        if (count == 0)
        {
            return;
        }

        var ring = new Rect(new Vector2(safe.Min.X + safe.Width * SeatRingInset, safe.Min.Y + safe.Height * 0.30f),
            new Vector2(safe.Max.X - safe.Width * SeatRingInset, safe.Max.Y - safe.Height * 0.08f));
        if (game == BroadcastGame.Blackjack)
        {
            for (var index = 0; index < count; index++)
            {
                var share = count == 1 ? 0.5f : index / (float)(count - 1);
                seatCenters[index] = new Vector2(safe.Min.X + safe.Width * (0.1f + 0.8f * share),
                    safe.Min.Y + safe.Height * (0.66f + 0.12f * MathF.Sin(MathF.PI * share)));
            }
        }
        else
        {
            SeatLayout.Ring(count, ring, seatCenters.AsSpan(0, count));
        }

        var nameWidth = MathF.Max(cardWidth * 2.4f, safe.Width / (count + 1));
        var cardHeight = PlayingCards.HeightFor(cardWidth);
        for (var index = 0; index < count; index++)
        {
            var seat = seats[index];
            var center = seatCenters[index];
            if (seat.Acting)
            {
                drawList.AddCircleFilled(center, cardWidth * 1.4f, ImGui.GetColorU32(CasinoColors.Money with
                {
                    W = 0.12f + 0.08f * Pulse.Wave(Pulse.Breath),
                }), 32);
            }

            DrawRow(drawList, seat.Cards, center, cardWidth, scale, seat.Out ? 0.45f : 1f);
            var nameTop = center.Y + cardHeight * 0.5f + 6f * scale;
            var nameHeight = Typography.LineHeight(TextStyles.Headline);
            VenueArt.Line(drawList, seat.Name, new Vector2(center.X, nameTop + nameHeight * 0.5f), nameWidth,
                seat.Acting ? CasinoColors.MoneyHighlight : CasinoColors.InkTitle, TextStyles.Headline);
            var stackHeight = Typography.LineHeight(TextStyles.Title3);
            VenueArt.Line(drawList, amounts.Value(seat.Stack),
                new Vector2(center.X, nameTop + nameHeight + stackHeight * 0.5f), nameWidth, CasinoColors.Money,
                TextStyles.Title3);
            if (seat.Bet > 0)
            {
                VenueArt.Line(drawList, texts.Number(L.Venue.BroadcastBet, seat.Bet),
                    new Vector2(center.X, center.Y - cardHeight * 0.5f - 12f * scale), nameWidth,
                    CasinoColors.InkBody, TextStyles.Subheadline);
            }
        }
    }

    private static void DrawRow(ImDrawListPtr drawList, int[] cards, Vector2 center, float cardWidth, float scale,
        float alpha = 1f)
    {
        if (cards.Length == 0)
        {
            return;
        }

        var cardHeight = PlayingCards.HeightFor(cardWidth);
        var step = cardWidth * (1f - CardOverlap);
        var total = cardWidth + step * (cards.Length - 1);
        var left = center.X - total * 0.5f;
        var rounding = PlayingCards.RoundingFor(cardWidth);
        for (var index = 0; index < cards.Length; index++)
        {
            var min = new Vector2(left + step * index, center.Y - cardHeight * 0.5f);
            var rect = new Rect(min, min + new Vector2(cardWidth, cardHeight));
            if (PlayingCards.IsCard(cards[index]))
            {
                PlayingCards.DrawFace(drawList, rect, cards[index], rounding, scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
            }

            if (alpha < 1f)
            {
                Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f - alpha)));
            }
        }
    }
}

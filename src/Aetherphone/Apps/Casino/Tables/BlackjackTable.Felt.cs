using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class BlackjackTable
{
    private const float BetFlightSeconds = 0.4f;
    private const float PlatePopSeconds = 0.2f;
    private const float SettleFlightSeconds = 0.55f;
    private const float BadgePopSeconds = 0.25f;
    private const float HeroRaiseSmoothing = 0.12f;
    private const float TitleDrop = 6f;
    private const float SurrenderWidth = 0.34f;
    private const float OpeningHandSpan = 1.45f;
    private const float StateRingRadius = 11f;
    private const float HeroColumnSink = 3f;
    private const float StateLineShare = 0.84f;

    private static readonly Vector4 PillFill = new(0f, 0f, 0f, 0.35f);

    private void DrawFelt(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        long turnRemaining, float delta, float phase, float scale)
    {
        var shoe = layout.ShoeAnchor;
        BlackjackTableArt.DrawShoe(drawList, shoe, scale);
        DrawDealerPuck(drawList, ui, board, phase, scale);
        DrawDealerCards(drawList, ui, board, scale);
        DrawStateBand(drawList, ui, board, turnRemaining, scale);
        var tapped = DrawRail(drawList, ui, board, turnRemaining, scale);
        var state = chips.State;
        if (state is not null && BlackjackRules.IsSeat(tapped) && tapped < SeatLimit(board)
            && seatViews[tapped].Phase == SeatPhase.Empty)
        {
            TapEmptySeat(tapped, state, board);
        }

        DrawHero(drawList, ui, board, turnRemaining, delta, scale);
        DrawSurrenderPill(ui, board, scale);
        dealer.DrawHand(drawList, shoe, delta, scale);
    }

    private void DrawStateBand(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, long remaining,
        float scale)
    {
        var band = layout.StateBand;
        var center = band.Center;
        var maxWidth = band.Width * StateLineShare;
        if (DrawAnnouncement(drawList, center, maxWidth, scale))
        {
            return;
        }

        if (BlackjackPhases.Over(board.Phase) && BlackjackRecap.PlayedSeats(board) > 0)
        {
            StageText.Status(drawList, center, Loc.T(L.Blackjack.RecapCaption), maxWidth, scale);
            return;
        }

        DrawStateLine(drawList, ui, board, center, remaining, scale);
    }

    private void DrawStateLine(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        Vector2 bandCenter, long remaining, float scale)
    {
        var y = bandCenter.Y;
        var timed = remaining > 0 && (board.Phase == BlackjackPhases.Betting || board.InsuranceOpen);
        var ringRadius = StateRingRadius * scale;
        var reserve = timed ? (ringRadius * 2f + Metrics.Space.Sm * scale) : 0f;
        var maxWidth = layout.Felt.Width * StateLineShare - reserve;
        var fit = StageText.FitScale(stateLabel, maxWidth, TextStyles.Title2, StageTextRole.State);
        var width = MathF.Min(maxWidth, Typography.Measure(stateLabel, fit, TextStyles.Title2.Weight).X);
        var center = new Vector2(bandCenter.X + reserve * 0.5f, y);
        StageText.StateLine(drawList, center, stateLabel, maxWidth, StageText.Strong);
        if (!timed)
        {
            return;
        }

        var ringCenter = new Vector2(center.X - width * 0.5f - Metrics.Space.Sm * scale - ringRadius, y);
        TurnTimerRing.Draw(drawList, ringCenter, ringRadius, remaining, TimerWindow(board), ui.Accent, scale);
    }

    private void DrawDealerPuck(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, float phase,
        float scale)
    {
        var puck = layout.DealerPuck;
        var radius = layout.DealerPuckPixels;
        if (BlackjackHosting.HostDeals(board) && board.DealerName.Length > 0)
        {
            AvatarView.DrawRemote(drawList, puck, radius, ui.Theme, board.DealerName, string.Empty, string.Empty,
                images, lodestone, 1f, 32);
            drawList.AddCircle(puck, radius, ImGui.GetColorU32(CasinoColors.Money), 32, MathF.Max(1f, 1.4f * scale));
        }
        else
        {
            BlackjackDealer.DrawPuck(drawList, puck, radius, phase, scale);
        }

        dealer.DrawSpeech(drawList, layout.SpeechArea, puck, radius, scale);
    }

    private void DrawDealerCards(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, float scale)
    {
        var fanCenter = layout.DealerFanCenter;
        var cards = board.DealerCards;
        var count = cards?.Length ?? 0;
        if (count == 0)
        {
            return;
        }

        var cardWidth = layout.DealerCardPixels;
        var step = BlackjackTableLayout.FanStep(cardWidth, count,
            layout.Felt.Width * BlackjackTableLayout.DealerFanShare);
        var start = fanCenter.X - (count - 1) * step * 0.5f;
        var shoe = layout.ShoeAnchor;
        var rounding = PlayingCards.RoundingFor(cardWidth);
        var reveal = playback.HoleReveal();
        for (var index = 0; index < count; index++)
        {
            var travel = playback.TravelOf(BlackjackDealPlayback.DealerSlot, index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = new Vector2(start + index * step, fanCenter.Y);
            var card = cards![index];
            if (index == 1 && playback.HoleRevealing())
            {
                var squashed = SquashedRect(target, cardWidth, BlackjackDealChoreography.RevealScaleX(reveal));
                if (BlackjackDealChoreography.RevealFaceUp(reveal) && PlayingCards.IsCard(card))
                {
                    PlayingCards.DrawFace(drawList, squashed, card, rounding, scale, true);
                }
                else
                {
                    PlayingCards.DrawBack(drawList, squashed, rounding, scale, true);
                }

                continue;
            }

            var center = BlackjackDealChoreography.Position(shoe, target, travel, scale);
            dealer.TrackCard(center, travel);
            var rect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
            }
        }

        if (board.DealerTotal <= 0)
        {
            return;
        }

        var pillCenter = new Vector2(fanCenter.X, layout.DealerTotalY);
        BlackjackTableArt.DrawTotalPill(drawList, pillCenter, TotalLabel(board.DealerTotal, board.DealerSoft, false),
            PillFill, ui.TitleInk, scale);
    }

    private static Rect SquashedRect(Vector2 center, float width, float scaleX)
    {
        var halfWidth = width * 0.5f * scaleX;
        var halfHeight = PlayingCards.HeightFor(width) * 0.5f;
        return new Rect(new Vector2(center.X - halfWidth, center.Y - halfHeight),
            new Vector2(center.X + halfWidth, center.Y + halfHeight));
    }

    private string TotalLabel(int total, bool soft, bool live)
    {
        return soft && live && total < BlackjackRules.TargetTotal
            ? text.Count(L.Blackjack.SoftTotal, total)
            : GameNumber.Label(total);
    }

    private int DrawRail(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, long turnRemaining,
        float scale)
    {
        var columnWidth = layout.ColumnWidth;
        var puckRadius = layout.PuckRadius;
        var shoe = layout.ShoeAnchor;
        var dealerAnchor = layout.DealerFanCenter;
        var seated = BlackjackRules.IsSeat(mySeat);
        var currency = CasinoCurrencies.Of(board);
        var limit = SeatLimit(board);
        var tapped = -1;
        var sitLabel = Loc.T(L.Blackjack.SeatSit);
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            if ((seated && seatIndex == mySeat) || seatIndex >= limit)
            {
                continue;
            }

            var slot = BlackjackTableLayout.RailSlotOf(seatIndex, mySeat, limit);
            var puck = layout.RailPuckCenter(slot);
            var view = seatViews[seatIndex];
            if (view.Phase == SeatPhase.Empty)
            {
                if (SeatSpot.DrawEmpty(drawList, puck, layout.SpotRadius, seated ? string.Empty : sitLabel,
                        CasinoColors.Money, !seated, layout.SpotScale))
                {
                    tapped = seatIndex;
                }

                continue;
            }

            var acting = view.Phase == SeatPhase.Acting;
            if (acting)
            {
                BlackjackTableArt.DrawActingGlow(drawList, puck, puckRadius * 2.2f, ui.Accent);
            }

            var dimmed = view.Phase == SeatPhase.Away || view.Phase == SeatPhase.Out || !view.Connected;
            AvatarView.DrawRemote(drawList, puck, puckRadius, ui.Theme, view.DisplayName, string.Empty,
                view.AvatarUrl, images, lodestone, 1f, 32, dimmed ? 0.45f : 1f, Frames.Of(view.FrameId));
            if (acting)
            {
                TurnTimerRing.Draw(drawList, puck, puckRadius + 4f * scale, turnRemaining, board.WindowSeconds,
                    ui.Accent, scale);
            }

            if (!view.Connected)
            {
                drawList.AddCircleFilled(new Vector2(puck.X + puckRadius * 0.7f, puck.Y - puckRadius * 0.7f),
                    3.4f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.45f + 0.4f * Pulse.Wave(Pulse.Breath))), 12);
            }

            DrawSeatPlate(drawList, currency, view, puck, puckRadius, columnWidth - 2f * scale, dimmed, scale);
            DrawBalanceTitle(drawList, currency, view.Stack, puck, puckRadius, columnWidth, scale);

            DrawBetDisplay(drawList, ui, seatIndex, currency, puck,
                new Vector2(puck.X, puck.Y - layout.RailBetLift), dealerAnchor, false, scale);
            DrawRailHands(drawList, ui, board, seatIndex, puck, columnWidth, shoe, scale);
        }

        return tapped;
    }

    private void DrawSeatPlate(ImDrawListPtr drawList, int currency, in SeatView view, Vector2 puck, float puckRadius,
        float width, bool dimmed, float scale)
    {
        var top = puck.Y + layout.RailPlateTop;
        var min = new Vector2(puck.X - width * 0.5f, top);
        var max = new Vector2(puck.X + width * 0.5f, top + BlackjackTableLayout.RailPlateHeight * scale);
        var rounding = Metrics.Radius.Md * scale;
        Material.LiquidGlass(drawList, min, max, rounding, scale, GlassTone.Dark, 0f);
        var inner = width - 6f * scale;
        var lineHeight = (max.Y - min.Y) * 0.5f;
        var name = Typography.FitText(view.DisplayName, inner, TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2(puck.X, min.Y + lineHeight * 0.55f), name,
            dimmed ? StageText.Body : StageText.Strong, TextStyles.Footnote);
        var stack = StackLabel(currency, view.Stack, true);
        var style = TextStyles.FootnoteEmphasized;
        var fit = StageText.FitScale(stack, inner, style, StageTextRole.Label);
        Typography.DrawCentered(drawList, new Vector2(puck.X, min.Y + lineHeight * 1.45f),
            Typography.FitText(stack, inner, fit, style.Weight), dimmed ? StageText.Body : StackInk(currency), fit,
            style.Weight);
    }

    private static void DrawBalanceTitle(ImDrawListPtr drawList, int currency, long stack, Vector2 puck,
        float puckRadius, float maxWidth, float scale)
    {
        if (currency != CasinoCurrencies.Chips)
        {
            return;
        }

        var title = StatusTitle.For(stack);
        if (title == BalanceTitle.None)
        {
            return;
        }

        StatusTitle.Draw(drawList, new Vector2(puck.X, puck.Y + puckRadius - TitleDrop * scale), title, maxWidth,
            scale);
    }

    private string StackLabel(int currency, long amount, bool compact)
    {
        if (currency == CasinoCurrencies.Gil)
        {
            return hostedText.Gil(amount);
        }

        return compact ? NumberText.Compact(amount) : NumberText.Group(amount);
    }

    private static Vector4 StackInk(int currency)
    {
        return currency == CasinoCurrencies.Practice ? CasinoColors.Practice : CasinoColors.Money;
    }

    private void DrawRailHands(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, int seatIndex,
        Vector2 puck, float columnWidth, Vector2 shoe, float scale)
    {
        var hands = projection.HandsAt(seatIndex);
        if (hands.Length == 0)
        {
            return;
        }

        var count = hands.Length < BlackjackRules.MaxHandsPerSeat ? hands.Length : BlackjackRules.MaxHandsPerSeat;
        var cardWidth = (count > 1 ? BlackjackTableLayout.RailSplitCardWidth : BlackjackTableLayout.RailCardWidth)
            * scale;
        var handWidth = columnWidth / count;
        var fanY = puck.Y - layout.RailCardsLift;
        var activeHand = board.ActiveSeat == seatIndex ? board.ActiveHand : -1;
        for (var handIndex = 0; handIndex < count; handIndex++)
        {
            var hand = hands[handIndex];
            var fanCenter = new Vector2(puck.X + (handIndex - (count - 1) * 0.5f) * handWidth, fanY);
            DrawHandFan(drawList, seatIndex, handIndex, hand, fanCenter, cardWidth, handWidth - 3f * scale, shoe,
                scale);
            var outcomeText = OutcomeLabel(hand);
            var badgeEntrance = BadgeEntrance(seatIndex);
            var badgeShown = hand.Outcome != BlackjackOutcomes.Pending && outcomeText.Length > 0
                && badgeEntrance > 0f;
            if (badgeShown)
            {
                var won = hand.Delta > 0;
                BlackjackTableArt.DrawOutcomeBadge(drawList,
                    fanCenter, outcomeText,
                    won ? CasinoColors.Money : ui.TitleInk, won ? CasinoColors.Money : ui.BodyInk, badgeEntrance,
                    handWidth, scale);
            }
            else if (hand.Total > 0)
            {
                var ink = hand.Outcome == BlackjackOutcomes.Bust
                    ? ui.MutedInk
                    : handIndex == activeHand ? ui.Accent : ui.TitleInk;
                BlackjackTableArt.DrawTotalPill(drawList, fanCenter, GameNumber.Label(hand.Total), PillFill, ink,
                    scale);
            }
        }
    }

    private void DrawHandFan(ImDrawListPtr drawList, int seatIndex, int handIndex, CasinoBlackjackHandDto hand,
        Vector2 fanCenter, float cardWidth, float maxWidth, Vector2 shoe, float scale)
    {
        var cards = hand.Cards;
        var count = cards?.Length ?? 0;
        if (count == 0)
        {
            return;
        }

        var step = BlackjackTableLayout.FanStep(cardWidth, count, maxWidth);
        var start = fanCenter.X - (count - 1) * step * 0.5f;
        var slot = BlackjackDealPlayback.SlotOf(seatIndex, handIndex);
        var rounding = PlayingCards.RoundingFor(cardWidth);
        for (var index = 0; index < count; index++)
        {
            var travel = playback.TravelOf(slot, index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = new Vector2(start + index * step, fanCenter.Y);
            var center = BlackjackDealChoreography.Position(shoe, target, travel, scale);
            dealer.TrackCard(center, travel);
            var rect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            var card = projection.CardAt(seatIndex, handIndex, index, cards![index]);
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
            }
        }
    }

    private void DrawHero(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, long turnRemaining,
        float delta, float scale)
    {
        if (!BlackjackRules.IsSeat(mySeat))
        {
            DrawHeroGhostSlots(drawList, scale);
            return;
        }

        var currency = CasinoCurrencies.Of(board);
        var puckCenter = DrawCapsule(drawList, ui, board, layout.CapsuleCenter, turnRemaining, delta, scale);
        var hands = projection.HandsAt(mySeat);
        var shoe = layout.ShoeAnchor;
        var myTurn = board.ActiveSeat == mySeat && board.Phase == BlackjackPhases.PlayerTurns;
        var cardWidth = layout.HeroCardPixels(hands.Length);
        var slotWidth = layout.HeroSlotWidth(hands.Length);
        var live = !BlackjackPhases.Over(board.Phase);
        if (hands.Length == 0)
        {
            DrawHeroGhostSlots(drawList, scale);
        }

        for (var handIndex = 0; handIndex < hands.Length && handIndex < heroRaise.Length; handIndex++)
        {
            var hand = hands[handIndex];
            var active = myTurn && board.ActiveHand == handIndex;
            var raise = heroRaise[handIndex].Step(active ? -layout.RaisePixels : 0f, HeroRaiseSmoothing, delta);
            var fanCenter = layout.HeroHandCenter(hands.Length, handIndex);
            fanCenter.Y += raise;
            if (active)
            {
                BlackjackTableArt.DrawActingGlow(drawList, fanCenter, cardWidth * 1.5f, ui.Accent);
            }

            DrawHandFan(drawList, mySeat, handIndex, hand, fanCenter, cardWidth,
                slotWidth - BlackjackTableLayout.HeroSlotPad * scale, shoe, scale);
            if (hand.Total > 0)
            {
                var fill = active ? Palette.WithAlpha(ui.Accent, 0.30f) : PillFill;
                var ink = hand.Outcome == BlackjackOutcomes.Bust
                    ? ui.MutedInk
                    : hand.Total == BlackjackRules.TargetTotal ? CasinoColors.Money : ui.TitleInk;
                BlackjackTableArt.DrawTotalCapsule(drawList, new Vector2(fanCenter.X, layout.HeroTotalY),
                    TotalLabel(hand.Total, hand.Soft, live && hand.Outcome == BlackjackOutcomes.Pending), fill, ink,
                    active, scale);
            }

            var outcomeText = OutcomeLabel(hand);
            if (hand.Outcome != BlackjackOutcomes.Pending && outcomeText.Length > 0)
            {
                var won = hand.Delta > 0;
                BlackjackTableArt.DrawOutcomeBadge(drawList, fanCenter, outcomeText,
                    won ? CasinoColors.Money : ui.TitleInk, won ? CasinoColors.Money : ui.BodyInk,
                    BadgeEntrance(mySeat), slotWidth, scale);
            }
        }

        DrawBetSpots(drawList, ui, board, scale);
        DrawBetDisplay(drawList, ui, mySeat, currency, puckCenter, layout.BetSpot, layout.DealerFanCenter, true,
            scale);
    }

    private void DrawBetDisplay(ImDrawListPtr drawList, AppSkin ui, int seatIndex, int currency, Vector2 origin,
        Vector2 anchor, Vector2 dealerAnchor, bool heroChips, float scale)
    {
        ref readonly var motion = ref motions[seatIndex];
        var practice = currency == CasinoCurrencies.Practice;
        if (motion.ShownBet > 0)
        {
            var flight = motion.BetClock / BetFlightSeconds;
            BlackjackTableArt.DrawFlightDisc(drawList, origin, anchor, flight,
                BlackjackTableArt.DiscColor(motion.ShownBet, practice), scale);
            var entrance = (motion.BetClock - BetFlightSeconds) / PlatePopSeconds;
            if (entrance > 0f)
            {
                if (heroChips)
                {
                    BlackjackTableArt.DrawChipColumn(drawList,
                        new Vector2(anchor.X, anchor.Y + HeroColumnSink * scale), motion.ShownBet, practice, scale);
                    BlackjackTableArt.DrawBetPlate(drawList, new Vector2(anchor.X, layout.BetPlateY),
                        motion.ShownBet, ui.TitleInk, entrance, practice, layout.MainPlateMaxWidth, scale);
                }
                else
                {
                    BlackjackTableArt.DrawBetPlate(drawList, anchor, motion.ShownBet, ui.TitleInk, entrance,
                        practice, layout.ColumnWidth - 2f * scale, scale);
                }
            }
        }

        if (!motion.SettleStarted || motion.SettleSign == 0 || motion.SettleClock >= SettleFlightSeconds)
        {
            return;
        }

        var progress = motion.SettleClock / SettleFlightSeconds;
        if (motion.SettleSign > 0)
        {
            BlackjackTableArt.DrawFlightDisc(drawList, dealerAnchor, anchor, progress,
                practice ? CasinoColors.Practice : CasinoColors.Money, scale);
            return;
        }

        BlackjackTableArt.DrawFlightDisc(drawList, anchor, dealerAnchor, progress,
            BlackjackTableArt.DiscColor(motion.ShownBet > 0 ? motion.ShownBet : BlackjackRules.MinBet, practice),
            scale);
    }

    private float BadgeEntrance(int seatIndex)
    {
        ref readonly var motion = ref motions[seatIndex];
        if (!motion.SettleStarted)
        {
            return 1f;
        }

        var entrance = (motion.SettleClock - SettleFlightSeconds * 0.5f) / BadgePopSeconds;
        return Math.Clamp(entrance, 0f, 1f);
    }

    private Vector2 DrawCapsule(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        Vector2 center, long turnRemaining, float delta, float scale)
    {
        var view = seatViews[mySeat];
        var height = layout.CapsulePixels;
        var puckRadius = layout.CapsulePuckPixels;
        stackRoll.Update(view.Stack, delta);
        var currency = CasinoCurrencies.Of(board);
        var stackLabel = StackLabel(currency, stackRoll.Display, false);
        var pad = 10f * scale;
        var title = currency == CasinoCurrencies.Chips ? StatusTitle.For(view.Stack) : BalanceTitle.None;
        var titleWidth = StatusTitle.Width(title, layout.CapsuleMaxWidth * 0.4f, scale);
        var titleReserve = titleWidth > 0f ? titleWidth + pad : 0f;
        var stackSize = Typography.Measure(stackLabel, TextStyles.Title3);
        var stackReserve = currency == CasinoCurrencies.Gil ? 0f : CurrencyGlyph.Reserve(stackSize.Y);
        var chrome = puckRadius * 2f + pad * 2.75f + titleReserve;
        var nameRoom = MathF.Max(stackReserve + stackSize.X, layout.CapsuleMaxWidth - chrome);
        var name = Typography.FitText(view.DisplayName, nameRoom, TextStyles.Footnote);
        var nameSize = Typography.Measure(name, TextStyles.Footnote);
        var textWidth = MathF.Max(nameSize.X, stackReserve + stackSize.X);
        var halfWidth = (chrome + textWidth) * 0.5f;
        var min = new Vector2(center.X - halfWidth, center.Y - height * 0.5f);
        var max = new Vector2(center.X + halfWidth, center.Y + height * 0.5f);
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, CasinoColors.FeltBottom, 0.92f);
        var puck = new Vector2(min.X + pad + puckRadius, center.Y);
        var dimmed = !view.Connected;
        AvatarView.DrawRemote(drawList, puck, puckRadius, ui.Theme, view.DisplayName, string.Empty, view.AvatarUrl,
            images, lodestone, 1f, 32, dimmed ? 0.45f : 1f, Frames.Of(view.FrameId));
        if (board.ActiveSeat == mySeat && board.Phase == BlackjackPhases.PlayerTurns)
        {
            TurnTimerRing.Draw(drawList, puck, puckRadius + 3.5f * scale, turnRemaining, board.WindowSeconds,
                ui.Accent, scale);
        }

        var textX = puck.X + puckRadius + pad * 0.75f;
        var nameTop = center.Y - (nameSize.Y + stackSize.Y) * 0.5f;
        var stackTop = nameTop + nameSize.Y;
        Typography.Draw(drawList, new Vector2(textX, nameTop), name, StageText.Strong, TextStyles.Footnote);
        var glyphSize = stackSize.Y * CurrencyGlyph.GlyphFraction;
        var glyphCenter = new Vector2(textX + glyphSize * 0.5f, stackTop + stackSize.Y * 0.5f);
        if (currency == CasinoCurrencies.Practice)
        {
            ChipStack.DrawPractice(drawList, glyphCenter, glyphSize * 0.5f);
        }
        else if (currency == CasinoCurrencies.Chips)
        {
            CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, glyphCenter, glyphSize);
        }

        Typography.Draw(drawList, new Vector2(textX + stackReserve, stackTop), stackLabel, StackInk(currency),
            TextStyles.Title3.Scale * stackRoll.PopScale, TextStyles.Title3.Weight);
        if (titleWidth > 0f)
        {
            StatusTitle.Draw(drawList, new Vector2(max.X - pad - titleWidth * 0.5f, center.Y), title, titleWidth,
                scale);
        }

        return puck;
    }

    private void DrawHeroGhostSlots(ImDrawListPtr drawList, float scale)
    {
        var cardWidth = layout.HeroCardPixels(1);
        var height = PlayingCards.HeightFor(cardWidth);
        var step = cardWidth * 0.45f;
        var rounding = PlayingCards.RoundingFor(cardWidth);
        var fanY = layout.HeroFanY;
        for (var index = 0; index < 2; index++)
        {
            var centerX = layout.Felt.Center.X + (index - 0.5f) * step;
            var min = new Vector2(centerX - cardWidth * 0.5f, fanY - height * 0.5f);
            PlayingCards.DrawSlot(drawList, new Rect(min, min + new Vector2(cardWidth, height)), rounding, scale);
        }
    }

    private string OutcomeLabel(CasinoBlackjackHandDto hand)
    {
        if (hand.Surrendered || hand.Outcome == BlackjackOutcomes.Surrender)
        {
            return Loc.T(L.Blackjack.OutcomeSurrender);
        }

        if (hand.Outcome == BlackjackOutcomes.Blackjack)
        {
            return Loc.T(L.Casino.BlackjackSeatNatural);
        }

        if (hand.Outcome == BlackjackOutcomes.Push)
        {
            return Loc.T(L.Casino.BlackjackSeatPush);
        }

        if (hand.Outcome == BlackjackOutcomes.Bust)
        {
            return Loc.T(L.Casino.BlackjackSeatBust);
        }

        return hand.Delta > 0 ? NumberText.Signed(hand.Delta) : string.Empty;
    }

    private void DrawSurrenderPill(AppSkin ui, CasinoBlackjackRoomStateDto board, float scale)
    {
        if (!BlackjackRules.IsSeat(mySeat) || board.ActiveSeat != mySeat
            || !BlackjackRules.Allows(projection.ActionsMask, BlackjackRules.ActionSurrender))
        {
            return;
        }

        var felt = layout.Felt;
        var centerY = layout.SurrenderCenterY;
        var height = MathF.Min(Button.LargeHeight * scale, layout.SurrenderBand);
        var handHalf = layout.HeroCardPixels(1) * OpeningHandSpan * 0.5f
            + BlackjackTableLayout.HeroSlotPad * scale;
        var right = felt.Max.X - BlackjackTableLayout.SideInset * 2f * scale;
        var width = MathF.Min(felt.Width * SurrenderWidth, right - felt.Center.X - handHalf);
        if (width <= height)
        {
            return;
        }

        var rect = new Rect(new Vector2(right - width, centerY - height * 0.5f),
            new Vector2(right, centerY + height * 0.5f));
        var legal = !rooms.StakeInFlight;
        if (!AppSkin.StackedPillButton(rect, Loc.T(L.Blackjack.SurrenderPill), Loc.T(L.Blackjack.SurrenderHalfBack),
                false, legal, ui.Ink) || !legal)
        {
            return;
        }

        inlineReason = string.Empty;
        rooms.SendBlackjackAction(BlackjackRules.ActionSurrender);
        CasinoSfx.Play(UiSound.ChipSlide);
    }
}

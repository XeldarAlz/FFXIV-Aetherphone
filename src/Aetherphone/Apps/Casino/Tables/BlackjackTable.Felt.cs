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
    private const float StateRingRadius = 11f;
    private const float HeroColumnSink = 3f;
    private const float StateLineShare = 0.84f;
    private const string StateMarquee = "casino.blackjack.state";
    private const string EvenDelta = "±0";

    private static readonly Vector4 PillFill = new(0f, 0f, 0f, 0.35f);

    private void DrawFelt(CasinoStage stage, ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        long turnRemaining, float delta, float phase, float scale)
    {
        var shoe = layout.ShoeAnchor;
        BlackjackTableArt.DrawShoe(drawList, shoe, layout.ShoePixels, scale);
        DrawDealerPuck(drawList, ui, board, phase, scale);
        DrawDealerCards(drawList, ui, board, scale);
        DrawStateBand(drawList, ui, board, turnRemaining, scale);
        var tapped = DrawRail(drawList, ui, board, turnRemaining, scale);
        var state = chips.State;
        if (state is not null && BlackjackRules.IsSeat(tapped) && tapped < SeatLimit(board)
            && seatViews[tapped].Phase == SeatPhase.Empty)
        {
            TapEmptySeat(stage, tapped, state, board);
        }

        DrawHero(drawList, ui, board, turnRemaining, delta, scale);
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
        var center = new Vector2(bandCenter.X + reserve * 0.5f, y);
        var stateStyle = StageText.Minimum(StageTextRole.State);
        var titleWidth = Typography.Measure(stateLabel, stateStyle).X;
        float width;
        if (StageText.Fits(titleWidth, maxWidth))
        {
            StageText.StateLine(drawList, center, stateLabel, maxWidth, StageText.Strong);
            width = titleWidth;
        }
        else
        {
            width = StageText.Status(drawList, center, stateLabel, maxWidth, StateMarquee).X;
        }

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

        var label = TotalLabel(board.DealerTotal, board.DealerSoft, false);
        var pillX = fanCenter.X;
        if (layout.Compact)
        {
            var fanRight = start + (count - 1) * step + cardWidth * 0.5f;
            pillX = layout.DealerTotalBesideX(fanRight, BlackjackTableArt.TotalPillHalfWidth(label, scale));
        }

        BlackjackTableArt.DrawTotalPill(drawList, new Vector2(pillX, layout.DealerTotalY), label, PillFill,
            ui.TitleInk, scale);
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
        var canSit = !seated && !dealing && !banking;
        var currency = CasinoCurrencies.Of(board);
        var limit = SeatLimit(board);
        var tapped = -1;
        var sitLabel = Loc.T(L.Blackjack.SeatSit);
        Span<bool> emptySlots = stackalloc bool[BlackjackRules.SeatCount];
        MarkEmptySlots(emptySlots, seated, limit);
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            if (!OnRail(seatIndex, seated, limit))
            {
                continue;
            }

            var slot = BlackjackTableLayout.RailSlotOf(seatIndex, mySeat, limit);
            var puck = layout.RailPuckCenter(slot);
            var view = seatViews[seatIndex];
            if (view.Phase == SeatPhase.Empty)
            {
                if (SeatSpot.DrawEmpty(drawList, puck, layout.SpotRadius, canSit ? sitLabel : string.Empty,
                        CasinoColors.Money, canSit, layout.SpotScale))
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
                TurnTimerRing.Draw(drawList, puck, puckRadius + BlackjackTableLayout.RailRingGap * scale,
                    turnRemaining, board.WindowSeconds, ui.Accent, scale);
                BlackjackTableArt.DrawTimeBankPips(drawList, BlackjackTableLayout.TimeBankPips(puck, puckRadius, scale),
                    projection.SeatAt(seatIndex)?.TimeBankLeft ?? 0, CasinoHostingRules.TimeBankUses, scale);
            }

            if (!view.Connected)
            {
                drawList.AddCircleFilled(new Vector2(puck.X + puckRadius * 0.7f, puck.Y - puckRadius * 0.7f),
                    3.4f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.45f + 0.4f * Pulse.Wave(Pulse.Breath))), 12);
            }

            var plate = layout.RailPlateRect(slot, SlotEmpty(emptySlots, slot - 1), SlotEmpty(emptySlots, slot + 1));
            DrawSeatPlate(drawList, currency, view, plate, dimmed, scale);
            if (layout.ShowsRailTitles)
            {
                DrawBalanceTitle(drawList, currency, view.Stack, puck, puckRadius, columnWidth, scale);
            }

            DrawRailSideStakes(drawList, board, seatIndex, slot, scale);

            DrawBetDisplay(drawList, ui, seatIndex, currency, puck,
                new Vector2(puck.X, puck.Y - layout.RailBetLift), dealerAnchor, false,
                !SettledBadgeShows(projection.HandsAt(seatIndex), BadgeEntrance(seatIndex)), scale);
            DrawRailHands(drawList, ui, board, seatIndex, puck, shoe, scale);
        }

        return tapped;
    }

    private void MarkEmptySlots(Span<bool> emptySlots, bool seated, int limit)
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            if (!OnRail(seatIndex, seated, limit))
            {
                continue;
            }

            emptySlots[BlackjackTableLayout.RailSlotOf(seatIndex, mySeat, limit)] =
                seatViews[seatIndex].Phase == SeatPhase.Empty;
        }
    }

    private bool OnRail(int seatIndex, bool seated, int limit) => (!seated || seatIndex != mySeat) && seatIndex < limit;

    private static bool SlotEmpty(ReadOnlySpan<bool> emptySlots, int slot) =>
        slot >= 0 && slot < emptySlots.Length && emptySlots[slot];

    private void DrawSeatPlate(ImDrawListPtr drawList, int currency, in SeatView view, in Rect plate, bool dimmed,
        float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        Material.LiquidGlass(drawList, plate.Min, plate.Max, rounding, scale, GlassTone.Dark, 0f);
        var textWidth = layout.RailPlateTextWidthFor(plate.Width);
        var centerX = plate.Center.X;
        var lineHeight = plate.Height * 0.5f;
        var nameStyle = TextStyles.Footnote;
        var name = RailName(view.SeatIndex, view.DisplayName, textWidth);
        var nameFit = StageText.FitScale(name, textWidth, nameStyle, StageTextRole.Caption);
        Typography.DrawCentered(drawList, new Vector2(centerX, plate.Min.Y + lineHeight * 0.55f),
            Typography.FitText(name, textWidth, nameFit, nameStyle.Weight),
            dimmed ? StageText.Body : StageText.Strong, nameFit, nameStyle.Weight);
        var stackStyle = TextStyles.FootnoteEmphasized;
        var stack = RailStack(currency, view.Stack, textWidth, stackStyle);
        var stackFit = StageText.FitScale(stack, textWidth, stackStyle, StageTextRole.Caption);
        Typography.DrawCentered(drawList, new Vector2(centerX, plate.Min.Y + lineHeight * 1.45f),
            Typography.FitText(stack, textWidth, stackFit, stackStyle.Weight),
            dimmed ? StageText.Body : StackInk(currency), stackFit, stackStyle.Weight);
    }

    private string RailStack(int currency, long amount, float width, in TextStyle style)
    {
        var label = StackLabel(currency, amount, true);
        if (currency != CasinoCurrencies.Gil || StageText.Fits(Typography.Measure(label, style).X, width))
        {
            return label;
        }

        return NumberText.Compact(amount);
    }

    private void DrawRailSideStakes(ImDrawListPtr drawList, CasinoBlackjackRoomStateDto board, int seatIndex,
        int slot, float scale)
    {
        if (!BlackjackRecap.SideBetsOffered(board))
        {
            return;
        }

        var sides = projection.SeatAt(seatIndex)?.SideBets;
        if (sides is null)
        {
            return;
        }

        var both = sides.PerfectPairs > 0 && sides.TwentyOnePlusThree > 0;
        if (sides.PerfectPairs > 0)
        {
            DrawRailSideStake(drawList, layout.RailSideStakeRect(slot, BlackjackSideBet.PerfectPairs, both),
                seatIndex, BlackjackSideBet.PerfectPairs, sides.PerfectPairs, scale);
        }

        if (sides.TwentyOnePlusThree > 0)
        {
            DrawRailSideStake(drawList, layout.RailSideStakeRect(slot, BlackjackSideBet.TwentyOnePlusThree, both),
                seatIndex, BlackjackSideBet.TwentyOnePlusThree, sides.TwentyOnePlusThree, scale);
        }
    }

    private void DrawRailSideStake(ImDrawListPtr drawList, in Rect rect, int seatIndex, BlackjackSideBet bet,
        long placed, float scale)
    {
        var recap = recaps[seatIndex];
        var result = BadgeEntrance(seatIndex) > 0f
            ? BlackjackRecap.SideResult(recap, bet, placed)
            : SideSpotResult.Open;
        var label = result == SideSpotResult.Paid
            ? text.Compact(L.Stage.Plus, BlackjackRecap.SideNet(recap, bet))
            : NumberText.Compact(placed);
        BlackjackTableArt.DrawSideStake(drawList, rect, label, result, scale);
    }

    private string RailName(int seatIndex, string displayName, float width)
    {
        if (Typography.Measure(displayName, TextStyles.Footnote).X <= width)
        {
            return displayName;
        }

        if (!ReferenceEquals(railNameSources[seatIndex], displayName))
        {
            railNameSources[seatIndex] = displayName;
            railForenames[seatIndex] = PitText.Forename(displayName);
        }

        return railForenames[seatIndex];
    }

    private static void DrawBalanceTitle(ImDrawListPtr drawList, int currency, long stack, Vector2 puck,
        float puckRadius, float maxWidth, float scale)
    {
        if (currency != CasinoCurrencies.Chips)
        {
            return;
        }

        var title = StatusTitle.For(stack);
        if (!StatusTitle.Fits(title, maxWidth, scale))
        {
            return;
        }

        StatusTitle.Draw(drawList, BlackjackTableLayout.RailTitleCenter(puck, puckRadius, scale), title, maxWidth,
            scale);
    }

    private string StackLabel(int currency, long amount, bool compact)
    {
        if (currency == CasinoCurrencies.Gil)
        {
            return compact ? hostedText.GilCompact(amount) : hostedText.Gil(amount);
        }

        return compact ? NumberText.Compact(amount) : NumberText.Group(amount);
    }

    private static Vector4 StackInk(int currency)
    {
        return currency == CasinoCurrencies.Practice ? CasinoColors.Practice : CasinoColors.Money;
    }

    private void DrawRailHands(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, int seatIndex,
        Vector2 puck, Vector2 shoe, float scale)
    {
        var hands = projection.HandsAt(seatIndex);
        if (hands.Length == 0)
        {
            return;
        }

        var count = hands.Length < BlackjackRules.MaxHandsPerSeat ? hands.Length : BlackjackRules.MaxHandsPerSeat;
        var cardWidth = layout.RailCardPixels(count);
        var handWidth = layout.RailHandWidth(count);
        var fanY = puck.Y - layout.RailCardsLift;
        var badgeY = puck.Y - layout.RailBetLift;
        var activeHand = board.ActiveSeat == seatIndex ? board.ActiveHand : -1;
        for (var handIndex = 0; handIndex < count; handIndex++)
        {
            var hand = hands[handIndex];
            var fanCenter = new Vector2(puck.X + (handIndex - (count - 1) * 0.5f) * handWidth, fanY);
            DrawHandFan(drawList, seatIndex, handIndex, hand, fanCenter, cardWidth, handWidth - 3f * scale, shoe,
                scale);
            if (hand.Total > 0)
            {
                var ink = hand.Outcome == BlackjackOutcomes.Bust
                    ? StageText.Body
                    : handIndex == activeHand ? ui.Accent : ui.TitleInk;
                BlackjackTableArt.DrawTotalPill(drawList, fanCenter, GameNumber.Label(hand.Total), PillFill, ink,
                    scale);
            }

            var outcomeText = RailOutcomeLabel(hand);
            var badgeEntrance = BadgeEntrance(seatIndex);
            if (outcomeText.Length > 0 && badgeEntrance > 0f)
            {
                var tint = hand.Delta > 0 ? CasinoColors.Money : hand.Delta < 0 ? StageText.Body : StageText.Strong;
                BlackjackTableArt.DrawOutcomeBadge(drawList, new Vector2(fanCenter.X, badgeY), outcomeText, tint,
                    tint, badgeEntrance, handWidth, scale);
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
        var revealing = playback.HandRevealing(slot);
        var reveal = playback.HandReveal(slot);
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
            var card = projection.CardAt(seatIndex, handIndex, index, cards![index]);
            if (revealing && travel >= 1f)
            {
                var squashed = SquashedRect(center, cardWidth, BlackjackDealChoreography.RevealScaleX(reveal));
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
    }

    private void DrawHero(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, long turnRemaining,
        float delta, float scale)
    {
        if (!BlackjackRules.IsSeat(mySeat))
        {
            if (!dealing && !banking)
            {
                DrawHeroGhostSlots(drawList, scale);
            }

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
                    ? StageText.Body
                    : hand.Total == BlackjackRules.TargetTotal ? CasinoColors.Money : ui.TitleInk;
                BlackjackTableArt.DrawTotalCapsule(drawList, new Vector2(fanCenter.X, layout.HeroTotalY),
                    TotalLabel(hand.Total, hand.Soft, live && hand.Outcome == BlackjackOutcomes.Pending), fill, ink,
                    active, scale);
            }

            var outcomeText = OutcomeLabel(hand);
            if (hand.Outcome != BlackjackOutcomes.Pending && outcomeText.Length > 0)
            {
                var ink = hand.Delta > 0 ? CasinoColors.Money : StageText.Strong;
                BlackjackTableArt.DrawOutcomeBadge(drawList, fanCenter, outcomeText, ink, ink,
                    BadgeEntrance(mySeat), slotWidth, scale);
            }
        }

        DrawBetSpots(drawList, ui, board, scale);
        DrawBetDisplay(drawList, ui, mySeat, currency, puckCenter, layout.BetSpot, layout.DealerFanCenter, true, true,
            scale);
    }

    internal static bool SettledBadgeShows(ReadOnlySpan<CasinoBlackjackHandDto> hands, float badgeEntrance)
    {
        if (badgeEntrance <= 0f)
        {
            return false;
        }

        var count = Math.Min(hands.Length, BlackjackRules.MaxHandsPerSeat);
        for (var handIndex = 0; handIndex < count; handIndex++)
        {
            if (hands[handIndex].Outcome != BlackjackOutcomes.Pending)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawBetDisplay(ImDrawListPtr drawList, AppSkin ui, int seatIndex, int currency, Vector2 origin,
        Vector2 anchor, Vector2 dealerAnchor, bool heroChips, bool showPlate, float scale)
    {
        ref readonly var motion = ref motions[seatIndex];
        var practice = currency == CasinoCurrencies.Practice;
        if (motion.ShownBet > 0 && showPlate)
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
                        practice, layout.RailBetPlateWidth, scale);
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
        var stackStyle = layout.Compact ? TextStyles.Headline : TextStyles.Title3;
        var stackSize = Typography.Measure(stackLabel, stackStyle);
        var stackReserve = currency == CasinoCurrencies.Gil ? 0f : CurrencyGlyph.Reserve(stackSize.Y);
        var frame = Frames.Of(view.FrameId);
        var frameReach = AvatarView.Reserve(frame, puckRadius);
        var chrome = puckRadius * 2f + frameReach + pad * 2.75f + titleReserve;
        var nameRoom = MathF.Max(stackReserve + stackSize.X, layout.CapsuleMaxWidth - chrome);
        var name = Typography.FitText(view.DisplayName, nameRoom, TextStyles.Footnote);
        var nameSize = Typography.Measure(name, TextStyles.Footnote);
        var textWidth = MathF.Max(nameSize.X, stackReserve + stackSize.X);
        var halfWidth = (chrome + textWidth) * 0.5f;
        var min = new Vector2(center.X - halfWidth, center.Y - height * 0.5f);
        var max = new Vector2(center.X + halfWidth, center.Y + height * 0.5f);
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, CasinoColors.FeltBottom, 0.92f);
        var puck = new Vector2(min.X + pad + frameReach + puckRadius, center.Y);
        var dimmed = !view.Connected;
        AvatarView.DrawRemote(drawList, puck, puckRadius, ui.Theme, view.DisplayName, string.Empty, view.AvatarUrl,
            images, lodestone, 1f, 32, dimmed ? 0.45f : 1f, frame);
        if (board.ActiveSeat == mySeat && board.Phase == BlackjackPhases.PlayerTurns)
        {
            TurnTimerRing.Draw(drawList, puck, puckRadius + BlackjackTableLayout.CapsuleRingGap * scale,
                turnRemaining, board.WindowSeconds, ui.Accent, scale);
            BlackjackTableArt.DrawTimeBankPips(drawList, BlackjackTableLayout.TimeBankPips(puck, puckRadius, scale),
                projection.SeatAt(mySeat)?.TimeBankLeft ?? 0, CasinoHostingRules.TimeBankUses, scale);
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
            stackStyle.Scale * stackRoll.PopScale, stackStyle.Weight);
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
            return hand.Delta > 0
                ? text.Number(L.Blackjack.NaturalPays, hand.Delta)
                : Loc.T(L.Casino.BlackjackSeatNatural);
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

    private string RailOutcomeLabel(CasinoBlackjackHandDto hand)
    {
        if (hand.Outcome == BlackjackOutcomes.Pending)
        {
            return string.Empty;
        }

        if (hand.Delta > 0)
        {
            return text.Compact(L.Stage.Plus, hand.Delta);
        }

        return hand.Delta < 0 ? NumberText.Compact(hand.Delta) : EvenDelta;
    }
}

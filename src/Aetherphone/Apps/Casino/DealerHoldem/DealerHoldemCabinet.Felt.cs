using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal sealed partial class DealerHoldemCabinet
{
    private const float PreviewAlpha = 0.45f;
    private const float StateShare = 0.92f;
    private const float BannerFade = 0.3f;
    private const float IdleCycle = 6f;
    private const float IdleDealSeconds = 0.35f;
    private const float WinGlow = 0.6f;
    private const float WinGlowPulse = 0.4f;

    private void DrawFelt(ImDrawListPtr drawList, in CasinoStageFrame frame, float scale)
    {
        var round = playback.Round;
        if (layout.HasTables)
        {
            DealerHoldemArt.PayTable(drawList, layout.BlindTable, Loc.T(L.DealerHoldem.BlindPays), texts.BlindNames,
                texts.BlindOdds, false, scale);
            DealerHoldemArt.PayTable(drawList, layout.TripsTable, Loc.T(L.DealerHoldem.TripsPays), texts.TripsNames,
                texts.TripsOdds, true, scale);
        }

        BlackjackDealer.DrawPuck(drawList, layout.Puck, layout.PuckRadiusPixels, frame.Phase, scale);
        DrawDealerCards(drawList, round, scale);
        DrawBoard(drawList, round, scale);
        DrawSpots(drawList, round, scale);
        DrawHeroCards(drawList, round, scale);
        DrawFlights(drawList, scale);
        DrawHandNames(drawList, round, scale);
        dealer.DrawSpeech(drawList, new Rect(layout.Safe.Min, new Vector2(layout.Puck.X, layout.Dealer.Max.Y)),
            layout.Puck, layout.PuckRadiusPixels, scale);
        DrawBanner(drawList, scale);
    }

    private void DrawDealerCards(ImDrawListPtr drawList, CasinoDealerHoldemDto? round, float scale)
    {
        var width = layout.DealerCardWidth;
        for (var index = 0; index < DealerHoldemRules.HoleCards; index++)
        {
            var center = layout.DealerCard(index);
            if (round is null || index >= playback.DealerShown)
            {
                DealerHoldemArt.CardSlot(drawList, center, width, scale);
                continue;
            }

            if (Flying(DealerTag + index))
            {
                continue;
            }

            var card = playback.Card(DealerHoldemCueKind.Dealer, index);
            HoldemArt.DrawCard(drawList, center, width, card, playback.Revealed, 1f, scale);
        }
    }

    private void DrawBoard(ImDrawListPtr drawList, CasinoDealerHoldemDto? round, float scale)
    {
        var width = layout.BoardCardWidth;
        for (var index = 0; index < DealerHoldemRules.BoardCards; index++)
        {
            var center = layout.BoardCard(index);
            if (round is null || index >= playback.BoardShown)
            {
                DealerHoldemArt.CardSlot(drawList, center, width, scale);
                continue;
            }

            if (Flying(BoardTag + index))
            {
                DealerHoldemArt.CardSlot(drawList, center, width, scale);
                continue;
            }

            var card = playback.Card(DealerHoldemCueKind.Board, index);
            HoldemArt.DrawCard(drawList, center, width, card, true, 1f, scale,
                highlight: HeroHighlight(round, card));
        }
    }

    private void DrawHeroCards(ImDrawListPtr drawList, CasinoDealerHoldemDto? round, float scale)
    {
        var width = layout.HeroCardWidth;
        if (round is null)
        {
            for (var index = 0; index < DealerHoldemRules.HoleCards; index++)
            {
                DealerHoldemArt.CardSlot(drawList, layout.HeroCard(index), width, scale);
            }

            return;
        }

        for (var index = 0; index < playback.HeroShown && index < DealerHoldemRules.HoleCards; index++)
        {
            if (Flying(HeroTag + index))
            {
                continue;
            }

            var card = playback.Card(DealerHoldemCueKind.Hero, index);
            var pose = HeroPose(index, true);
            var dim = round.Folded ? 0.55f : 1f;
            DrawPose(drawList, pose, card, scale, dim, HeroHighlight(round, card));
        }
    }

    private void DrawFlights(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (!cards.Visible(index))
            {
                continue;
            }

            DrawPose(drawList, cards.Pose(index), cards.Card(index), scale, 1f, false);
        }

        for (var index = 0; index < chips.Count; index++)
        {
            DealerHoldemArt.FlyingChips(drawList, chips.Position(index), chips.Amount(index), chips.Alpha(index),
                scale);
        }
    }

    private static void DrawPose(ImDrawListPtr drawList, in Games.Framework.Cards.CardPose pose, int card, float scale,
        float alpha, bool highlight)
    {
        HoldemArt.DrawCard(drawList, pose.Center, pose.Width, card, pose.FaceUp, pose.Squash, scale, alpha, highlight);
    }

    private bool HeroHighlight(CasinoDealerHoldemDto round, int card) =>
        playback.VerdictShown && !round.Folded && InBest(round.PlayerBest, card);

    private static bool InBest(int[]? best, int card)
    {
        if (best is null || card < 0)
        {
            return false;
        }

        for (var index = 0; index < best.Length; index++)
        {
            if (best[index] == card)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawSpots(ImDrawListPtr drawList, CasinoDealerHoldemDto? round, float scale)
    {
        var radius = layout.CircleRadius;
        var labelWidth = layout.CircleLabelWidth;
        var ante = layout.Circle(DealerHoldemSpot.Ante);
        var blind = layout.Circle(DealerHoldemSpot.Blind);
        DealerHoldemArt.EqualsSign(drawList, (ante + blind) * 0.5f, scale);
        for (var index = 0; index < DealerHoldemRules.SpotCount; index++)
        {
            var spot = (DealerHoldemSpot)index;
            var center = layout.Circle(spot);
            var result = spotResults[index];
            var lit = result == DealerHoldemResult.Win || index == resolvingSpot;
            var glow = result == DealerHoldemResult.Win ? WinGlow + WinGlowPulse * Pulse.Wave(Pulse.Breath) : 0f;
            DealerHoldemArt.Spot(drawList, center, radius, lit, glow, scale);
            var amount = SpotAmount(round, spot, out var alpha);
            if (amount > 0)
            {
                DealerHoldemArt.Stake(drawList, center, radius, amount, NumberText.Compact(amount), alpha, scale);
            }

            var label = spotShort[index].Length > 0 ? spotShort[index] : texts.Spot(spot);
            DealerHoldemArt.SpotLabel(drawList, layout.CircleLabel(spot), label, labelWidth,
                result == DealerHoldemResult.Win);
        }
    }

    private long SpotAmount(CasinoDealerHoldemDto? round, DealerHoldemSpot spot, out float alpha)
    {
        alpha = 1f;
        var index = (int)spot;
        if (round is null || dealing)
        {
            alpha = PreviewAlpha;
            return spot switch
            {
                DealerHoldemSpot.Ante or DealerHoldemSpot.Blind => composer.Amount,
                DealerHoldemSpot.Trips when trips => DealerHoldemRules.TripsFor(composer.Amount),
                _ => 0,
            };
        }

        if (spotResults[index] != DealerHoldemResult.None)
        {
            if (chips.InFlight(index))
            {
                return DealerHoldemPlayback.StakeOf(round, spot);
            }

            return spotShown[index];
        }

        return DealerHoldemPlayback.StakeOf(round, spot);
    }

    private void DrawHandNames(ImDrawListPtr drawList, CasinoDealerHoldemDto? round, float scale)
    {
        if (round is null)
        {
            return;
        }

        var width = layout.Safe.Width * StateShare;
        var heroSettled = playback.HeroShown >= DealerHoldemRules.HoleCards && !playback.Busy;
        if (round.PlayerHand >= 0 && (heroSettled || playback.VerdictShown))
        {
            StageText.Plate(drawList, new Vector2(layout.Safe.Center.X, layout.HeroNameY),
                HoldemHandNames.Describe(round.PlayerHand), width, StageText.Strong, TextStyles.FootnoteEmphasized,
                scale);
        }

        if (!playback.VerdictShown || round.DealerHand < 0)
        {
            return;
        }

        var line = dealerLine.Get(L.DealerHoldem.DealerHas, HoldemHandNames.Describe(round.DealerHand));
        StageText.Plate(drawList, new Vector2(layout.Safe.Center.X, layout.DealerNameY), line, width,
            round.DealerQualifies ? StageText.Strong : StageText.Body, TextStyles.FootnoteEmphasized, scale);
    }

    private void DrawBanner(ImDrawListPtr drawList, float scale)
    {
        if (bannerClock <= 0f)
        {
            return;
        }

        var fade = MathF.Min(1f, bannerClock / BannerFade);
        StageText.Plate(drawList, layout.Board.Center, Loc.T(L.DealerHoldem.NoQualify), layout.Safe.Width * StateShare,
            CasinoColors.InkTitle with { W = fade }, TextStyles.Title3, scale);
    }

    private void DrawStateLine(ImDrawListPtr drawList, float scale)
    {
        var center = new Vector2(layout.Safe.Center.X, layout.StateY);
        var width = layout.Safe.Width * StateShare;
        if (noticeText.Length > 0)
        {
            StageText.Plate(drawList, center, noticeText, width, StageText.Strong, TextStyles.Subheadline, scale);
            return;
        }

        if (resolvingSpot >= 0 && spotLines[resolvingSpot].Length > 0)
        {
            var win = spotResults[resolvingSpot] == DealerHoldemResult.Win;
            StageText.Plate(drawList, center, spotLines[resolvingSpot], width,
                win ? CasinoColors.Money : StageText.Strong, TextStyles.Title3, scale);
            return;
        }

        StageText.StateLine(drawList, center, StateText(), width, StageText.Strong);
    }

    private string StateText()
    {
        var round = playback.Round;
        if (dealing || round is not null && playback.Busy && !DealerHoldemRules.IsOver(round.Phase))
        {
            return Loc.T(L.DealerHoldem.Dealing);
        }

        if (round is null)
        {
            return Loc.T(L.DealerHoldem.PlaceAnte);
        }

        if (playback.Busy)
        {
            return Loc.T(L.DealerHoldem.Showdown);
        }

        return round.Phase switch
        {
            DealerHoldemRules.PhasePreFlop => Loc.T(L.DealerHoldem.DecidePreFlop),
            DealerHoldemRules.PhaseFlop => Loc.T(L.DealerHoldem.DecideFlop),
            DealerHoldemRules.PhaseRiver => Loc.T(L.DealerHoldem.DecideRiver),
            _ => OutcomeText(round),
        };
    }

    private string OutcomeText(CasinoDealerHoldemDto round)
    {
        if (round.Phase == DealerHoldemRules.PhaseVoided)
        {
            return Loc.T(L.DealerHoldem.Voided);
        }

        if (round.Payout > round.Stake)
        {
            return outcomeLabel.Get(round.Capped ? L.DealerHoldem.YouWinCapped : L.DealerHoldem.YouWin,
                NumberText.Compact(round.Payout - round.Stake));
        }

        if (round.Payout == round.Stake)
        {
            return Loc.T(L.DealerHoldem.PushLine);
        }

        return Loc.T(round.Folded ? L.DealerHoldem.Folded : L.DealerHoldem.DealerWins);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleTime += deltaSeconds;
        FeltTable.DrawCloth(drawList, rect, false, false, scale);
        var time = idleTime % IdleCycle;
        var width = MathF.Min(rect.Width / 6.5f, PlayingCards.WidthFor(rect.Height * 0.32f));
        var gap = width * 0.12f;
        var boardLeft = rect.Center.X - (width * 5f + gap * 4f) * 0.5f + width * 0.5f;
        var boardY = rect.Min.Y + rect.Height * 0.36f;
        for (var index = 0; index < DealerHoldemRules.BoardCards; index++)
        {
            var center = new Vector2(boardLeft + index * (width + gap), boardY);
            var shown = time > 1f + index * IdleDealSeconds;
            if (!shown)
            {
                DealerHoldemArt.CardSlot(drawList, center, width, scale);
                continue;
            }

            HoldemArt.DrawCard(drawList, center, width, IdleCard(index), true, 1f, scale);
        }

        var heroWidth = width * 1.3f;
        var heroY = rect.Min.Y + rect.Height * 0.74f;
        HoldemArt.DrawCard(drawList, new Vector2(rect.Center.X - heroWidth * 0.45f, heroY), heroWidth, 0, true, 1f,
            scale);
        HoldemArt.DrawCard(drawList, new Vector2(rect.Center.X + heroWidth * 0.45f, heroY), heroWidth, 13, true, 1f,
            scale);
    }

    private static int IdleCard(int index) => index switch
    {
        0 => 26,
        1 => 39,
        2 => 12,
        3 => 5,
        _ => 44,
    };
}

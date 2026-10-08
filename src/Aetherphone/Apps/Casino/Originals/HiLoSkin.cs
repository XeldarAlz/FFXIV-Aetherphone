using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class HiLoSkin : IOriginalsSkin
{
    private const float MaxCardWidth = 120f;
    private const float CardShare = 0.34f;
    private const float RailCardWidth = 30f;
    private const float RailOverlap = 0.55f;
    private const float RailHeightShare = 0.22f;
    private const float CallGap = 10f;
    private const int RailShown = 12;
    private const int CardTag = 1;

    private readonly HiLoChain chain = new();
    private readonly CardFlight flight = new(4);

    private OriginalsLabel cashedLabel;
    private int flownCount;
    private int callStep = -1;
    private bool noticePending;
    private LocString notice;
    private Vector2 cardCenter;
    private float cardWidth;
    private float idleTime;

    public string GameId => CasinoGames.HiLo;

    public LocString Title => L.Originals.GameHiLo;

    public CasinoSign Sign => CasinoSign.HiLo;

    public LocString Action => L.Originals.DealFor;

    public LocString Hint => chain.Live ? L.Originals.HiLoHint : L.Originals.HiLoIdleHint;

    public bool Knob => false;

    public bool AutoAvailable => false;

    public bool Live => chain.Live;

    public bool Busy => chain.Animating || flight.Busy || callStep >= 0;

    public bool CanCashOut => chain.CanCashOut && callStep < 0;

    public long CashOutValue => chain.CashOutValue;

    public LocString LiveSecondary => L.Originals.Skip;

    public Vector2 Focus => cardCenter;

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public void Enter()
    {
        noticePending = false;
        callStep = -1;
    }

    public void Reset()
    {
        chain.Clear();
        flight.Clear();
        flownCount = 0;
        callStep = -1;
        noticePending = false;
    }

    public void Snap()
    {
        chain.Snap();
        flight.Clear();
        flownCount = chain.Count;
    }

    public void Resume(CasinoOriginalsOpenDto open)
    {
        var round = open.HiLo;
        if (round is null || chain.Live || !chain.Resume(round))
        {
            return;
        }

        flight.Clear();
        flownCount = chain.Count;
        Raise(L.Originals.Resumed);
    }

    public void Consume(CasinoOriginalsStore originals, bool instant)
    {
        var round = originals.TakeHiLo();
        if (round is not null)
        {
            callStep = -1;
            if (!round.Granted)
            {
                Raise(OriginalsControls.ReasonNotice(round.Reason));
                if (string.Equals(round.Reason, CasinoReasons.InvalidMove, StringComparison.Ordinal))
                {
                    originals.LoadOpen();
                }
            }
            else if (!chain.Apply(round, instant))
            {
                Raise(L.Casino.ReasonGeneric);
            }
            else if (instant)
            {
                flight.Clear();
                flownCount = chain.Count;
            }
        }

        if (!originals.TakeFailure(OriginalsGame.HiLo))
        {
            return;
        }

        callStep = -1;
        Raise(L.Casino.ReasonUnreachable);
    }

    public void Advance(float deltaSeconds)
    {
        chain.Advance(deltaSeconds);
        flight.Advance(deltaSeconds);
        while (flight.TryTakeLanded(out _))
        {
            CasinoSfx.Play(UiSound.CardSnap);
        }

        if (chain.TakeBust())
        {
            CasinoSfx.Play(UiSound.Bust);
        }

        if (chain.TakeLanded() && chain.Live && chain.Count > 1
            && chain.StepResult(chain.Count - 2) == HiLoStep.Won)
        {
            CasinoSfx.Pitched(UiSound.TileSafe, chain.Count - 1);
        }
    }

    public int FillLadder(Span<LadderStep> steps, out int focus)
    {
        focus = 0;
        if (!chain.HasRound)
        {
            return 0;
        }

        var moves = chain.Count - 1;
        var first = Math.Max(0, moves - (steps.Length - 1));
        var count = 0;
        for (var move = first; move < moves; move++)
        {
            var card = chain.Card(move + 1);
            var state = chain.StepResult(move) switch
            {
                HiLoStep.Won => LadderState.Past,
                HiLoStep.Lost => LadderState.Lost,
                _ => LadderState.Upcoming,
            };
            steps[count] = new LadderStep(PlayingCards.RankLabel(OriginalsRules.HiLoRank(card) - 1),
                OriginalsText.MultiplierHundredths(chain.ChainHundredths(move + 1)), state);
            count++;
        }

        if (count > 0)
        {
            ref var last = ref steps[count - 1];
            if (last.State == LadderState.Past)
            {
                last.State = chain.Live ? LadderState.Current : LadderState.Hit;
            }
        }

        focus = Math.Max(0, count - 1);
        return count;
    }

    public void DrawWorld(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var world = frame.World;
        var railHeight = MathF.Min(world.Height * RailHeightShare, PlayingCards.HeightFor(RailCardWidth * scale));
        DrawRail(drawList, new Rect(world.Min, new Vector2(world.Max.X, world.Min.Y + railHeight)), scale);
        var stage = new Rect(new Vector2(world.Min.X, world.Min.Y + railHeight + Metrics.Space.Sm * scale), world.Max);
        cardWidth = MathF.Min(MathF.Min(stage.Width * CardShare, MaxCardWidth * scale),
            PlayingCards.WidthFor(stage.Height * 0.78f));
        cardCenter = new Vector2(stage.Center.X, stage.Min.Y + PlayingCards.HeightFor(cardWidth) * 0.5f);
        LaunchNewCards(scale);
        DrawCurrentCard(drawList, scale);
        DrawCalls(drawList, frame, ui, stage, scale);
    }

    public void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable)
    {
    }

    public bool Play(CasinoOriginalsStore originals, long stake, bool auto)
    {
        if (chain.Live)
        {
            return false;
        }

        chain.Clear();
        flight.Clear();
        flownCount = 0;
        originals.StartHiLo(stake);
        CasinoSfx.Play(UiSound.ChipSlide);
        return true;
    }

    public void CashOut(CasinoOriginalsStore originals)
    {
        if (!CanCashOut || originals.InFlight)
        {
            return;
        }

        originals.CashOutHiLo(chain.RoundId);
    }

    public void Secondary(CasinoOriginalsStore originals)
    {
        if (!chain.Live || callStep >= 0 || originals.InFlight || chain.Count >= OriginalsRules.HiLoDeck)
        {
            return;
        }

        callStep = chain.Step;
        originals.SkipHiLo(chain.RoundId, chain.Step);
    }

    public void Step(CasinoOriginalsStore originals, bool auto)
    {
    }

    public bool TakeSettled(out OriginalsOutcome outcome) => chain.TakeSettled(out outcome);

    public bool TakeNotice(out LocString message)
    {
        message = notice;
        if (!noticePending)
        {
            return false;
        }

        noticePending = false;
        return true;
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleTime += deltaSeconds;
        var width = MathF.Min(rect.Width * 0.3f, PlayingCards.WidthFor(rect.Height * 0.8f));
        var flip = idleTime % 3f / 3f;
        var squash = MathF.Abs(MathF.Cos(MathF.PI * Math.Clamp(flip * 2f - 0.5f, 0f, 1f)));
        var face = flip > 0.5f;
        var card = (int)(idleTime / 3f) % OriginalsRules.HiLoDeck;
        OriginalsArt.Card(drawList, rect.Center - new Vector2(width * 0.7f, 0f), width * 0.85f, (card + 17) % 52, true,
            1f, scale);
        OriginalsArt.Card(drawList, rect.Center + new Vector2(width * 0.35f, 0f), width, card, face, squash, scale);
    }

    private void LaunchNewCards(float scale)
    {
        if (chain.Count < flownCount)
        {
            flownCount = 0;
        }

        if (!chain.HasRound || chain.Count == flownCount)
        {
            return;
        }

        var from = new CardPose(cardCenter + new Vector2(cardWidth * 1.6f, -cardWidth * 0.2f), cardWidth * 0.9f, 0.18f,
            false);
        var to = new CardPose(cardCenter, cardWidth, 0f, true);
        flight.Clear();
        flight.Launch(chain.Current, from, to, flip: true, tag: CardTag, seconds: HiLoChain.FlipSeconds);
        flownCount = chain.Count;
    }

    private void DrawCurrentCard(ImDrawListPtr drawList, float scale)
    {
        if (!chain.HasRound)
        {
            OriginalsArt.Card(drawList, cardCenter, cardWidth, -1, false, 1f, scale);
            return;
        }

        if (flight.Busy)
        {
            if (chain.Count > 1)
            {
                OriginalsArt.Card(drawList, cardCenter, cardWidth, chain.Card(chain.Count - 2), true, 1f, scale);
            }

            for (var index = 0; index < flight.Count; index++)
            {
                if (!flight.Visible(index))
                {
                    continue;
                }

                var pose = flight.Pose(index);
                OriginalsArt.Card(drawList, pose.Center, pose.Width, flight.Card(index), pose.FaceUp, pose.Squash,
                    scale);
            }

            return;
        }

        OriginalsArt.Card(drawList, cardCenter, cardWidth, chain.Current, true, 1f, scale);
        if (chain.Phase != OriginalsRules.PhaseBusted)
        {
            return;
        }

        var half = new Vector2(cardWidth * 0.5f, PlayingCards.HeightFor(cardWidth) * 0.5f);
        var rounding = PlayingCards.RoundingFor(cardWidth);
        var pulse = 0.18f + 0.14f * Pulse.Wave(Pulse.Breath);
        Squircle.Fill(drawList, cardCenter - half, cardCenter + half, rounding,
            ImGui.GetColorU32(OriginalsArt.Boom with { W = pulse }));
    }

    private void DrawRail(ImDrawListPtr drawList, Rect row, float scale)
    {
        if (!chain.HasRound || chain.Count < 2)
        {
            return;
        }

        var width = PlayingCards.WidthFor(row.Height);
        var step = width * (1f - RailOverlap);
        var past = chain.Count - 1;
        var first = Math.Max(0, past - RailShown);
        var shown = past - first;
        var total = width + step * (shown - 1);
        var left = row.Center.X - total * 0.5f + width * 0.5f;
        for (var index = 0; index < shown; index++)
        {
            var cardIndex = first + index;
            var center = new Vector2(left + index * step, row.Center.Y);
            OriginalsArt.Card(drawList, center, width, chain.Card(cardIndex), true, 1f, scale, false);
            var result = chain.StepResult(cardIndex);
            var dot = result switch
            {
                HiLoStep.Won => CasinoColors.Money,
                HiLoStep.Lost => OriginalsArt.Boom,
                _ => CasinoColors.InkMuted,
            };
            drawList.AddCircleFilled(new Vector2(center.X, row.Max.Y + 3f * scale), 2.5f * scale,
                ImGui.GetColorU32(dot), 10);
        }
    }

    private void DrawCalls(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui, Rect stage, float scale)
    {
        var cardHeight = PlayingCards.HeightFor(cardWidth);
        var gap = CallGap * scale;
        var columnWidth = MathF.Max(1f, (stage.Width - cardWidth) * 0.5f - gap);
        var buttonHeight = Button.LargeHeight * scale;
        var top = cardCenter.Y - buttonHeight * 0.5f - Typography.LineHeight(TextStyles.Caption1) * 0.5f;
        var leftRect = new Rect(new Vector2(stage.Min.X, top), new Vector2(stage.Min.X + columnWidth, top + buttonHeight));
        var rightRect = new Rect(new Vector2(stage.Max.X - columnWidth, top), new Vector2(stage.Max.X, top + buttonHeight));
        if (!chain.Live)
        {
            DrawOutcome(drawList, new Vector2(cardCenter.X, cardCenter.Y + cardHeight * 0.5f + gap), ui, stage.Width);
            return;
        }

        var enabled = frame.Interactive && callStep < 0 && !frame.Originals.InFlight && !Busy;
        var left = PickOption(HiLoCall.Lower, HiLoCall.Below, HiLoCall.Same);
        var right = PickOption(HiLoCall.Higher, HiLoCall.Above, HiLoCall.Same);
        if (left == right)
        {
            left = -1;
        }

        DrawCall(drawList, frame.Originals, leftRect, left, ui, enabled, scale);
        DrawCall(drawList, frame.Originals, rightRect, right, ui, enabled, scale);
        var same = Find(HiLoCall.Same);
        if (same < 0 || same == left || same == right)
        {
            return;
        }

        var sameWidth = Button.WidthFor(Loc.T(L.Originals.Same), ButtonSize.Small) + gap * 2f;
        var sameTop = cardCenter.Y + cardHeight * 0.5f + gap;
        var sameRect = new Rect(new Vector2(cardCenter.X - sameWidth * 0.5f, sameTop),
            new Vector2(cardCenter.X + sameWidth * 0.5f, sameTop + Button.SmallHeight * scale));
        DrawCall(drawList, frame.Originals, sameRect, same, ui, enabled, scale);
    }

    private void DrawCall(ImDrawListPtr drawList, CasinoOriginalsStore originals, Rect rect, int optionIndex,
        AppSkin ui, bool enabled, float scale)
    {
        if (optionIndex < 0)
        {
            return;
        }

        var option = chain.Option(optionIndex);
        var label = Loc.T(LabelFor(option.Call));
        if (Button.Draw(drawList, rect, Typography.FitText(label, rect.Width - rect.Height * 0.5f,
                    Button.LabelStyle(rect.Height)), ui.Ink, ButtonStyle.Tinted, enabled: enabled,
                id: CallId(option.Call)))
        {
            callStep = chain.Step;
            originals.GuessHiLo(chain.RoundId, chain.Step, option.Call);
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var chance = OriginalsText.Percent(option.ChanceBasisPoints);
        var multiplier = OriginalsText.Multiplier(option.MultiplierTenThousandths);
        var lineTop = rect.Max.Y + 4f * scale;
        var chanceSize = Typography.Measure(chance, TextStyles.Caption1);
        var multiplierSize = Typography.Measure(multiplier, TextStyles.Caption1);
        var gap = Metrics.Space.Sm * scale;
        var left = rect.Center.X - (chanceSize.X + gap + multiplierSize.X) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, lineTop), chance, ui.MutedInk, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(left + chanceSize.X + gap, lineTop), multiplier, CasinoColors.Money,
            TextStyles.Caption1);
    }

    private void DrawOutcome(ImDrawListPtr drawList, Vector2 topCenter, AppSkin ui, float width)
    {
        if (!chain.HasRound)
        {
            return;
        }

        if (chain.Phase == OriginalsRules.PhaseBusted)
        {
            Typography.DrawWrappedCentered(drawList, Loc.T(L.Originals.HiLoBusted), TextStyles.Subheadline,
                CasinoColors.Loss, topCenter, width);
            return;
        }

        if (chain.Phase != OriginalsRules.PhaseCashedOut)
        {
            return;
        }

        var text = cashedLabel.Get(L.Originals.CashedAt, OriginalsText.Multiplier(chain.MultiplierTenThousandths));
        Typography.DrawWrappedCentered(drawList, text, TextStyles.Subheadline, CasinoColors.Money, topCenter, width);
    }

    private int PickOption(HiLoCall first, HiLoCall second, HiLoCall third)
    {
        var found = Find(first);
        if (found >= 0)
        {
            return found;
        }

        found = Find(second);
        return found >= 0 ? found : Find(third);
    }

    private int Find(HiLoCall call)
    {
        for (var index = 0; index < chain.OptionCount; index++)
        {
            if (chain.Option(index).Call == call)
            {
                return index;
            }
        }

        return -1;
    }

    private static LocString LabelFor(HiLoCall call) => call switch
    {
        HiLoCall.Higher => L.Originals.Higher,
        HiLoCall.Lower => L.Originals.Lower,
        HiLoCall.Above => L.Originals.Above,
        HiLoCall.Below => L.Originals.Below,
        _ => L.Originals.Same,
    };

    private static string CallId(HiLoCall call) => call switch
    {
        HiLoCall.Higher => "casino.originals.hilo.higher",
        HiLoCall.Lower => "casino.originals.hilo.lower",
        HiLoCall.Above => "casino.originals.hilo.above",
        HiLoCall.Below => "casino.originals.hilo.below",
        _ => "casino.originals.hilo.same",
    };

    private void Raise(LocString message)
    {
        notice = message;
        noticePending = true;
    }
}

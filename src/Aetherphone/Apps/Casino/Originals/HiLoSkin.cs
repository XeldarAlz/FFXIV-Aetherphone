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
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class HiLoSkin : IOriginalsSkin
{
    private const float MaxCardWidth = 120f;
    private const float CardShare = 0.34f;
    private const float CallPad = 12f;
    private const float CallIcon = 14f;
    private const float CallIconGap = 6f;
    private const float SubAlpha = 0.8f;
    private const int CardTag = 1;

    private static readonly TextStyle CallStyle = TextStyles.Headline;

    private readonly HiLoChain chain = new();
    private readonly CardFlight flight = new(4);
    private readonly int[] callOrder = new int[HiLoCallLayout.MaxCalls];

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

    public LocString Strip => L.Originals.StripChain;

    public bool Knob => false;

    public bool AutoAvailable => false;

    public bool Live => chain.Live;

    public bool Busy => chain.Animating || flight.Busy || callStep >= 0;

    public bool CanCashOut => chain.CanCashOut && callStep < 0;

    public long CashOutValue => chain.CashOutValue;

    public long LiveStake => chain.Stake;

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
        var stage = frame.World;
        var callsHeight = CallsHeight(scale);
        var cardRoom = MathF.Max(0f, stage.Height - callsHeight - HiLoCallLayout.Gap * scale);
        cardWidth = MathF.Min(MathF.Min(stage.Width * CardShare, MaxCardWidth * scale), PlayingCards.WidthFor(cardRoom));
        var cardHeight = PlayingCards.HeightFor(cardWidth);
        var top = HiLoCallLayout.GroupTop(stage, HiLoCallLayout.GroupHeight(cardHeight, callsHeight, scale));
        cardCenter = new Vector2(stage.Center.X, top + cardHeight * 0.5f);
        LaunchNewCards(scale);
        DrawCurrentCard(drawList, scale);
        var callsTop = top + cardHeight + HiLoCallLayout.Gap * scale;
        DrawCalls(drawList, frame, ui,
            new Rect(new Vector2(stage.Min.X, callsTop), new Vector2(stage.Max.X, callsTop + callsHeight)), scale);
    }

    public void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable)
    {
    }

    public void Gate()
    {
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
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

    private static float CallsHeight(float scale) =>
        HiLoCallLayout.CallHeight * scale + Metrics.Space.Xs * scale
        + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 2f;

    private void DrawCalls(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui, Rect row, float scale)
    {
        if (!chain.Live)
        {
            DrawOutcome(drawList, new Vector2(row.Center.X, row.Min.Y), ui, row.Width);
            return;
        }

        var left = PickOption(HiLoCall.Lower, HiLoCall.Below, HiLoCall.Same);
        var right = PickOption(HiLoCall.Higher, HiLoCall.Above, HiLoCall.Same);
        if (left == right)
        {
            left = -1;
        }

        var same = Find(HiLoCall.Same);
        var count = 0;
        if (left >= 0)
        {
            callOrder[count] = left;
            count++;
        }

        if (same >= 0 && same != left && same != right)
        {
            callOrder[count] = same;
            count++;
        }

        if (right >= 0)
        {
            callOrder[count] = right;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        Span<float> naturals = stackalloc float[HiLoCallLayout.MaxCalls];
        Span<float> widths = stackalloc float[HiLoCallLayout.MaxCalls];
        for (var index = 0; index < count; index++)
        {
            naturals[index] = CallNatural(chain.Option(callOrder[index]), scale);
        }

        HiLoCallLayout.Distribute(naturals[..count], row.Width, scale, widths[..count]);
        var enabled = frame.Interactive && callStep < 0 && !frame.Originals.InFlight && !Busy;
        for (var index = 0; index < count; index++)
        {
            DrawCall(drawList, frame.Originals, HiLoCallLayout.Call(row, widths[..count], index, scale),
                callOrder[index], ui, enabled, scale);
        }
    }

    private static float CallNatural(in HiLoOption option, float scale)
    {
        var icon = HasIcon(option.Call) ? (CallIcon + CallIconGap) * scale : 0f;
        var line = icon + Typography.Measure(Loc.T(LabelFor(option.Call)), CallStyle).X;
        var sub = Inclusive(option.Call) ? Typography.Measure(Loc.T(L.Originals.OrSame), TextStyles.Footnote).X : 0f;
        var chance = Typography.Measure(OriginalsText.Percent(option.ChanceBasisPoints), TextStyles.FootnoteEmphasized).X;
        var multiplier = Typography.Measure(OriginalsText.Multiplier(option.MultiplierTenThousandths),
            TextStyles.FootnoteEmphasized).X;
        return MathF.Max(MathF.Max(line, sub), MathF.Max(chance, multiplier)) + CallPad * 2f * scale;
    }

    private void DrawCall(ImDrawListPtr drawList, CasinoOriginalsStore originals, Rect rect, int optionIndex,
        AppSkin ui, bool enabled, float scale)
    {
        var option = chain.Option(optionIndex);
        var call = option.Call;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink, ButtonStyle.Tinted, ButtonRole.Normal, enabled, hovered,
            ImGui.GetID(CallId(call)));
        var room = MathF.Max(1f, face.Face.Width - CallPad * 2f * scale);
        var hasIcon = HasIcon(call);
        var iconSize = CallIcon * scale;
        var iconWidth = hasIcon ? iconSize + CallIconGap * scale : 0f;
        var label = Typography.FitText(Loc.T(LabelFor(call)), MathF.Max(1f, room - iconWidth), CallStyle);
        var labelSize = Typography.Measure(label, CallStyle);
        var sub = Inclusive(call) ? Typography.FitText(Loc.T(L.Originals.OrSame), room, TextStyles.Footnote) : string.Empty;
        var subSize = sub.Length > 0 ? Typography.Measure(sub, TextStyles.Footnote) : Vector2.Zero;
        var top = face.Face.Center.Y - (labelSize.Y + subSize.Y) * 0.5f;
        var left = face.Face.Center.X - (iconWidth + labelSize.X) * 0.5f;
        if (hasIcon)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, top + labelSize.Y * 0.5f),
                IconFor(call), face.LabelInk, iconSize);
        }

        Typography.Draw(drawList, new Vector2(left + iconWidth, top), label, face.LabelInk, CallStyle);
        if (sub.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(face.Face.Center.X - subSize.X * 0.5f, top + labelSize.Y), sub,
                face.LabelInk with { W = face.LabelInk.W * SubAlpha }, TextStyles.Footnote);
        }

        if (enabled && UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            callStep = chain.Step;
            originals.GuessHiLo(chain.RoundId, chain.Step, call);
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var lineTop = rect.Max.Y + Metrics.Space.Xs * scale;
        var lineHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        DrawCentered(drawList, OriginalsText.Percent(option.ChanceBasisPoints), rect.Center.X, lineTop, rect.Width,
            CasinoColors.InkTitle);
        DrawCentered(drawList, OriginalsText.Multiplier(option.MultiplierTenThousandths), rect.Center.X,
            lineTop + lineHeight, rect.Width, CasinoColors.Money);
    }

    private static void DrawCentered(ImDrawListPtr drawList, string text, float centerX, float top, float width,
        Vector4 ink)
    {
        var shown = Typography.FitText(text, width, TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(shown, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), shown, ink, TextStyles.FootnoteEmphasized);
    }

    private static bool Inclusive(HiLoCall call) => call is HiLoCall.Higher or HiLoCall.Lower;

    private static bool HasIcon(HiLoCall call) => call != HiLoCall.Same;

    private static FontAwesomeIcon IconFor(HiLoCall call) =>
        call is HiLoCall.Higher or HiLoCall.Above ? FontAwesomeIcon.ArrowUp : FontAwesomeIcon.ArrowDown;

    private void DrawOutcome(ImDrawListPtr drawList, Vector2 topCenter, AppSkin ui, float width)
    {
        if (!chain.HasRound)
        {
            return;
        }

        if (chain.Phase == OriginalsRules.PhaseBusted)
        {
            Typography.DrawWrappedCentered(drawList, Loc.T(L.Originals.HiLoBusted), TextStyles.Subheadline,
                CasinoColors.InkBody, topCenter, width);
            return;
        }

        if (chain.Phase != OriginalsRules.PhaseCashedOut)
        {
            return;
        }

        var text = cashedLabel.Get(L.Originals.CashedAt, OriginalsText.Multiplier(chain.MultiplierTenThousandths));
        Typography.DrawWrappedCentered(drawList, text, TextStyles.Title3, CasinoColors.Money, topCenter, width);
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
        HiLoCall.Higher or HiLoCall.Above => L.Originals.Above,
        HiLoCall.Lower or HiLoCall.Below => L.Originals.Below,
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

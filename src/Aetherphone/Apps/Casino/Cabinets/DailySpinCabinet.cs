using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class DailySpinCabinet
{
    private const float MaxRingRadius = 104f;
    private const float PillHeight = 48f;
    private const int SpinTurns = 5;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private readonly CasinoSpinStore spin;
    private readonly ParticleSystem particles = new(192);

    private RollingValue coinRoll;
    private string inlineReason = string.Empty;
    private string spunRoundId = string.Empty;
    private float angle;
    private float spinFromAngle;
    private float sweep;
    private float spinElapsedSeconds = WheelChoreography.SpinSeconds;
    private int landedSegment = -1;
    private int peg;
    private long landedAmount;
    private bool spinning;
    private bool celebrated;
    private Vector2 ringCenter;

    public DailySpinCabinet(CasinoSpinStore spin)
    {
        this.spin = spin;
    }

    public void Enter()
    {
        inlineReason = string.Empty;
    }

    public void Reset()
    {
        particles.Clear();
        inlineReason = string.Empty;
        spunRoundId = string.Empty;
        angle = 0f;
        spinFromAngle = 0f;
        sweep = 0f;
        spinElapsedSeconds = WheelChoreography.SpinSeconds;
        landedSegment = -1;
        landedAmount = 0;
        spinning = false;
        celebrated = false;
        coinRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        ConsumeClaimResult();
        AdoptKnownSpin();
        Advance(delta, scale);
        particles.Update(delta);

        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Md * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var answer = spin.Answer;
        var claim = DailySpinStatus.Of(answer);

        var y = body.Min.Y + Metrics.Space.Sm * scale;
        var intro = Loc.T(L.Casino.SpinIntro);
        var introBlock = Typography.MeasureWrappedBlock(intro, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(new Vector2(left, y), intro, ui.MutedInk, TextStyles.Footnote, width);
        y += introBlock.Y + Metrics.Space.Md * scale;

        y = DrawRing(drawList, left, y, width, scale);
        y = DrawBanner(drawList, ui, claim, left, y, width, scale, delta);
        DrawAction(drawList, ui, answer, claim, left, y, width, scale);
        particles.Draw(drawList, scale);
    }

    private void ConsumeClaimResult()
    {
        if (spin.TakeClaimFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var result = spin.TakeClaimResult();
        if (result is null)
        {
            return;
        }

        if (!result.Granted)
        {
            inlineReason = result.Reason.Length > 0 ? result.Reason : CasinoReasons.AlreadyClaimed;
            spunRoundId = result.RoundId;
            landedSegment = result.Segment;
            landedAmount = result.Amount;
            celebrated = true;
            spinning = false;
            if (DailySpinRules.IsSegment(result.Segment))
            {
                angle = WheelChoreography.RestAngleOf(result.Segment, DailySpinRules.SegmentCount);
                coinRoll.Snap((int)Math.Min(landedAmount, int.MaxValue));
            }

            return;
        }

        inlineReason = string.Empty;
        spunRoundId = result.RoundId;
        landedSegment = result.Segment;
        landedAmount = result.Amount;
        celebrated = false;
        BeginSpin(result.Segment);
    }

    private void AdoptKnownSpin()
    {
        var answer = spin.Answer;
        if (spinning
            || answer is null
            || answer.RoundId.Length == 0
            || string.Equals(answer.RoundId, spunRoundId, StringComparison.Ordinal)
            || DailySpinStatus.Of(answer) != DailySpinClaim.Claimed)
        {
            return;
        }

        spunRoundId = answer.RoundId;
        landedSegment = answer.Segment;
        landedAmount = answer.Amount;
        celebrated = true;
        if (!DailySpinRules.IsSegment(answer.Segment))
        {
            return;
        }

        angle = WheelChoreography.RestAngleOf(answer.Segment, DailySpinRules.SegmentCount);
        coinRoll.Snap((int)Math.Min(landedAmount, int.MaxValue));
    }

    private void BeginSpin(int segment)
    {
        if (!DailySpinRules.IsSegment(segment))
        {
            spinning = false;
            return;
        }

        spinFromAngle = angle;
        sweep = WheelChoreography.SweepFor(spinFromAngle, segment, DailySpinRules.SegmentCount, SpinTurns);
        spinElapsedSeconds = 0f;
        spinning = true;
        peg = WheelChoreography.PegOf(angle, DailySpinRules.SegmentCount);
        coinRoll.Snap(0);
    }

    private void Advance(float deltaSeconds, float scale)
    {
        if (!spinning)
        {
            return;
        }

        spinElapsedSeconds += deltaSeconds;
        if (spinElapsedSeconds >= WheelChoreography.SpinSeconds)
        {
            spinElapsedSeconds = WheelChoreography.SpinSeconds;
            spinning = false;
            Celebrate(scale);
        }

        angle = WheelChoreography.AngleAt(spinFromAngle, sweep, spinElapsedSeconds);
        var nextPeg = WheelChoreography.PegOf(angle, DailySpinRules.SegmentCount);
        if (nextPeg != peg)
        {
            peg = nextPeg;
            UiFeedback.Play(UiSound.GameTick);
        }
    }

    private void Celebrate(float scale)
    {
        if (celebrated || landedAmount <= 0)
        {
            return;
        }

        celebrated = true;
        if (DailySpinRules.IsTopAward(landedSegment))
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            particles.Confetti(ringCenter, 110, ConfettiPalette, 340f * scale, 5f, 1.7f);
            particles.Sparkle(ringCenter, 28, Gold, 200f * scale, 4f, 1.1f);
            return;
        }

        UiFeedback.Play(UiSound.GameWin);
        particles.Confetti(ringCenter, 48, ConfettiPalette, 250f * scale, 4f, 1.2f);
    }

    private float DrawRing(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var radius = MathF.Min(width * 0.40f, MaxRingRadius * scale);
        var center = new Vector2(left + width * 0.5f, y + radius + 14f * scale);
        ringCenter = center;
        var glow = !spinning && DailySpinRules.IsSegment(landedSegment)
            ? 0.55f + 0.45f * Pulse.Wave(Pulse.Breath)
            : 0f;
        var highlight = spinning ? -1 : landedSegment;
        SpinRingArt.Draw(drawList, center, radius, angle, highlight, glow, scale);
        WheelRingArt.DrawPointer(drawList, center, radius, scale);
        return center.Y + radius + 22f * scale;
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, DailySpinClaim claim, float left, float y,
        float width, float scale, float delta)
    {
        var center = new Vector2(left + width * 0.5f, y + 16f * scale);
        if (spinning)
        {
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SpinTurning), Gold,
                TextStyles.SubheadlineEmphasized);
            return y + 40f * scale;
        }

        if (claim != DailySpinClaim.Claimed)
        {
            Typography.DrawCentered(drawList, center,
                Loc.T(L.Casino.SpinTopNote, GameNumber.Label((int)DailySpinRules.TopAward)), ui.MutedInk,
                TextStyles.Footnote);
            return y + 40f * scale;
        }

        if (landedAmount > 0)
        {
            coinRoll.Update((int)Math.Min(landedAmount, int.MaxValue), delta);
            var amount = NumberText.Group((long)coinRoll.Display);
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SpinWonBanner, amount), Gold,
                TextStyles.Title3.Scale * coinRoll.PopScale, TextStyles.Title3.Weight);
            return y + 44f * scale;
        }

        Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SpinClaimedTitle), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        return y + 40f * scale;
    }

    private void DrawAction(ImDrawListPtr drawList, AppSkin ui,
        Core.Aethernet.Contracts.CasinoDailySpinDto? answer, DailySpinClaim claim, float left, float y,
        float width, float scale)
    {
        if (inlineReason.Length > 0)
        {
            y = DrawReasonCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), left, y, width, scale)
                + Metrics.Space.Sm * scale;
        }

        if (spinning)
        {
            return;
        }

        if (claim != DailySpinClaim.Claimed)
        {
            var enabled = DailySpinStatus.CanClaim(answer, spin.Busy);
            var pillRect = new Rect(new Vector2(left + width * 0.18f, y),
                new Vector2(left + width * 0.82f, y + PillHeight * scale));
            if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.SpinAction), true, enabled, ui.Theme))
            {
                inlineReason = string.Empty;
                spin.Claim();
            }

            return;
        }

        var reset = answer is not null && answer.NextSpinAtUnix > 0
            ? Loc.T(L.Casino.SpinNextAt, TimeText.FutureMoment(answer.NextSpinAtUnix))
            : Loc.T(L.Casino.SpinNextSoon);
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 10f * scale), reset, ui.MutedInk,
            TextStyles.Footnote);
    }

    private static float DrawReasonCard(ImDrawListPtr drawList, AppSkin ui, string message, float left, float y,
        float width, float scale)
    {
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, width - pad * 2f);
        return max.Y;
    }
}

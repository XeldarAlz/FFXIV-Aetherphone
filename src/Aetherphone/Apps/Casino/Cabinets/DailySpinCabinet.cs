using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class DailySpinCabinet : ICabinetIdle
{
    private const float MaxRingRadius = 150f;
    private const float SignShare = 0.13f;
    private const float SignMin = 34f;
    private const float SignMax = 56f;
    private const float SignFill = 0.78f;
    private const float FooterHeight = 112f;
    private const float WheelPad = 10f;
    private const float SpotSpread = 0.2f;
    private const float SpotSweep = 0.22f;
    private const float SpotSweepRate = 0.5f;
    private const float SpotReach = 1.25f;
    private const float RestSpot = 0.16f;
    private const float SpinSpot = 0.26f;
    private const float TopSpot = 0.42f;
    private const float ButtonShare = 0.64f;
    private const float FlickerRate = 7f;
    private const int FlickerEvery = 23;

    private readonly CasinoSpinStore spin;
    private readonly DailySpinPlayback playback = new();
    private readonly DailySpinIdle idle = new();
    private readonly SpinFlourish flourish = new();

    private RollingAmount coinRoll;
    private LabelSlot wonLabel;
    private LabelSlot topLabel;
    private string nextText = string.Empty;
    private long nextAtUnix = -1;
    private int nextDay = -1;
    private LanguageInfo? nextLanguage;
    private string inlineReason = string.Empty;
    private string spunRoundId = string.Empty;
    private Vector2 ringCenter;

    public DailySpinCabinet(CasinoSpinStore spin)
    {
        this.spin = spin;
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public void Enter()
    {
        inlineReason = string.Empty;
    }

    public void Reset()
    {
        playback.Reset();
        flourish.Clear();
        inlineReason = string.Empty;
        spunRoundId = string.Empty;
        coinRoll.Snap(0);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = frame.DeltaSeconds;
        ConsumeClaimResult();
        AdoptKnownSpin();
        if (frame.SnapToTruth)
        {
            playback.Snap();
        }

        playback.Idle(delta);
        playback.Update(delta);
        if (playback.TakeTicks() > 0)
        {
            CasinoSfx.Play(UiSound.WheelTick);
        }

        if (playback.TakeLanded())
        {
            Celebrate(stage);
        }

        flourish.Update(stage, delta, frame.Layout, frame.SnapToTruth);
        var drawList = ImGui.GetWindowDrawList();
        var safe = frame.Safe;
        var answer = spin.Answer;
        var claim = DailySpinStatus.Of(answer);
        var signHeight = Math.Clamp(safe.Height * SignShare, SignMin * scale, SignMax * scale);
        var footerTop = safe.Max.Y - FooterHeight * scale;
        var intro = Loc.T(L.Casino.SpinIntro);
        var introTop = safe.Min.Y + signHeight;
        var introHeight = Typography.DrawWrappedLeft(new Vector2(safe.Min.X, introTop), intro, CasinoColors.InkBody,
            TextStyles.Caption1, safe.Width);
        var wheelTop = introTop + introHeight;
        var wheelArea = new Rect(new Vector2(safe.Min.X, wheelTop),
            new Vector2(safe.Max.X, MathF.Max(wheelTop, footerTop)));
        DrawSpotlights(drawList, frame.Full, wheelArea.Center, frame.Phase);
        DrawSign(drawList, safe, signHeight, frame.Phase);
        DrawWheel(drawList, wheelArea, frame.Phase, scale);
        DrawFooter(drawList, ui, stage, frame, answer, claim,
            new Rect(new Vector2(safe.Min.X, footerTop), safe.Max), delta, scale);
        flourish.Draw(drawList, frame.Layout, frame.Phase, scale);
        flourish.HandleSkip(frame.Layout);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        idle.Draw(drawList, rect, deltaSeconds, UiScale.Current);
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

        spunRoundId = result.RoundId;
        if (!result.Granted)
        {
            inlineReason = result.Reason.Length > 0 ? result.Reason : CasinoReasons.AlreadyClaimed;
            playback.Adopt(result.Segment, result.Amount);
            coinRoll.Snap(result.Amount);
            return;
        }

        inlineReason = string.Empty;
        flourish.Clear();
        coinRoll.Snap(0);
        playback.Begin(result.Segment, result.Amount);
    }

    private void AdoptKnownSpin()
    {
        var answer = spin.Answer;
        if (playback.Spinning
            || answer is null
            || answer.RoundId.Length == 0
            || string.Equals(answer.RoundId, spunRoundId, StringComparison.Ordinal)
            || DailySpinStatus.Of(answer) != DailySpinClaim.Claimed)
        {
            return;
        }

        spunRoundId = answer.RoundId;
        playback.Adopt(answer.Segment, answer.Amount);
        coinRoll.Snap(answer.Amount);
    }

    private void Celebrate(CasinoStage stage)
    {
        if (playback.Amount <= 0)
        {
            return;
        }

        var tier = DailySpinRules.IsTopAward(playback.Segment) ? WinTier.Epic : WinTier.Win;
        flourish.Start(stage, tier, playback.Amount, ringCenter);
    }

    private static void DrawSpotlights(ImDrawListPtr drawList, Rect full, Vector2 target, float phase)
    {
        var sway = MathF.Sin(phase * SpotSweepRate) * SpotSweep;
        var left = new Vector2(full.Min.X, full.Min.Y);
        var right = new Vector2(full.Max.X, full.Min.Y);
        var leftAim = target - left;
        var rightAim = target - right;
        CasinoLights.Spotlight(drawList, left, MathF.Atan2(leftAim.Y, leftAim.X) + sway, leftAim.Length() * SpotReach,
            SpotSpread, CasinoColors.LightB, RestSpot);
        CasinoLights.Spotlight(drawList, right, MathF.Atan2(rightAim.Y, rightAim.X) - sway,
            rightAim.Length() * SpotReach, SpotSpread, CasinoColors.LightA, RestSpot);
    }

    private static void DrawSign(ImDrawListPtr drawList, Rect safe, float bandHeight, float phase)
    {
        var height = CasinoSigns.HeightToFit(CasinoSign.FreeSpin, safe.Width * 0.86f, bandHeight * SignFill);
        var step = (int)(phase * FlickerRate);
        var lit = step % FlickerEvery == 0 ? 0.35f : 0.88f + 0.12f * Pulse.Wave(Pulse.Fast);
        CasinoSigns.Draw(drawList, CasinoSign.FreeSpin,
            new Vector2(safe.Center.X, safe.Min.Y + bandHeight * 0.5f), height, CasinoColors.LightA, lit);
    }

    private void DrawWheel(ImDrawListPtr drawList, Rect area, float phase, float scale)
    {
        var head = SpinRingArt.RimInset * scale;
        var radius = MathF.Min(MathF.Min(area.Width * 0.5f, (area.Height - head) * 0.5f) - WheelPad * scale,
            MaxRingRadius * scale);
        radius = MathF.Max(radius, 1f);
        ringCenter = new Vector2(area.Center.X, area.Center.Y + head * 0.5f);
        var rested = playback.Rested;
        var top = rested && DailySpinRules.IsTopAward(playback.Segment);
        var glow = rested ? 0.55f + 0.45f * Pulse.Wave(Pulse.Breath) : 0f;
        var spot = playback.Spinning ? SpinSpot : top ? TopSpot : 0f;
        if (spot > 0f)
        {
            CasinoLights.Spotlight(drawList, new Vector2(ringCenter.X, area.Min.Y - head), MathF.PI * 0.5f,
                radius * 2.4f, SpotSpread * 1.4f, CasinoColors.MoneyHighlight, spot);
        }

        SpinRingArt.DrawRim(drawList, ringCenter, radius, phase * (playback.Spinning || top ? 2.5f : 1f),
            rested || playback.Spinning ? 1f : 0.7f, scale);
        SpinRingArt.Draw(drawList, ringCenter, radius, playback.Angle, rested ? playback.Segment : -1, glow, scale);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, ringCenter, radius * 0.34f);
        SpinRingArt.DrawPointer(drawList, ringCenter, radius, playback.PointerDeflection, scale);
    }

    private void DrawFooter(ImDrawListPtr drawList, AppSkin ui, CasinoStage stage, in CasinoStageFrame frame,
        CasinoDailySpinDto? answer, DailySpinClaim claim, Rect footer, float delta, float scale)
    {
        var y = footer.Min.Y;
        var width = footer.Width;
        if (inlineReason.Length > 0)
        {
            var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, footer.Min.X,
                y - height - Metrics.Space.Sm * scale, width, scale);
        }

        var bannerHeight = Typography.LineHeight(TextStyles.Title3);
        var bannerCenter = new Vector2(footer.Center.X, y + bannerHeight * 0.5f);
        DrawBanner(drawList, claim, bannerCenter, width, delta);
        y += bannerHeight + Metrics.Space.Sm * scale;
        if (playback.Spinning)
        {
            return;
        }

        if (claim != DailySpinClaim.Claimed)
        {
            var buttonWidth = width * ButtonShare;
            var button = new Rect(new Vector2(footer.Center.X - buttonWidth * 0.5f, y),
                new Vector2(footer.Center.X + buttonWidth * 0.5f, y + Button.LargeHeight * scale));
            var enabled = DailySpinStatus.CanClaim(answer, spin.Busy) && !frame.Blocked;
            var pressed = Button.Draw(button, Loc.T(L.Casino.SpinAction), ui.Ink, ButtonStyle.Prominent,
                enabled: enabled);
            if (!pressed && !(enabled && stage.RepeatPressed()))
            {
                return;
            }

            inlineReason = string.Empty;
            spin.Claim();
            CasinoSfx.Play(UiSound.ChipSlide);
            return;
        }

        var reset = Typography.FitText(NextText(answer), width, TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2(footer.Center.X, y + Typography.LineHeight(TextStyles.Footnote)),
            reset, CasinoColors.InkMuted, TextStyles.Footnote);
    }

    private void DrawBanner(ImDrawListPtr drawList, DailySpinClaim claim, Vector2 center, float width, float delta)
    {
        if (playback.Spinning)
        {
            Typography.DrawCentered(drawList, center,
                Typography.FitText(Loc.T(L.Casino.SpinTurning), width, TextStyles.SubheadlineEmphasized),
                CasinoColors.Money, TextStyles.SubheadlineEmphasized);
            return;
        }

        if (claim != DailySpinClaim.Claimed)
        {
            var note = topLabel.Get(L.Casino.SpinTopNote, (int)DailySpinRules.TopAward);
            Typography.DrawCentered(drawList, center, Typography.FitText(note, width, TextStyles.Footnote),
                CasinoColors.InkBody, TextStyles.Footnote);
            return;
        }

        var amount = playback.Amount;
        if (amount <= 0)
        {
            Typography.DrawCentered(drawList, center,
                Typography.FitText(Loc.T(L.Casino.SpinClaimedTitle), width, TextStyles.SubheadlineEmphasized),
                CasinoColors.InkTitle, TextStyles.SubheadlineEmphasized);
            return;
        }

        coinRoll.Update(amount, delta);
        var text = Typography.FitText(wonLabel.Get(L.Casino.SpinWonBanner, (int)Math.Min(coinRoll.Display, int.MaxValue)),
            width, TextStyles.Title3);
        Typography.DrawCentered(drawList, center, text, CasinoColors.Money, TextStyles.Title3.Scale * coinRoll.PopScale,
            TextStyles.Title3.Weight);
    }

    private string NextText(CasinoDailySpinDto? answer)
    {
        var next = answer?.NextSpinAtUnix ?? 0;
        var day = DateTime.Now.DayOfYear;
        if (next == nextAtUnix && day == nextDay && ReferenceEquals(nextLanguage, Loc.Current) && nextText.Length > 0)
        {
            return nextText;
        }

        nextAtUnix = next;
        nextDay = day;
        nextLanguage = Loc.Current;
        nextText = next > 0
            ? Loc.T(L.Casino.SpinNextAt, TimeText.FutureMoment(next))
            : Loc.T(L.Casino.SpinNextSoon);
        return nextText;
    }
}

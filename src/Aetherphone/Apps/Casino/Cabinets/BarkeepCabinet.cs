using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum BarkeepMode
{
    None,
    Wager,
    Practice,
}

internal sealed class BarkeepCabinet
{
    private const string PracticeStatsId = "casino.barkeep";
    private const float PadX = 16f;
    private const float GradeFlashSecondsTotal = 0.8f;
    private const float TapFlashSecondsTotal = 0.35f;
    private const double FinishRetryDelaySeconds = 3.0;
    private const double FinishFailureRetryDelaySeconds = 5.0;
    private const string MalformedReason = "malformed";

    private static readonly Vector4 Gold = BarkeepArt.PerfectGold;

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private readonly CasinoStore store;
    private readonly CasinoPlayStore play;
    private readonly GameStatsStore stats;
    private readonly Action openCashier;
    private readonly ParticleSystem particles = new(192);
    private readonly BarkeepVerbStage verbStage = new();
    private readonly Random random = new();

    private RollingValue scoreRoll;
    private RollingValue payoutRoll;
    private BarkeepShift? shift;
    private BarkeepMode mode;
    private CasinoBarkeepFinishDto? settle;
    private bool settleCelebrated;
    private bool practiceSettled;
    private int practiceScore;
    private bool practiceNewBest;
    private bool startRequested;
    private bool finishSent;
    private double finishRetryAtElapsed;
    private int gradeFlash = -1;
    private float gradeFlashLeft;
    private int tapFlashGrade = -1;
    private float tapFlashLeft;
    private Vector2 tapFlashCenter;
    private string inlineReason = string.Empty;
    private int clockSecond = -1;
    private string clockText = "0:00";

    public BarkeepCabinet(CasinoStore store, CasinoPlayStore play, GameStatsStore stats, Action openCashier)
    {
        this.store = store;
        this.play = play;
        this.stats = stats;
        this.openCashier = openCashier;
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        play.RecoverPendingRound();
    }

    public void Reset()
    {
        if (mode == BarkeepMode.Wager && shift is not null && settle is null)
        {
            verbStage.Cancel();
            gradeFlashLeft = 0f;
            tapFlashLeft = 0f;
            inlineReason = string.Empty;
            particles.Clear();
            return;
        }

        shift = null;
        mode = BarkeepMode.None;
        settle = null;
        settleCelebrated = false;
        practiceSettled = false;
        practiceNewBest = false;
        startRequested = false;
        finishSent = false;
        finishRetryAtElapsed = 0;
        gradeFlash = -1;
        gradeFlashLeft = 0f;
        tapFlashLeft = 0f;
        inlineReason = string.Empty;
        verbStage.Cancel();
        particles.Clear();
        scoreRoll.Snap(0);
        payoutRoll.Snap(0);
    }

    public void Tick()
    {
        ConsumeResults();
        if (mode != BarkeepMode.Wager || shift is null || finishSent || settle is not null)
        {
            return;
        }

        var elapsed = shift.ElapsedSeconds(NowUnixSeconds());
        var wrapReady = shift.ShouldAutoFinish(elapsed) || (shift.AllServed && shift.CanFinish(elapsed));
        if (wrapReady && elapsed >= finishRetryAtElapsed)
        {
            SendFinish();
        }
    }

    private void ConsumeResults()
    {
        var start = play.TakeBarkeepStart();
        if (start is not null)
        {
            startRequested = false;
            if (!start.Granted)
            {
                inlineReason = start.Reason.Length > 0 ? start.Reason : CasinoReasons.Unreachable;
            }
            else
            {
                var fresh = BarkeepShift.FromStart(start);
                if (fresh is null)
                {
                    inlineReason = MalformedReason;
                }
                else if (shift is null || !string.Equals(shift.RoundId, fresh.RoundId, StringComparison.Ordinal))
                {
                    shift = fresh;
                    mode = BarkeepMode.Wager;
                    settle = null;
                    settleCelebrated = false;
                    practiceSettled = false;
                    finishSent = false;
                    finishRetryAtElapsed = 0;
                    gradeFlashLeft = 0f;
                    tapFlashLeft = 0f;
                    inlineReason = string.Empty;
                    verbStage.Cancel();
                    scoreRoll.Snap(fresh.Score);
                }
            }
        }

        var finish = play.TakeBarkeepFinish();
        if (finish is not null)
        {
            if (string.Equals(finish.Reason, CasinoReasons.Cooldown, StringComparison.Ordinal) && shift is not null)
            {
                finishSent = false;
                finishRetryAtElapsed = shift.ElapsedSeconds(NowUnixSeconds()) + FinishRetryDelaySeconds;
            }
            else
            {
                settle = finish;
                finishSent = false;
                settleCelebrated = false;
                payoutRoll.Snap(0);
                verbStage.Cancel();
            }
        }

        var ownsTheInFlightPost = startRequested || finishSent;
        if (!ownsTheInFlightPost || !play.TakeRoundFailure())
        {
            return;
        }

        if (finishSent && shift is not null)
        {
            finishSent = false;
            finishRetryAtElapsed = shift.ElapsedSeconds(NowUnixSeconds()) + FinishFailureRetryDelaySeconds;
            return;
        }

        startRequested = false;
        inlineReason = CasinoReasons.Unreachable;
    }

    private void SendFinish()
    {
        if (shift is null || play.RoundInFlight)
        {
            return;
        }

        finishSent = true;
        play.FinishBarkeep(shift.RoundId, shift.BuildFinishOrders());
    }

    private static double NowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 0.001;
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        particles.Update(delta);

        var drawList = ImGui.GetWindowDrawList();
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;

        if (settle is not null)
        {
            DrawSettle(drawList, ui, body, left, width, scale, delta);
        }
        else if (practiceSettled)
        {
            DrawPracticeSettle(drawList, ui, body, left, width, scale, delta);
        }
        else if (shift is not null && finishSent)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
        }
        else if (shift is not null)
        {
            DrawShift(drawList, ui, body, left, width, scale, delta);
        }
        else if (startRequested || play.RoundInFlight)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
        }
        else
        {
            DrawLobby(drawList, ui, left, body.Min.Y + Metrics.Space.Sm * scale, width, scale);
        }

        particles.Draw(drawList, scale);
    }

    private void DrawShift(ImDrawListPtr drawList, AppSkin ui, Rect body, float left, float width, float scale,
        float delta)
    {
        var current = shift!;
        if (gradeFlashLeft > 0f)
        {
            gradeFlashLeft -= delta;
            if (gradeFlashLeft <= 0f)
            {
                gradeFlash = -1;
            }
        }

        if (mode == BarkeepMode.Practice && current.AllServed && gradeFlashLeft <= 0f)
        {
            EndPractice();
            return;
        }

        var elapsed = current.ElapsedSeconds(NowUnixSeconds());
        var y = body.Min.Y + Metrics.Space.Sm * scale;
        y = DrawShiftHud(drawList, ui, current, elapsed, left, y, width, scale, delta);
        y += Metrics.Space.Sm * scale;

        if (mode == BarkeepMode.Wager && current.InLastCall(elapsed))
        {
            var warning = Loc.T(L.Casino.BarkeepLastCall,
                GameNumber.Label((int)Math.Ceiling(current.SecondsUntilForfeit(elapsed))));
            y = DrawNoticeCard(drawList, ui, warning, Gold, left, y, width, scale);
            y += Metrics.Space.Sm * scale;
        }

        var canvasHeight = Math.Clamp(body.Max.Y - y - 96f * scale, 140f * scale, 230f * scale);
        var canvas = new Rect(new Vector2(left, y), new Vector2(left + width, y + canvasHeight));
        ui.Card(drawList, canvas.Min, canvas.Max, Metrics.Radius.Card * scale);
        DrawOrderCanvas(drawList, ui, current, elapsed, canvas, scale, delta);
        DrawTapFlash(drawList, ui, scale, delta);
        y = canvas.Max.Y + Metrics.Space.Md * scale;

        if (mode == BarkeepMode.Practice)
        {
            var practiceRect = new Rect(new Vector2(left + width * 0.25f, y),
                new Vector2(left + width * 0.75f, y + 40f * scale));
            if (ui.GhostButton(practiceRect, Loc.T(L.Casino.BarkeepEndPractice)))
            {
                EndPractice();
            }

            return;
        }

        var canEnd = current.CanFinish(elapsed);
        var endRect = new Rect(new Vector2(left + width * 0.2f, y), new Vector2(left + width * 0.8f, y + 44f * scale));
        if (AppSkin.PillButton(endRect, Loc.T(L.Casino.BarkeepEndShift), true, canEnd, ui.Theme))
        {
            SendFinish();
        }

        y = endRect.Max.Y + Metrics.Space.Sm * scale;
        if (!canEnd)
        {
            var wait = Loc.T(L.Casino.BarkeepSettlesIn,
                GameNumber.Label((int)Math.Ceiling(BarkeepShift.FinishEarliestSeconds - elapsed)));
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 8f * scale), wait, ui.MutedInk,
                TextStyles.Footnote);
        }
    }

    private float DrawShiftHud(ImDrawListPtr drawList, AppSkin ui, BarkeepShift current, double elapsed, float left,
        float y, float width, float scale, float delta)
    {
        var height = 48f * scale;
        var pillWidth = width * 0.4f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + pillWidth, y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.25f)), Metrics.Stroke.Hairline);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, y + 7f * scale), Loc.T(L.Casino.BarkeepScore),
            ui.MutedInk, TextStyles.Caption1);
        scoreRoll.Update(current.Score, delta);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, y + 21f * scale),
            GameNumber.Label(scoreRoll.Display), ui.TitleInk, TextStyles.SubheadlineEmphasized);

        var clock = ClockLabel(elapsed);
        var clockSize = Typography.Measure(clock, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left + width - clockSize.X, y + 6f * scale), clock, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var counter = Loc.T(L.Casino.BarkeepPatronCounter,
            GameNumber.Label(Math.Min(current.CompletedOrders + 1, current.PatronCount)),
            GameNumber.Label(current.PatronCount));
        var counterSize = Typography.Measure(counter, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(left + width - counterSize.X, y + 26f * scale), counter, ui.MutedInk,
            TextStyles.Caption1);
        return y + height;
    }

    private void DrawOrderCanvas(ImDrawListPtr drawList, AppSkin ui, BarkeepShift current, double elapsed,
        Rect canvas, float scale, float delta)
    {
        if (current.AllServed)
        {
            Typography.DrawCentered(drawList, canvas.Center, Loc.T(L.Casino.BarkeepShiftDone), ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            return;
        }

        if (!current.CurrentPatronArrived(elapsed))
        {
            verbStage.Cancel();
            Typography.DrawCentered(drawList, canvas.Center with { Y = canvas.Center.Y - 10f * scale },
                Loc.T(L.Casino.BarkeepNextPatron), ui.MutedInk, TextStyles.Subheadline);
            var wait = GameNumber.Label((int)Math.Ceiling(current.SecondsUntilCurrentPatron(elapsed)));
            Typography.DrawCentered(drawList, canvas.Center with { Y = canvas.Center.Y + 14f * scale }, wait,
                ui.TitleInk, TextStyles.Title3);
            return;
        }

        var stepsY = canvas.Min.Y + 18f * scale;
        DrawStepChips(drawList, ui, current, canvas.Center.X, stepsY, scale);

        var verbTitle = VerbName(current.CurrentStepKind);
        Typography.DrawCentered(drawList, new Vector2(canvas.Center.X, stepsY + 26f * scale), Loc.T(verbTitle),
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.DrawCentered(drawList, new Vector2(canvas.Center.X, stepsY + 46f * scale),
            Loc.T(VerbHint(current.CurrentStepKind)), ui.MutedInk, TextStyles.Caption1);

        var stageTop = stepsY + 58f * scale;
        var stage = new Rect(new Vector2(canvas.Min.X + 10f * scale, stageTop),
            new Vector2(canvas.Max.X - 10f * scale, canvas.Max.Y - 8f * scale));

        if (gradeFlashLeft > 0f && gradeFlash >= 0)
        {
            DrawGradeFlash(drawList, ui, stage, scale);
            return;
        }

        if (!verbStage.Active)
        {
            verbStage.Begin(current.CurrentStepKind, random);
        }

        var hovered = UiInteract.Hover(stage.Min, stage.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var tapped = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        verbStage.Update(delta, held, tapped);
        verbStage.Draw(drawList, ui, stage, scale);

        if (verbStage.TryTakeTapGrade(out var tapGrade))
        {
            CelebrateTap(ui, tapGrade, stage, scale);
        }

        if (!verbStage.TryTakeGrade(out var grade))
        {
            return;
        }

        gradeFlash = grade;
        gradeFlashLeft = GradeFlashSecondsTotal;
        current.CommitStepGrade(grade);
        CelebrateGrade(ui, grade, stage.Center, scale);
    }

    private void CelebrateTap(AppSkin ui, int grade, Rect stage, float scale)
    {
        tapFlashGrade = grade;
        tapFlashLeft = TapFlashSecondsTotal;
        tapFlashCenter = Vector2.Clamp(ImGui.GetIO().MousePos, stage.Min, stage.Max);
        var tint = BarkeepArt.GradeTint(grade, ui.Accent);
        if (grade != BarkeepGrading.MissGrade)
        {
            UiFeedback.Play(UiSound.GameTick);
        }

        switch (grade)
        {
            case BarkeepGrading.PerfectGrade:
                particles.Sparkle(tapFlashCenter, 8, Gold, 110f * scale, 2.6f, 0.55f);
                break;
            case BarkeepGrading.GoodGrade:
                particles.Burst(tapFlashCenter, 6, tint, 100f * scale, 2.2f, 0.45f);
                break;
            case BarkeepGrading.RoughGrade:
                particles.Burst(tapFlashCenter, 4, tint, 70f * scale, 2f, 0.4f);
                break;
            default:
                particles.Streaks(tapFlashCenter, 6, tint, 120f * scale, 2.2f, 0.45f, 0.9f, MathF.PI * 0.5f);
                break;
        }
    }

    private void CelebrateGrade(AppSkin ui, int grade, Vector2 origin, float scale)
    {
        switch (grade)
        {
            case BarkeepGrading.PerfectGrade:
                UiFeedback.Play(UiSound.GameMatch);
                particles.Sparkle(origin, 16, Gold, 150f * scale, 3f, 0.75f);
                break;
            case BarkeepGrading.GoodGrade:
                UiFeedback.Play(UiSound.GameCollect);
                particles.Burst(origin, 12, ui.Accent, 130f * scale, 2.6f, 0.6f);
                break;
            case BarkeepGrading.RoughGrade:
                particles.Burst(origin, 6, BarkeepArt.RoughAmber, 90f * scale, 2.2f, 0.5f);
                break;
            default:
                UiFeedback.Play(UiSound.GameWrong);
                particles.Streaks(origin, 10, BarkeepArt.MissRed, 150f * scale, 2.6f, 0.55f, 1.1f,
                    MathF.PI * 0.5f);
                break;
        }
    }

    private void DrawTapFlash(ImDrawListPtr drawList, AppSkin ui, float scale, float delta)
    {
        if (tapFlashLeft <= 0f)
        {
            return;
        }

        tapFlashLeft -= delta;
        var progress = 1f - MathF.Max(tapFlashLeft, 0f) / TapFlashSecondsTotal;
        var tint = BarkeepArt.GradeTint(tapFlashGrade, ui.Accent);
        var radius = (10f + 26f * Easing.EaseOutCubic(progress)) * scale;
        drawList.AddCircle(tapFlashCenter, radius,
            ImGui.GetColorU32(Palette.WithAlpha(tint, (1f - progress) * 0.85f)), 32,
            MathF.Max(1f, 2.4f * scale * (1f - progress * 0.5f)));
    }

    private void DrawStepChips(ImDrawListPtr drawList, AppSkin ui, BarkeepShift current, float centerX, float y,
        float scale)
    {
        var stepCount = current.CurrentStepCount;
        var gap = 30f * scale;
        for (var step = 0; step < stepCount; step++)
        {
            var chipCenter = new Vector2(centerX + (step - (stepCount - 1) * 0.5f) * gap, y);
            var done = step < current.CurrentStepIndex;
            var activeStep = step == current.CurrentStepIndex;
            var ink = done
                ? GradeColor(ui, current.CurrentOrderGrade(step))
                : activeStep
                    ? ui.Accent
                    : Palette.WithAlpha(ui.MutedInk, 0.45f);
            if (activeStep)
            {
                var pulse = 0.25f + 0.12f * MathF.Sin((float)ImGui.GetTime() * 5f);
                drawList.AddCircle(chipCenter, 13f * scale, ImGui.GetColorU32(Gold with { W = pulse }), 24,
                    1.6f * scale);
            }

            BarkeepArt.DrawVerbGlyph(drawList, current.StepKindAt(current.CurrentPatronIndex, step), chipCenter,
                8f * scale, ImGui.GetColorU32(ink));
        }
    }

    private void DrawGradeFlash(ImDrawListPtr drawList, AppSkin ui, Rect stage, float scale)
    {
        var label = gradeFlash switch
        {
            BarkeepGrading.PerfectGrade => Loc.T(L.Casino.BarkeepGradePerfect),
            BarkeepGrading.GoodGrade => Loc.T(L.Casino.BarkeepGradeGood),
            BarkeepGrading.RoughGrade => Loc.T(L.Casino.BarkeepGradeRough),
            _ => Loc.T(L.Casino.BarkeepGradeMiss),
        };
        var color = GradeColor(ui, gradeFlash);
        var progress = 1f - Math.Clamp(gradeFlashLeft / GradeFlashSecondsTotal, 0f, 1f);
        var fade = 1f - Easing.EaseOutCubic(progress);
        var rounding = 14f * scale;
        Squircle.Fill(drawList, stage.Min, stage.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(color, fade * 0.14f)));
        var inflate = Easing.EaseOutCubic(progress) * 6f * scale;
        Squircle.Stroke(drawList, stage.Min - new Vector2(inflate, inflate),
            stage.Max + new Vector2(inflate, inflate), rounding + inflate,
            ImGui.GetColorU32(Palette.WithAlpha(color, fade * 0.6f)), MathF.Max(1f, 2f * scale));

        var pop = GameJuice.PopIn(MathF.Min(1f, progress / 0.35f));
        var shakeX = gradeFlash == BarkeepGrading.MissGrade
            ? MathF.Sin(progress * 34f) * (1f - progress) * 5f * scale
            : 0f;
        Typography.DrawCentered(drawList,
            new Vector2(stage.Center.X + shakeX, stage.Center.Y - 10f * scale), label, color,
            TextStyles.Title3.Scale * (0.6f + 0.4f * pop), TextStyles.Title3.Weight);
        Typography.DrawCentered(drawList, stage.Center with { Y = stage.Center.Y + 14f * scale },
            "+" + GameNumber.Label(Math.Max(gradeFlash, 0)), Palette.WithAlpha(color, 0.8f),
            TextStyles.FootnoteEmphasized);
    }

    private static Vector4 GradeColor(AppSkin ui, int grade)
    {
        return BarkeepArt.GradeTint(grade, ui.Accent);
    }

    private void DrawSettle(ImDrawListPtr drawList, AppSkin ui, Rect body, float left, float width, float scale,
        float delta)
    {
        var result = settle!;
        var y = body.Min.Y + Metrics.Space.Lg * scale;
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y), Loc.T(L.Casino.BarkeepShiftDone),
            ui.TitleInk, TextStyles.Title3);
        y += 30f * scale;
        if (shift is not null)
        {
            var served = Loc.T(L.Casino.BarkeepServed, GameNumber.Label(shift.CompletedOrders),
                GameNumber.Label(shift.PatronCount));
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y), served, ui.MutedInk,
                TextStyles.Footnote);
            y += 24f * scale;
        }

        var scoreLine = Loc.T(L.Casino.BarkeepScore) + ": " + GameNumber.Label(result.Score);
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y), scoreLine, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        y += 34f * scale;

        if (result.Granted && result.Payout > 0)
        {
            if (!settleCelebrated)
            {
                settleCelebrated = true;
                UiFeedback.Play(UiSound.GameWin);
                particles.Confetti(new Vector2(left + width * 0.5f, y), 60, ConfettiPalette, 300f * scale, 5f, 1.4f);
            }

            payoutRoll.Update((int)result.Payout, delta);
            var amount = "+" + NumberText.Group((long)payoutRoll.Display);
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 8f * scale), amount, Gold,
                TextStyles.Title1.Scale * payoutRoll.PopScale, TextStyles.Title1.Weight);
            y += 44f * scale;
            if (string.Equals(result.Reason, CasinoReasons.CapReached, StringComparison.Ordinal))
            {
                y = DrawNoticeCard(drawList, ui, Loc.T(L.Casino.ReasonCapReached), ui.Accent, left, y, width, scale);
                y += Metrics.Space.Sm * scale;
            }
        }
        else if (!result.Granted && string.Equals(result.Reason, CasinoReasons.Expired, StringComparison.Ordinal))
        {
            var expired = Loc.T(L.Casino.BarkeepExpired);
            var block = Typography.MeasureWrappedBlock(expired, TextStyles.Footnote, width);
            Typography.DrawWrappedLeft(new Vector2(left, y), expired, ui.MutedInk, TextStyles.Footnote, width);
            y += block.Y + Metrics.Space.Md * scale;
        }
        else if (!result.Granted)
        {
            y = DrawNoticeCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(result.Reason)), ui.Accent, left, y,
                width, scale);
            y += Metrics.Space.Sm * scale;
        }
        else
        {
            var noTips = Loc.T(L.Casino.BarkeepNoTips, GameNumber.Label(BarkeepRules.LadderScoreFloors[0]));
            var block = Typography.MeasureWrappedBlock(noTips, TextStyles.Footnote, width);
            Typography.DrawWrappedLeft(new Vector2(left, y), noTips, ui.MutedInk, TextStyles.Footnote, width);
            y += block.Y + Metrics.Space.Md * scale;
        }

        y += Metrics.Space.Sm * scale;
        y = DrawLadderCard(drawList, ui, left, y, width, scale, BarkeepRules.LadderBand(result.Score));
        y += Metrics.Space.Lg * scale;

        var doneRect = new Rect(new Vector2(left + width * 0.25f, y), new Vector2(left + width * 0.75f, y + 44f * scale));
        if (AppSkin.PillButton(doneRect, Loc.T(L.Casino.BarkeepDone), true, true, ui.Theme))
        {
            settle = null;
            shift = null;
            mode = BarkeepMode.None;
            settleCelebrated = false;
            payoutRoll.Snap(0);
            scoreRoll.Snap(0);
        }
    }

    private void DrawPracticeSettle(ImDrawListPtr drawList, AppSkin ui, Rect body, float left, float width,
        float scale, float delta)
    {
        var y = body.Min.Y + Metrics.Space.Lg * scale;
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y), Loc.T(L.Casino.BarkeepShiftDone),
            ui.TitleInk, TextStyles.Title3);
        y += 34f * scale;
        payoutRoll.Update(practiceScore, delta);
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 8f * scale),
            GameNumber.Label(payoutRoll.Display), ui.TitleInk,
            TextStyles.Title1.Scale * payoutRoll.PopScale, TextStyles.Title1.Weight);
        y += 46f * scale;
        if (practiceNewBest)
        {
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y), Loc.T(L.Casino.BarkeepNewBest),
                Gold, TextStyles.FootnoteEmphasized);
        }
        else
        {
            var best = Loc.T(L.Casino.BarkeepBestScore, GameNumber.Label(stats.Get(PracticeStatsId).BestScore));
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y), best, ui.MutedInk,
                TextStyles.Footnote);
        }

        y += Metrics.Space.Lg * scale;
        var againRect = new Rect(new Vector2(left + width * 0.2f, y), new Vector2(left + width * 0.8f, y + 44f * scale));
        if (AppSkin.PillButton(againRect, Loc.T(L.Casino.BarkeepPracticeAgain), true, true, ui.Theme))
        {
            StartPractice();
        }

        y = againRect.Max.Y + Metrics.Space.Sm * scale;
        var doneRect = new Rect(new Vector2(left + width * 0.3f, y), new Vector2(left + width * 0.7f, y + 38f * scale));
        if (ui.GhostButton(doneRect, Loc.T(L.Casino.BarkeepDone)))
        {
            practiceSettled = false;
            shift = null;
            mode = BarkeepMode.None;
            payoutRoll.Snap(0);
            scoreRoll.Snap(0);
        }
    }

    private void DrawLobby(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale)
    {
        var state = store.State;
        if (state is null)
        {
            LoadingPulse.Draw(new Vector2(left + width * 0.5f, y + 120f * scale), 16f * scale, ui.Palette.Accent,
                ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        if (inlineReason.Length > 0)
        {
            y = DrawNoticeCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), ui.Accent, left, y,
                width, scale);
            y += Metrics.Space.Sm * scale;
        }

        var pad = 14f * scale;
        var sitting = state.Sitting;
        var seated = sitting is not null;
        var blocked = state.StakesPaused || state.Draining;
        var entryText = NumberText.Group(BarkeepRules.EntryChips);

        var wagerTitle = Loc.T(L.Casino.BarkeepWagerTitle);
        var wagerHint = Loc.T(L.Casino.BarkeepWagerHint, entryText);
        var hintBlock = Typography.MeasureWrappedBlock(wagerHint, TextStyles.Footnote, width - pad * 2f);
        var ladderHeight = 20f * scale * BarkeepRules.LadderScoreFloors.Length + 30f * scale;
        var wagerCardHeight = 24f * scale + hintBlock.Y + ladderHeight + 74f * scale + pad * 2f;
        var wagerMin = new Vector2(left, y);
        var wagerMax = new Vector2(left + width, y + wagerCardHeight);
        ui.Card(drawList, wagerMin, wagerMax, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(wagerMin.X + pad, wagerMin.Y + pad), wagerTitle, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(wagerMin.X + pad, wagerMin.Y + pad + 24f * scale), wagerHint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);
        var ladderY = wagerMin.Y + pad + 24f * scale + hintBlock.Y + 8f * scale;
        DrawLadderRows(drawList, ui, wagerMin.X + pad, ladderY, width - pad * 2f, scale, -1);

        var actionY = ladderY + ladderHeight - 16f * scale;
        if (!seated)
        {
            var needSeat = Loc.T(L.Casino.BarkeepNeedSeat);
            Typography.DrawWrappedLeft(new Vector2(wagerMin.X + pad, actionY), needSeat, ui.MutedInk,
                TextStyles.Footnote, width - pad * 2f);
            var seatRect = new Rect(new Vector2(left + width * 0.25f, actionY + 22f * scale),
                new Vector2(left + width * 0.75f, actionY + 60f * scale));
            if (ui.GhostButton(seatRect, Loc.T(L.Casino.Cashier)))
            {
                openCashier();
            }
        }
        else
        {
            var lowStack = sitting!.Stack < BarkeepRules.EntryChips;
            var canStart = !blocked && !lowStack && !play.RoundInFlight && !startRequested;
            var startRect = new Rect(new Vector2(left + width * 0.15f, actionY),
                new Vector2(left + width * 0.85f, actionY + 52f * scale));
            if (AppSkin.StackedPillButton(startRect, Loc.T(L.Casino.BarkeepStart), entryText, true, canStart,
                    ui.Theme))
            {
                inlineReason = string.Empty;
                StartWager();
                UiFeedback.Play(UiSound.CasinoChips);
            }

            if (blocked)
            {
                var notice = state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : Loc.T(L.Casino.DrainingTitle);
                Typography.DrawCentered(drawList,
                    new Vector2(left + width * 0.5f, startRect.Max.Y + 12f * scale), notice, ui.MutedInk,
                    TextStyles.Caption1);
            }
            else if (lowStack)
            {
                Typography.DrawCentered(drawList,
                    new Vector2(left + width * 0.5f, startRect.Max.Y + 12f * scale),
                    Loc.T(L.Casino.BarkeepLowStack), ui.MutedInk, TextStyles.Caption1);
            }
        }

        y = wagerMax.Y + Metrics.Space.Md * scale;

        var practiceTitle = Loc.T(L.Casino.BarkeepPracticeTitle);
        var practiceHint = Loc.T(L.Casino.BarkeepPracticeHint);
        var practiceHintBlock = Typography.MeasureWrappedBlock(practiceHint, TextStyles.Footnote, width - pad * 2f);
        var practiceCardHeight = 24f * scale + practiceHintBlock.Y + 88f * scale + pad;
        var practiceMin = new Vector2(left, y);
        var practiceMax = new Vector2(left + width, y + practiceCardHeight);
        ui.Card(drawList, practiceMin, practiceMax, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(practiceMin.X + pad, practiceMin.Y + pad), practiceTitle, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(practiceMin.X + pad, practiceMin.Y + pad + 24f * scale), practiceHint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);
        var bestY = practiceMin.Y + pad + 24f * scale + practiceHintBlock.Y + 6f * scale;
        var best = Loc.T(L.Casino.BarkeepBestScore, GameNumber.Label(stats.Get(PracticeStatsId).BestScore));
        Typography.Draw(drawList, new Vector2(practiceMin.X + pad, bestY), best, ui.MutedInk, TextStyles.Caption1);
        var practiceRect = new Rect(new Vector2(left + width * 0.2f, bestY + 22f * scale),
            new Vector2(left + width * 0.8f, bestY + 66f * scale));
        if (AppSkin.PillButton(practiceRect, practiceTitle, false, true, ui.Theme))
        {
            StartPractice();
        }
    }

    private float DrawLadderCard(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale,
        int reachedBand)
    {
        var pad = 14f * scale;
        var rowsHeight = 20f * scale * BarkeepRules.LadderScoreFloors.Length;
        var height = 24f * scale + rowsHeight + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), Loc.T(L.Casino.BarkeepLadderTitle),
            ui.TitleInk, TextStyles.FootnoteEmphasized);
        DrawLadderRows(drawList, ui, min.X + pad, min.Y + pad + 24f * scale, width - pad * 2f, scale, reachedBand);
        return max.Y;
    }

    private static void DrawLadderRows(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width,
        float scale, int reachedBand)
    {
        for (var band = 0; band < BarkeepRules.LadderScoreFloors.Length; band++)
        {
            var rowY = y + band * 20f * scale;
            var reached = band == reachedBand;
            var ink = reached ? Gold : ui.BodyInk;
            var row = Loc.T(L.Casino.BarkeepLadderRow, GameNumber.Label(BarkeepRules.LadderScoreFloors[band]),
                GameNumber.Label((int)BarkeepRules.LadderPays[band]));
            Typography.Draw(drawList, new Vector2(left, rowY), row, ink,
                reached ? TextStyles.FootnoteEmphasized : TextStyles.Footnote);
        }
    }

    private static float DrawNoticeCard(ImDrawListPtr drawList, AppSkin ui, string message, Vector4 tint, float left,
        float y, float width, float scale)
    {
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.35f)),
            1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, width - pad * 2f);
        return max.Y;
    }

    private void StartWager()
    {
        if (play.RoundInFlight || shift is not null)
        {
            return;
        }

        startRequested = true;
        play.StartBarkeep();
    }

    private void StartPractice()
    {
        shift = new BarkeepShift(string.Empty, BarkeepShift.PracticeScript(random),
            (long)NowUnixSeconds());
        mode = BarkeepMode.Practice;
        practiceSettled = false;
        practiceNewBest = false;
        settle = null;
        gradeFlashLeft = 0f;
        tapFlashLeft = 0f;
        verbStage.Cancel();
        scoreRoll.Snap(0);
        payoutRoll.Snap(0);
    }

    private void EndPractice()
    {
        if (shift is null)
        {
            return;
        }

        practiceScore = shift.Score;
        practiceNewBest = stats.SubmitScore(PracticeStatsId, practiceScore);
        practiceSettled = true;
        payoutRoll.Snap(0);
        verbStage.Cancel();
    }

    private static LocString VerbName(int stepKind)
    {
        return stepKind switch
        {
            BarkeepRules.PourKind => L.Casino.BarkeepVerbPour,
            BarkeepRules.ShakeKind => L.Casino.BarkeepVerbShake,
            BarkeepRules.LayerKind => L.Casino.BarkeepVerbLayer,
            _ => L.Casino.BarkeepVerbGarnish,
        };
    }

    private static LocString VerbHint(int stepKind)
    {
        return stepKind switch
        {
            BarkeepRules.PourKind => L.Casino.BarkeepHintPour,
            BarkeepRules.ShakeKind => L.Casino.BarkeepHintShake,
            BarkeepRules.LayerKind => L.Casino.BarkeepHintLayer,
            _ => L.Casino.BarkeepHintGarnish,
        };
    }

    private string ClockLabel(double elapsedSeconds)
    {
        var total = (int)elapsedSeconds;
        if (total != clockSecond)
        {
            clockSecond = total;
            clockText = string.Concat(GameNumber.Label(total / 60), ":",
                (total % 60).ToString("00", Loc.Culture));
        }

        return clockText;
    }
}

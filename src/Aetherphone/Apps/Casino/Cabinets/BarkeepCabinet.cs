using Aetherphone.Apps.Casino.Stage;
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

internal sealed class BarkeepCabinet : ICabinetIdle
{
    private const string PracticeStatsId = "casino.barkeep";
    private const string MalformedReason = "malformed";
    private const float GradeFlashSecondsTotal = 0.8f;
    private const float TapFlashSecondsTotal = 0.35f;
    private const double FinishRetryDelaySeconds = 3.0;
    private const double FinishFailureRetryDelaySeconds = 5.0;
    private const float SignShare = 0.12f;
    private const float ShelfShare = 0.30f;
    private const float CounterShare = 0.46f;
    private const float CounterTopHeight = 14f;
    private const float CounterFrontHeight = 10f;
    private const float PatronShare = 0.62f;
    private const float WorkGap = 10f;
    private const float PunchDecay = 6f;
    private const float ShakePunch = 0.035f;
    private const float ShakeJitter = 26f;
    private const float FeverRise = 3f;
    private const float FeverFall = 1.5f;
    private const float TossSeconds = 0.55f;
    private const float TossArc = 0.6f;
    private const float CardPad = 14f;
    private const float CardGap = 6f;
    private const float LadderRowHeight = 22f;
    private const float ShadowOffset = 1.5f;
    private const float PracticePillShare = 0.6f;
    private const float HudPad = 4f;
    private const ulong JitterSeed = 0xBA2C4EE5UL;
    private const ulong IdleSeed = 0x1D1EBA2UL;
    private const int FeverSparkles = 24;

    private static readonly Vector4 Gold = BarkeepArt.PerfectGold;
    private static readonly Vector4 CardFill = new(0.06f, 0.035f, 0.05f, 0.94f);
    private static readonly Vector4 Veil = new(0f, 0f, 0f, 0.45f);

    private readonly CasinoStore store;
    private readonly CasinoPlayStore play;
    private readonly GameStatsStore stats;
    private readonly BarkeepVerbStage verbStage = new();
    private readonly BarkeepBarFlow flow = new();
    private readonly BarkeepBarFlow idleFlow = new();
    private readonly BarkeepTipMeter tips = new();
    private readonly BetComposer composer = new("##barkeepEntry");
    private readonly Random random = new();
    private readonly string[] shareRows = new string[BarkeepRules.LadderRatioFloors.Length];
    private readonly string[] pointRows = new string[BarkeepRules.LadderRatioFloors.Length];

    private RollingValue scoreRoll;
    private BarkeepShift? shift;
    private BarkeepMode mode;
    private CasinoBarkeepFinishDto? settle;
    private bool settleRecorded;
    private bool practiceSettled;
    private int practiceScore;
    private bool practiceNewBest;
    private bool startRequested;
    private bool finishSent;
    private bool snapFlow;
    private double finishRetryAtElapsed;
    private int gradeFlash = -1;
    private float gradeFlashLeft;
    private int tapFlashGrade = -1;
    private float tapFlashLeft;
    private Vector2 tapFlashCenter;
    private string inlineReason = string.Empty;
    private float punch;
    private float fever;
    private float idleTime;
    private float tossLeft;
    private bool tossLanded;
    private Vector2 tossFrom;
    private Vector2 tossTo;
    private GameRandom jitter = GameRandom.FromSeed(JitterSeed);
    private LabelSlot comboLabel;
    private LabelSlot waitLabel;
    private LabelSlot lastCallLabel;
    private LabelSlot noTipsLabel;
    private LabelSlot bestLabel;
    private LabelPairSlot counterLabel;
    private LabelPairSlot scoreLabel;
    private LabelPairSlot servedLabel;
    private LanguageInfo? shareLanguage;
    private LanguageInfo? pointLanguage;
    private int pointMaxScore = -1;
    private string wagerHint = string.Empty;

    public BarkeepCabinet(CasinoStore store, CasinoPlayStore play, GameStatsStore stats)
    {
        this.store = store;
        this.play = play;
        this.stats = stats;
        idleFlow.Begin(IdleSeed, BarkeepBarFlow.VisibleQueue + 1);
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public static float DeckHeight => BetComposer.Pad * 2f + BetComposer.ActionHeight * 2f + BetComposer.Gap
        + HudExtra;

    private const float HudExtra = 8f;

    public static int ReturnTenths => BarkeepRules.ReturnBasisPointsAtPerfectPlay / 10;

    public bool Practicing => mode == BarkeepMode.Practice && (shift is not null || practiceSettled);

    public void Enter()
    {
        inlineReason = string.Empty;
        snapFlow = true;
        play.RecoverPendingRound();
    }

    public void Reset()
    {
        if (mode == BarkeepMode.Wager && shift is not null && settle is null)
        {
            verbStage.Cancel();
            gradeFlashLeft = 0f;
            tapFlashLeft = 0f;
            tossLeft = 0f;
            inlineReason = string.Empty;
            snapFlow = true;
            return;
        }

        shift = null;
        mode = BarkeepMode.None;
        settle = null;
        settleRecorded = false;
        practiceSettled = false;
        practiceNewBest = false;
        startRequested = false;
        finishSent = false;
        finishRetryAtElapsed = 0;
        gradeFlash = -1;
        gradeFlashLeft = 0f;
        tapFlashLeft = 0f;
        tossLeft = 0f;
        punch = 0f;
        fever = 0f;
        inlineReason = string.Empty;
        verbStage.Cancel();
        tips.Reset();
        scoreRoll.Snap(0);
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

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var delta = frame.DeltaSeconds;
        tips.Update(delta);
        punch = MathF.Max(0f, punch - delta * PunchDecay * MathF.Max(punch, 0.01f) * 30f);
        var feverTarget = tips.Fever && shift is not null ? 1f : 0f;
        fever = feverTarget > fever
            ? MathF.Min(feverTarget, fever + delta * FeverRise)
            : MathF.Max(feverTarget, fever - delta * FeverFall);
        if (frame.SnapToTruth)
        {
            snapFlow = true;
        }

        var scene = BarkeepScene.Compute(frame, scale);
        var sceneStart = drawList.VtxBuffer.Size;
        DrawBar(drawList, scene, frame.Phase, delta, scale);
        DrawWork(drawList, stage, frame, ui, scene, scale);
        ApplyCamera(drawList, sceneStart, scene.Work.Center, scale);
        DrawDeck(stage, frame, ui, scale);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleTime += deltaSeconds;
        var counterY = rect.Min.Y + rect.Height * 0.72f;
        BarkeepSceneArt.Wall(drawList, new Rect(rect.Min, new Vector2(rect.Max.X, counterY)));
        var shelves = new Rect(new Vector2(rect.Min.X, rect.Min.Y + rect.Height * 0.2f),
            new Vector2(rect.Max.X, rect.Min.Y + rect.Height * 0.62f));
        BarkeepSceneArt.Shelves(drawList, shelves, idleTime, 0f, scale);
        BarkeepSceneArt.Sign(drawList, new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.1f), rect.Width * 0.4f,
            rect.Height * 0.14f, idleTime, 0f);
        var unit = rect.Height * 0.4f;
        for (var patronIndex = 0; patronIndex <= BarkeepBarFlow.VisibleQueue; patronIndex++)
        {
            var across = BarkeepBarFlow.TargetFor(patronIndex, 0) - 0.18f;
            var bob = MathF.Sin(idleTime * 2.4f + patronIndex) * 1.2f * scale;
            BarkeepSceneArt.Patron(drawList, idleFlow.LookOf(patronIndex),
                new Vector2(rect.Min.X + rect.Width * across, counterY), unit, bob, 1f);
        }

        BarkeepSceneArt.Counter(drawList, new Rect(new Vector2(rect.Min.X, counterY),
            new Vector2(rect.Max.X, counterY + CounterTopHeight * scale)), new Rect(
            new Vector2(rect.Min.X, counterY + CounterTopHeight * scale), rect.Max), scale);
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
                    BeginShift(fresh, BarkeepMode.Wager, BarkeepBarFlow.SeedOf(fresh.RoundId));
                    snapFlow = fresh.ElapsedSeconds(NowUnixSeconds()) > BarkeepRules.SecondsPerOrder;
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
                settleRecorded = false;
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

    private void BeginShift(BarkeepShift next, BarkeepMode nextMode, ulong seed)
    {
        shift = next;
        mode = nextMode;
        settle = null;
        settleRecorded = false;
        practiceSettled = false;
        practiceNewBest = false;
        finishSent = false;
        finishRetryAtElapsed = 0;
        gradeFlashLeft = 0f;
        tapFlashLeft = 0f;
        tossLeft = 0f;
        inlineReason = string.Empty;
        verbStage.Cancel();
        tips.Reset();
        flow.Begin(seed, next.PatronCount);
        scoreRoll.Snap(next.Score);
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

    private void DrawBar(ImDrawListPtr drawList, in BarkeepScene scene, float phase, float delta, float scale)
    {
        BarkeepSceneArt.Wall(drawList, scene.Wall);
        BarkeepSceneArt.Shelves(drawList, scene.Shelves, phase, fever, scale);
        if (fever > 0f)
        {
            CasinoLights.BulbChase(drawList, scene.Shelves.Inset(-4f * scale), Metrics.Radius.Grouped * scale, scale,
                phase * 2f, CasinoLights.BulbPitch, CasinoColors.Money, BarkeepSceneArt.FeverTint(phase), fever);
        }

        BarkeepSceneArt.Sign(drawList, scene.SignCenter, scene.Wall.Width * 0.36f, scene.SignHeight, phase, fever);
        DrawPatrons(drawList, scene, phase, delta, scale);
        BarkeepSceneArt.Counter(drawList, scene.CounterTop, scene.CounterFront, scale);
        DrawComboChip(drawList, scene, phase, scale);
    }

    private void DrawPatrons(ImDrawListPtr drawList, in BarkeepScene scene, float phase, float delta, float scale)
    {
        if (shift is null)
        {
            DrawIdlePatrons(drawList, scene, phase, scale);
            return;
        }

        var elapsed = shift.ElapsedSeconds(NowUnixSeconds());
        if (snapFlow)
        {
            flow.Snap(shift, elapsed);
            snapFlow = false;
        }
        else
        {
            flow.Update(shift, elapsed, delta);
        }

        var unit = scene.PatronUnit;
        var feetY = scene.CounterTop.Min.Y + unit * 0.1f;
        for (var patronIndex = flow.PatronCount - 1; patronIndex >= 0; patronIndex--)
        {
            if (!flow.IsVisible(patronIndex))
            {
                continue;
            }

            var feet = new Vector2(scene.Wall.Min.X + scene.Wall.Width * flow.PositionOf(patronIndex), feetY);
            var bob = MathF.Sin(phase * 2.4f + patronIndex * 1.7f) * 1.2f * scale;
            BarkeepSceneArt.Patron(drawList, flow.LookOf(patronIndex), feet, unit, bob, 1f);
        }

        var current = shift.CurrentPatronIndex;
        if (shift.AllServed || !flow.AtRail(shift, current) || settle is not null || practiceSettled)
        {
            return;
        }

        var head = new Vector2(scene.Wall.Min.X + scene.Wall.Width * flow.PositionOf(current),
            BarkeepSceneArt.HeadTop(flow.LookOf(current), new Vector2(0f, feetY), unit));
        var pulse = 0.5f + 0.5f * MathF.Sin(phase * 5f);
        BarkeepSceneArt.OrderBubble(drawList, shift, head, CasinoColors.LightA, pulse, scale);
    }

    private void DrawIdlePatrons(ImDrawListPtr drawList, in BarkeepScene scene, float phase, float scale)
    {
        var unit = scene.PatronUnit;
        var feetY = scene.CounterTop.Min.Y + unit * 0.1f;
        for (var patronIndex = BarkeepBarFlow.VisibleQueue; patronIndex >= 1; patronIndex--)
        {
            var across = BarkeepBarFlow.TargetFor(patronIndex, 0);
            var bob = MathF.Sin(phase * 2.4f + patronIndex * 1.7f) * 1.2f * scale;
            BarkeepSceneArt.Patron(drawList, idleFlow.LookOf(patronIndex),
                new Vector2(scene.Wall.Min.X + scene.Wall.Width * across, feetY), unit, bob, 0.85f);
        }
    }

    private void DrawComboChip(ImDrawListPtr drawList, in BarkeepScene scene, float phase, float scale)
    {
        if (shift is null || tips.Count < 2)
        {
            return;
        }

        var label = comboLabel.Get(L.Barkeep.Combo, tips.Count);
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var padding = new Vector2(10f, 4f) * scale;
        var min = new Vector2(scene.Shelves.Min.X + 12f * scale, scene.Shelves.Min.Y + 4f * scale);
        var max = min + size + padding * 2f;
        var tint = tips.Fever ? BarkeepSceneArt.FeverTint(phase) : Gold;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(CardFill));
        Squircle.Stroke(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(tint with { W = 0.8f }),
            MathF.Max(1f, scale));
        Typography.Draw(drawList, min + padding, label, tint, style);
        var heatWidth = (max.X - min.X) * tips.Heat;
        drawList.AddLine(new Vector2(min.X + padding.X * 0.5f, max.Y + 3f * scale),
            new Vector2(min.X + padding.X * 0.5f + heatWidth, max.Y + 3f * scale), ImGui.GetColorU32(tint),
            MathF.Max(1f, 2f * scale));
        if (!tips.Fever)
        {
            return;
        }

        var feverText = Loc.T(L.Barkeep.Fever);
        var feverSize = Typography.Measure(feverText, TextStyles.Headline);
        var pop = 1f + 0.08f * MathF.Sin(phase * 8f);
        Typography.Draw(drawList, new Vector2(max.X + 10f * scale, (min.Y + max.Y - feverSize.Y * pop) * 0.5f), feverText,
            tint, TextStyles.Headline.Scale * pop, TextStyles.Headline.Weight);
    }

    private void DrawWork(ImDrawListPtr drawList, CasinoStage stage, in CasinoStageFrame frame, AppSkin ui,
        in BarkeepScene scene, float scale)
    {
        var work = scene.Work;
        if (settle is not null)
        {
            DrawSettleCard(drawList, stage, frame, ui, scene, scale);
            return;
        }

        if (practiceSettled)
        {
            DrawPracticeCard(drawList, frame, ui, scene, scale);
            return;
        }

        if (shift is not null && finishSent || shift is null && (startRequested || play.RoundInFlight))
        {
            LoadingPulse.Draw(work.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        if (shift is not null)
        {
            DrawShiftWork(drawList, stage, frame, ui, work, scale);
            return;
        }

        DrawLobbyWork(drawList, ui, work, scale);
    }

    private void DrawLobbyWork(ImDrawListPtr drawList, AppSkin ui, Rect work, float scale)
    {
        var state = store.State;
        var top = work.Min.Y;
        var width = work.Width;
        if (inlineReason.Length > 0)
        {
            top = CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty,
                Loc.T(CasinoReasons.MessageFor(inlineReason)), work.Min.X, top, width, scale) + CardGap * scale;
        }

        RefreshShareRows();
        top = Typography.DrawWrappedCentered(drawList, wagerHint, TextStyles.Subheadline, CasinoColors.InkTitle,
            new Vector2(work.Center.X, top), width) + CardGap * scale;
        for (var band = 0; band < shareRows.Length; band++)
        {
            var row = Typography.FitText(shareRows[band], width, TextStyles.Footnote);
            Typography.DrawCentered(drawList, new Vector2(work.Center.X, top + LadderRowHeight * scale * 0.5f), row,
                band == shareRows.Length - 1 ? Gold : CasinoColors.InkTitle, TextStyles.Footnote);
            top += LadderRowHeight * scale;
        }

        top += CardGap * scale;
        var best = bestLabel.Get(L.Casino.BarkeepBestScore, stats.Get(PracticeStatsId).BestScore);
        Typography.DrawCentered(drawList, new Vector2(work.Center.X, top + LadderRowHeight * scale * 0.5f),
            Typography.FitText(best, width, TextStyles.Footnote), CasinoColors.InkBody, TextStyles.Footnote);
        if (state is null || state.Sitting is not null)
        {
            return;
        }

        top += LadderRowHeight * scale + CardGap * scale;
        Typography.DrawWrappedCentered(drawList, Loc.T(L.Casino.BarkeepNeedSeat), TextStyles.Subheadline,
            CasinoColors.InkTitle, new Vector2(work.Center.X, top), width);
    }

    private void DrawShiftWork(ImDrawListPtr drawList, CasinoStage stage, in CasinoStageFrame frame, AppSkin ui,
        Rect work, float scale)
    {
        var current = shift!;
        var delta = frame.DeltaSeconds;
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
        var top = work.Min.Y;
        if (mode == BarkeepMode.Wager && current.InLastCall(elapsed))
        {
            var warning = lastCallLabel.Get(L.Casino.BarkeepLastCall,
                (int)Math.Ceiling(current.SecondsUntilForfeit(elapsed)));
            top = CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Info, string.Empty, warning, work.Min.X, top,
                work.Width, scale) + CardGap * scale;
        }

        var area = new Rect(new Vector2(work.Min.X, top), work.Max);
        if (current.AllServed)
        {
            Typography.DrawCentered(drawList, area.Center, Loc.T(L.Casino.BarkeepShiftDone), CasinoColors.InkTitle,
                TextStyles.Title3);
            return;
        }

        if (!flow.AtRail(current, current.CurrentPatronIndex) || !current.CurrentPatronArrived(elapsed))
        {
            verbStage.Cancel();
            ShadowedCentered(drawList, area.Center with { Y = area.Center.Y - 16f * scale },
                Typography.FitText(Loc.T(L.Casino.BarkeepNextPatron), area.Width, TextStyles.Title2),
                CasinoColors.InkTitle, TextStyles.Title2, scale);
            var wait = (int)Math.Ceiling(current.SecondsUntilCurrentPatron(elapsed));
            if (wait > 0)
            {
                ShadowedCentered(drawList, area.Center with { Y = area.Center.Y + 18f * scale },
                    GameNumber.Label(wait), CasinoColors.Money, TextStyles.Title2, scale);
            }

            return;
        }

        var kind = current.CurrentStepKind;
        var titleY = area.Min.Y + Typography.LineHeight(TextStyles.Title2) * 0.5f;
        ShadowedCentered(drawList, new Vector2(area.Center.X, titleY),
            Typography.FitText(Loc.T(VerbName(kind)), area.Width, TextStyles.Title2), CasinoColors.InkTitle,
            TextStyles.Title2, scale);
        var hintTop = titleY + Typography.LineHeight(TextStyles.Title2) * 0.5f + 2f * scale;
        var hintBottom = Typography.DrawWrappedCentered(drawList, Loc.T(VerbHint(kind)), TextStyles.Subheadline,
            CasinoColors.InkBody, new Vector2(area.Center.X, hintTop), area.Width);
        var canvas = new Rect(new Vector2(area.Min.X + 8f * scale, hintBottom + 6f * scale),
            new Vector2(area.Max.X - 8f * scale, area.Max.Y));
        if (canvas.Height <= 0f)
        {
            return;
        }

        if (gradeFlashLeft > 0f && gradeFlash >= 0)
        {
            DrawGradeFlash(drawList, canvas, scale);
            DrawToss(drawList, delta, scale);
            DrawTapFlash(drawList, ui, delta, scale);
            return;
        }

        if (!verbStage.Active)
        {
            verbStage.Begin(kind, random);
        }

        var blocked = frame.Blocked || stage.OverlayOpen;
        var hovered = !blocked && UiInteract.Hover(canvas.Min, canvas.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiInteract.ReportGestureSurface();
        }

        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var tapped = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        verbStage.Update(delta, held, tapped);
        verbStage.Draw(drawList, ui, canvas, delta, scale);
        if (verbStage.TakeShakeTap())
        {
            punch = MathF.Max(punch, ShakePunch);
            verbStage.Jolt();
        }

        if (verbStage.TryTakeTapGrade(out var tapGrade))
        {
            CelebrateTap(stage, ui, tapGrade, canvas, scale);
        }

        DrawTapFlash(drawList, ui, delta, scale);
        if (!verbStage.TryTakeGrade(out var grade))
        {
            return;
        }

        gradeFlash = grade;
        gradeFlashLeft = GradeFlashSecondsTotal;
        if (kind == BarkeepRules.GarnishKind)
        {
            tossFrom = verbStage.GarnishFrom;
            tossTo = verbStage.GarnishTo;
            tossLanded = grade >= BarkeepGrading.RoughGrade;
            tossLeft = TossSeconds;
        }

        current.CommitStepGrade(grade);
        CelebrateGrade(stage, grade, canvas.Center, scale);
    }

    private void CelebrateTap(CasinoStage stage, AppSkin ui, int grade, Rect canvas, float scale)
    {
        tapFlashGrade = grade;
        tapFlashLeft = TapFlashSecondsTotal;
        tapFlashCenter = Vector2.Clamp(ImGui.GetIO().MousePos, canvas.Min, canvas.Max);
        var tint = BarkeepArt.GradeTint(grade, ui.Accent);
        if (grade != BarkeepGrading.MissGrade)
        {
            CasinoSfx.Play(UiSound.GameTick);
        }

        var particles = stage.Particles;
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
        }
    }

    private void CelebrateGrade(CasinoStage stage, int grade, Vector2 origin, float scale)
    {
        var particles = stage.Particles;
        switch (grade)
        {
            case BarkeepGrading.PerfectGrade:
                CasinoSfx.Play(UiSound.GameMatch);
                particles.Sparkle(origin, 16, Gold, 150f * scale, 3f, 0.75f);
                break;
            case BarkeepGrading.GoodGrade:
                CasinoSfx.Play(UiSound.GameCollect);
                particles.Burst(origin, 12, CasinoColors.LightA, 130f * scale, 2.6f, 0.6f);
                break;
            case BarkeepGrading.RoughGrade:
                particles.Burst(origin, 6, BarkeepArt.RoughAmber, 90f * scale, 2.2f, 0.5f);
                break;
            default:
                CasinoSfx.Play(UiSound.Bust);
                break;
        }

        switch (tips.Grade(grade))
        {
            case BarkeepFeverChange.Started:
                CasinoSfx.Play(UiSound.TurnChime);
                CasinoLights.LightSweep(stage.Backdrop, 0.6f);
                particles.Emit(CasinoLights.Sparkle(scale), origin, FeverSparkles);
                break;
            case BarkeepFeverChange.Ended:
                break;
        }
    }

    private void DrawToss(ImDrawListPtr drawList, float delta, float scale)
    {
        if (tossLeft <= 0f)
        {
            return;
        }

        tossLeft = MathF.Max(0f, tossLeft - delta);
        var progress = 1f - tossLeft / TossSeconds;
        var end = tossLanded ? tossTo : tossTo + new Vector2(tossTo.X > tossFrom.X ? 60f * scale : -60f * scale,
            30f * scale);
        var position = Vector2.Lerp(tossFrom, end, progress);
        var height = MathF.Abs(end.X - tossFrom.X) * TossArc + 30f * scale;
        position.Y -= MathF.Sin(progress * MathF.PI) * height;
        BarkeepVerbStage.DrawCherry(drawList, position, 9f * scale, scale);
        if (tossLeft <= 0f && tossLanded)
        {
            CasinoSfx.Play(UiSound.Daub);
        }
    }

    private void DrawTapFlash(ImDrawListPtr drawList, AppSkin ui, float delta, float scale)
    {
        if (tapFlashLeft <= 0f)
        {
            return;
        }

        tapFlashLeft -= delta;
        var progress = 1f - MathF.Max(tapFlashLeft, 0f) / TapFlashSecondsTotal;
        var tint = BarkeepArt.GradeTint(tapFlashGrade, ui.Accent);
        var radius = (10f + 26f * Easing.EaseOutCubic(progress)) * scale;
        drawList.AddCircle(tapFlashCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, (1f - progress) * 0.85f)),
            32, MathF.Max(1f, 2.4f * scale * (1f - progress * 0.5f)));
    }

    private void DrawGradeFlash(ImDrawListPtr drawList, Rect canvas, float scale)
    {
        var label = gradeFlash switch
        {
            BarkeepGrading.PerfectGrade => Loc.T(L.Casino.BarkeepGradePerfect),
            BarkeepGrading.GoodGrade => Loc.T(L.Casino.BarkeepGradeGood),
            BarkeepGrading.RoughGrade => Loc.T(L.Casino.BarkeepGradeRough),
            _ => Loc.T(L.Casino.BarkeepGradeMiss),
        };
        var color = BarkeepArt.GradeTint(gradeFlash, CasinoColors.LightA);
        var progress = 1f - Math.Clamp(gradeFlashLeft / GradeFlashSecondsTotal, 0f, 1f);
        var fade = 1f - Easing.EaseOutCubic(progress);
        var rounding = 14f * scale;
        Squircle.Fill(drawList, canvas.Min, canvas.Max, rounding, ImGui.GetColorU32(Palette.WithAlpha(color, fade * 0.14f)));
        var inflate = Easing.EaseOutCubic(progress) * 6f * scale;
        Squircle.Stroke(drawList, canvas.Min - new Vector2(inflate, inflate), canvas.Max + new Vector2(inflate, inflate),
            rounding + inflate, ImGui.GetColorU32(Palette.WithAlpha(color, fade * 0.6f)), MathF.Max(1f, 2f * scale));
        var pop = GameJuice.PopIn(MathF.Min(1f, progress / 0.35f));
        var shakeX = gradeFlash == BarkeepGrading.MissGrade
            ? MathF.Sin(progress * 34f) * (1f - progress) * 5f * scale
            : 0f;
        var fitted = Typography.FitText(label, canvas.Width, TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(canvas.Center.X + shakeX, canvas.Center.Y - 10f * scale), fitted,
            color, TextStyles.Title2.Scale * (0.6f + 0.4f * pop), TextStyles.Title2.Weight);
        Typography.DrawCentered(drawList, canvas.Center with { Y = canvas.Center.Y + 16f * scale },
            GameNumber.Signed(Math.Max(gradeFlash, 0)), Palette.WithAlpha(color, 0.8f), TextStyles.FootnoteEmphasized);
    }

    private void DrawSettleCard(ImDrawListPtr drawList, CasinoStage stage, in CasinoStageFrame frame, AppSkin ui,
        in BarkeepScene scene, float scale)
    {
        var result = settle!;
        var maxScore = shift?.MaxScore ?? 0;
        RecordSettle(stage, frame, result);
        drawList.AddRectFilled(frame.Full.Min, frame.Full.Max, ImGui.GetColorU32(Veil));
        var card = scene.Card;
        var pad = CardPad * scale;
        var width = card.Width - pad * 2f;
        var height = SettleHeight(result, maxScore, width, scale) + pad * 2f;
        var top = card.Center.Y - height * 0.5f;
        var min = new Vector2(card.Min.X, MathF.Max(card.Min.Y, top));
        var max = new Vector2(card.Max.X, min.Y + height);
        PaintCard(drawList, min, max, scale);
        var centerX = (min.X + max.X) * 0.5f;
        var y = min.Y + pad;
        y = Line(drawList, Loc.T(L.Casino.BarkeepShiftDone), centerX, y, width, CasinoColors.InkTitle, TextStyles.Title3);
        if (shift is not null)
        {
            y = Line(drawList, servedLabel.Get(L.Casino.BarkeepServed, shift.CompletedOrders, shift.PatronCount), centerX,
                y, width, CasinoColors.InkBody, TextStyles.Footnote);
        }

        y += CardGap * scale;
        y = Line(drawList, Loc.T(L.Casino.BarkeepScore), centerX, y, width, CasinoColors.InkBody, TextStyles.Footnote);
        y = Line(drawList, scoreLabel.Get(L.Barkeep.ScoreOf, result.Score, maxScore), centerX, y, width,
            CasinoColors.InkTitle, TextStyles.Title2);
        y += CardGap * scale;
        if (result.Granted)
        {
            y = Line(drawList, Loc.T(L.Barkeep.Tips), centerX, y, width, CasinoColors.InkBody, TextStyles.Footnote);
            var amount = NumberText.Group(result.Payout);
            var size = CurrencyGlyph.MeasureAmount(amount, TextStyles.Title3);
            CurrencyGlyph.DrawAmount(drawList, new Vector2(centerX - size.X * 0.5f, y), amount, CurrencyKind.Chips,
                CasinoColors.InkBody, TextStyles.Title3);
            y += size.Y + CardGap * scale;
        }

        var notice = SettleNotice(result, maxScore);
        if (notice.Length > 0)
        {
            y = Typography.DrawWrappedCentered(drawList, notice, TextStyles.Subheadline, CasinoColors.InkBody,
                new Vector2(centerX, y), width) + CardGap * scale;
        }

        DrawPointRows(drawList, min.X + pad, y, width, maxScore, BarkeepRules.LadderBand(result.Score, maxScore), scale);
    }

    private void RecordSettle(CasinoStage stage, in CasinoStageFrame frame, CasinoBarkeepFinishDto result)
    {
        if (settleRecorded)
        {
            return;
        }

        settleRecorded = true;
        if (!result.Granted)
        {
            return;
        }

        stage.Settle(new CasinoBetRecord(L.Casino.GameBarkeep, BarkeepRules.EntryChips, result.Payout, result.RoundId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), result.Capped));
        stage.Celebration.Celebrate(BarkeepRules.EntryChips, result.Payout, frame.Safe.Center, frame.Instant);
    }

    private float SettleHeight(CasinoBarkeepFinishDto result, int maxScore, float width, float scale)
    {
        var gap = CardGap * scale;
        var height = Typography.LineHeight(TextStyles.Title3) + Typography.LineHeight(TextStyles.Footnote) + gap
            + Typography.LineHeight(TextStyles.Footnote) + Typography.LineHeight(TextStyles.Title2) + gap;
        if (result.Granted)
        {
            height += Typography.LineHeight(TextStyles.Footnote) + Typography.LineHeight(TextStyles.Title3) + gap;
        }

        var notice = SettleNotice(result, maxScore);
        if (notice.Length > 0)
        {
            height += Typography.MeasureWrappedBlock(notice, TextStyles.Subheadline, width).Y + gap;
        }

        return height + LadderRowHeight * scale * BarkeepRules.LadderRatioFloors.Length;
    }

    private string SettleNotice(CasinoBarkeepFinishDto result, int maxScore)
    {
        if (result.Granted && string.Equals(result.Reason, CasinoReasons.CapReached, StringComparison.Ordinal))
        {
            return Loc.T(L.Casino.ReasonCapReached);
        }

        if (!result.Granted && string.Equals(result.Reason, CasinoReasons.Expired, StringComparison.Ordinal))
        {
            return Loc.T(L.Casino.BarkeepExpired);
        }

        if (!result.Granted)
        {
            return Loc.T(CasinoReasons.MessageFor(result.Reason));
        }

        return result.Payout > 0
            ? string.Empty
            : noTipsLabel.Get(L.Casino.BarkeepNoTips, BarkeepRules.PointsForBand(0, maxScore));
    }

    private void DrawPracticeCard(ImDrawListPtr drawList, in CasinoStageFrame frame, AppSkin ui, in BarkeepScene scene,
        float scale)
    {
        drawList.AddRectFilled(frame.Full.Min, frame.Full.Max, ImGui.GetColorU32(Veil));
        var card = scene.Card;
        var pad = CardPad * scale;
        var width = card.Width - pad * 2f;
        var height = Typography.LineHeight(TextStyles.Title3) + Typography.LineHeight(TextStyles.Title1)
            + Typography.LineHeight(TextStyles.Footnote) + CardGap * 2f * scale + pad * 2f;
        var min = new Vector2(card.Min.X, MathF.Max(card.Min.Y, card.Center.Y - height * 0.5f));
        var max = new Vector2(card.Max.X, min.Y + height);
        PaintCard(drawList, min, max, scale);
        var centerX = (min.X + max.X) * 0.5f;
        var y = min.Y + pad;
        y = Line(drawList, Loc.T(L.Casino.BarkeepShiftDone), centerX, y, width, CasinoColors.InkTitle, TextStyles.Title3);
        y += CardGap * scale;
        scoreRoll.Update(practiceScore, frame.DeltaSeconds);
        var score = GameNumber.Label(scoreRoll.Display);
        var style = TextStyles.Title1;
        var size = Typography.Measure(score, style.Scale * scoreRoll.PopScale, style.Weight);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, y), score, CasinoColors.InkTitle,
            style.Scale * scoreRoll.PopScale, style.Weight);
        y += Typography.LineHeight(style) + CardGap * scale;
        var best = practiceNewBest
            ? Loc.T(L.Casino.BarkeepNewBest)
            : bestLabel.Get(L.Casino.BarkeepBestScore, stats.Get(PracticeStatsId).BestScore);
        Line(drawList, best, centerX, y, width, practiceNewBest ? Gold : CasinoColors.InkBody, TextStyles.Footnote);
    }

    private static void ShadowedCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 ink,
        in TextStyle style, float scale)
    {
        var offset = new Vector2(0f, ShadowOffset * scale);
        Typography.DrawCentered(drawList, center + offset, text, new Vector4(0f, 0f, 0f, 0.7f * ink.W), style);
        Typography.DrawCentered(drawList, center, text, ink, style);
    }

    private static void PaintCard(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(CardFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Gold with { W = 0.35f }), MathF.Max(1f, scale));
    }

    private static float Line(ImDrawListPtr drawList, string text, float centerX, float top, float width, Vector4 ink,
        in TextStyle style)
    {
        var fitted = Typography.FitText(text, width, style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink, style);
        return top + Typography.LineHeight(style);
    }

    private void DrawPointRows(ImDrawListPtr drawList, float left, float top, float width, int maxScore, int reached,
        float scale)
    {
        RefreshPointRows(maxScore);
        for (var band = 0; band < pointRows.Length; band++)
        {
            var lit = band == reached;
            var style = lit ? TextStyles.FootnoteEmphasized : TextStyles.Footnote;
            var row = Typography.FitText(pointRows[band], width, style);
            var size = Typography.Measure(row, style);
            Typography.Draw(drawList, new Vector2(left + (width - size.X) * 0.5f, top + band * LadderRowHeight * scale),
                row, lit ? Gold : CasinoColors.InkBody, style);
        }
    }

    private void RefreshPointRows(int maxScore)
    {
        if (maxScore == pointMaxScore && ReferenceEquals(pointLanguage, Loc.Current))
        {
            return;
        }

        pointMaxScore = maxScore;
        pointLanguage = Loc.Current;
        for (var band = 0; band < pointRows.Length; band++)
        {
            pointRows[band] = Loc.T(L.Casino.BarkeepLadderRow,
                GameNumber.Label(BarkeepRules.PointsForBand(band, maxScore)),
                NumberText.Group(BarkeepRules.LadderPays[band]));
        }
    }

    private void RefreshShareRows()
    {
        if (ReferenceEquals(shareLanguage, Loc.Current))
        {
            return;
        }

        shareLanguage = Loc.Current;
        for (var band = 0; band < shareRows.Length; band++)
        {
            shareRows[band] = Loc.T(L.Barkeep.LadderShare,
                GameNumber.Label(BarkeepRules.LadderRatioFloors[band] / 10),
                NumberText.Group(BarkeepRules.LadderPays[band]));
        }

        wagerHint = Loc.T(L.Casino.BarkeepWagerHint, NumberText.Group(BarkeepRules.EntryChips));
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, float scale)
    {
        var deck = frame.Deck;
        if (deck.Height <= 0f)
        {
            return;
        }

        var pad = BetComposer.Pad * scale;
        var row = new Rect(new Vector2(deck.Min.X + pad, deck.Min.Y + pad),
            new Vector2(deck.Max.X - pad, deck.Min.Y + pad + BetComposer.ActionHeight * scale));
        var actionTop = deck.Max.Y - (BetComposer.Pad + BetComposer.ActionHeight) * scale;
        var action = new Rect(new Vector2(deck.Min.X + pad, actionTop),
            new Vector2(deck.Max.X - pad, actionTop + BetComposer.ActionHeight * scale));
        if (settle is not null)
        {
            if (Button.Draw(action, Loc.T(L.Casino.BarkeepDone), ui.Ink))
            {
                FinishSettle();
            }

            return;
        }

        if (practiceSettled)
        {
            var donePill = new Rect(new Vector2(row.Center.X - row.Width * PracticePillShare * 0.5f, row.Min.Y),
                new Vector2(row.Center.X + row.Width * PracticePillShare * 0.5f, row.Max.Y));
            if (Button.Draw(donePill, Loc.T(L.Casino.BarkeepDone), ui.Ink, ButtonStyle.Gray))
            {
                FinishSettle();
            }

            if (Button.Draw(action, Loc.T(L.Casino.BarkeepPracticeAgain), ui.Ink))
            {
                StartPractice();
            }

            return;
        }

        if (shift is not null)
        {
            DrawShiftDeck(ui, row, action, scale);
            return;
        }

        DrawLobbyDeck(stage, frame, ui, row);
    }

    private void DrawShiftDeck(AppSkin ui, Rect row, Rect action, float scale)
    {
        var current = shift!;
        var drawList = ImGui.GetWindowDrawList();
        var elapsed = current.ElapsedSeconds(NowUnixSeconds());
        scoreRoll.Update(current.Score, ImGui.GetIO().DeltaTime);
        var third = row.Width / 3f;
        var inset = HudPad * scale;
        DrawHudCell(drawList, new Rect(row.Min, new Vector2(row.Min.X + third, row.Max.Y)), Loc.T(L.Casino.BarkeepScore),
            GameNumber.Label(scoreRoll.Display), inset);
        DrawHudCell(drawList, new Rect(new Vector2(row.Min.X + third, row.Min.Y), new Vector2(row.Min.X + third * 2f, row.Max.Y)),
            string.Empty, counterLabel.Get(L.Casino.BarkeepPatronCounter,
                Math.Min(current.CompletedOrders + 1, current.PatronCount), current.PatronCount), inset);
        DrawHudCell(drawList, new Rect(new Vector2(row.Min.X + third * 2f, row.Min.Y), row.Max), string.Empty,
            TimeText.Duration((int)elapsed), inset);
        if (mode == BarkeepMode.Practice)
        {
            if (Button.Draw(action, Loc.T(L.Casino.BarkeepEndPractice), ui.Ink, ButtonStyle.Tinted))
            {
                EndPractice();
            }

            return;
        }

        var canEnd = current.CanFinish(elapsed) && !finishSent;
        var label = canEnd
            ? Loc.T(L.Casino.BarkeepEndShift)
            : waitLabel.Get(L.Casino.BarkeepSettlesIn,
                Math.Max(0, (int)Math.Ceiling(BarkeepShift.FinishEarliestSeconds - elapsed)));
        if (Button.Draw(action, label, ui.Ink, ButtonStyle.Prominent, enabled: canEnd))
        {
            SendFinish();
        }
    }

    private static void DrawHudCell(ImDrawListPtr drawList, Rect cell, string caption, string value, float inset)
    {
        var width = cell.Width - inset * 2f;
        var centerX = cell.Center.X;
        if (caption.Length == 0)
        {
            var fitted = Typography.FitText(value, width, TextStyles.Headline);
            Typography.DrawCentered(drawList, cell.Center, fitted, CasinoColors.InkTitle, TextStyles.Headline);
            return;
        }

        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.Title3);
        var top = cell.Center.Y - (captionHeight + valueHeight) * 0.5f;
        Line(drawList, caption, centerX, top, width, CasinoColors.InkBody, TextStyles.Footnote);
        Line(drawList, value, centerX, top + captionHeight, width, CasinoColors.Money, TextStyles.Title3);
    }

    private void DrawLobbyDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, Rect row)
    {
        var state = store.State;
        var busy = startRequested || play.RoundInFlight;
        var pillWidth = row.Width * PracticePillShare;
        var pill = new Rect(new Vector2(row.Center.X - pillWidth * 0.5f, row.Min.Y),
            new Vector2(row.Center.X + pillWidth * 0.5f, row.Max.Y));
        if (Button.Draw(pill, Loc.T(L.Casino.BarkeepPracticeTitle), ui.Ink, ButtonStyle.Tinted, enabled: !busy))
        {
            StartPractice();
        }

        if (state is null)
        {
            return;
        }

        var sitting = state.Sitting ?? CasinoWire.NoBankroll;
        var blocked = state.StakesPaused || state.Draining || frame.Blocked;
        var enabled = !blocked && !busy;
        composer.Reset(BarkeepRules.EntryChips);
        var model = new BetComposerModel(BarkeepRules.EntryChips, BarkeepRules.EntryChips, sitting.Stack,
            L.Barkeep.StartFor, enabled, FixedAmount: true, Knob: true, Repeat: stage.RepeatPressed(), Busy: busy);
        if (composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds) != BetComposerAction.Confirm)
        {
            return;
        }

        inlineReason = string.Empty;
        StartWager();
    }

    private void FinishSettle()
    {
        settle = null;
        shift = null;
        mode = BarkeepMode.None;
        practiceSettled = false;
        settleRecorded = false;
        tips.Reset();
        scoreRoll.Snap(0);
    }

    private void ApplyCamera(ImDrawListPtr drawList, int firstVertex, Vector2 pivot, float scale)
    {
        if (punch <= 0f)
        {
            return;
        }

        var zoom = 1f + punch;
        var offset = new Vector2(jitter.Range(-1f, 1f), jitter.Range(-1f, 1f)) * punch * ShakeJitter * scale;
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = firstVertex; vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            vertex.Pos = pivot + (vertex.Pos - pivot) * zoom + offset;
        }
    }

    private void StartWager()
    {
        if (play.RoundInFlight || shift is not null)
        {
            return;
        }

        startRequested = true;
        play.StartBarkeep();
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void StartPractice()
    {
        var script = new BarkeepShift(string.Empty, BarkeepShift.PracticeScript(random), (long)NowUnixSeconds());
        BeginShift(script, BarkeepMode.Practice, GameSeed.Fresh());
        scoreRoll.Snap(0);
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
        scoreRoll.Snap(0);
        verbStage.Cancel();
        if (practiceNewBest)
        {
            CasinoSfx.Play(UiSound.LevelUp);
        }
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

    private readonly struct BarkeepScene
    {
        public readonly Rect Wall;
        public readonly Rect Shelves;
        public readonly Vector2 SignCenter;
        public readonly float SignHeight;
        public readonly Rect CounterTop;
        public readonly Rect CounterFront;
        public readonly float PatronUnit;
        public readonly Rect Work;
        public readonly Rect Card;

        private BarkeepScene(Rect wall, Rect shelves, Vector2 signCenter, float signHeight, Rect counterTop,
            Rect counterFront, float patronUnit, Rect work, Rect card)
        {
            Wall = wall;
            Shelves = shelves;
            SignCenter = signCenter;
            SignHeight = signHeight;
            CounterTop = counterTop;
            CounterFront = counterFront;
            PatronUnit = patronUnit;
            Work = work;
            Card = card;
        }

        public static BarkeepScene Compute(in CasinoStageFrame frame, float scale)
        {
            var full = frame.Full;
            var safe = frame.Safe;
            var counterY = safe.Min.Y + safe.Height * CounterShare;
            var wall = new Rect(full.Min, new Vector2(full.Max.X, counterY));
            var signHeight = safe.Height * SignShare;
            var signCenter = new Vector2(safe.Center.X, safe.Min.Y + signHeight * 0.5f);
            var shelvesTop = safe.Min.Y + signHeight;
            var shelves = new Rect(new Vector2(full.Min.X, shelvesTop),
                new Vector2(full.Max.X, shelvesTop + safe.Height * ShelfShare));
            var counterTop = new Rect(new Vector2(full.Min.X, counterY),
                new Vector2(full.Max.X, counterY + CounterTopHeight * scale));
            var counterFront = new Rect(new Vector2(full.Min.X, counterTop.Max.Y),
                new Vector2(full.Max.X, counterTop.Max.Y + CounterFrontHeight * scale));
            var patronUnit = (counterY - shelvesTop) * PatronShare;
            var work = new Rect(new Vector2(safe.Min.X, counterFront.Max.Y + WorkGap * scale), safe.Max);
            var card = new Rect(new Vector2(safe.Min.X + 8f * scale, safe.Min.Y), new Vector2(safe.Max.X - 8f * scale,
                safe.Max.Y));
            return new BarkeepScene(wall, shelves, signCenter, signHeight, counterTop, counterFront, patronUnit, work, card);
        }
    }
}

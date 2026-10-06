using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class SlotsCabinet
{
    private const float PadX = 16f;
    private const float InfoRowHeight = 48f;
    private const float BannerHeight = 62f;
    private const float SpinPillHeight = Button.LargeHeight;
    private const float TurboWidth = 62f;
    private const long ResultHoldMilliseconds = 10000;
    private const float AutoWidth = 62f;
    private const float AutoChipHeight = Button.RegularHeight;
    private const float AutoChipGap = 6f;

    private const float SpinRowsPerSecond = 15f;
    private const float AnticipationRowsPerSecond = 5.5f;
    private const float LineTraceCycleSeconds = 0.7f;
    private const long SmallCelebrationMultiple = 4;
    private const string BonusBannerMarqueeId = "casino.slots.banner.bonus";
    private const string BonusSubMarqueeId = "casino.slots.banner.bonusSub";
    private const string JackpotTitleMarqueeId = "casino.slots.banner.jackpot";
    private const string JackpotAmountMarqueeId = "casino.slots.banner.jackpotAmount";
    private const string BigWinMarqueeId = "casino.slots.banner.bigWin";
    private const string CapNoteMarqueeId = "casino.slots.banner.capNote";
    private const float JackpotShakeSeconds = 0.55f;
    private const float JackpotShakeAmplitude = 5f;
    private const float JackpotFlashSeconds = 0.7f;
    private const float JackpotBulbSpacing = 22f;
    private const float JackpotChaseStepsPerSecond = 10f;

    private static readonly int[] AutoRounds = { 10, 25, 50, 100 };

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);
    private static readonly Vector4 FeltTop = new(0.049f, 0.094f, 0.075f, 1f);
    private static readonly Vector4 FeltBottom = new(0.024f, 0.051f, 0.040f, 1f);
    private static readonly int[] DecorativeCycle = { 4, 0, 5, 1, 6, 2, 7, 3 };

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private readonly CasinoStore store;
    private readonly CasinoPlayStore play;
    private readonly Action openCashier;
    private readonly SlotsRoundPlayback playback = new();
    private readonly ParticleSystem particles = new(256);
    private readonly SlotsPayTableSheet payTable = new();
    private readonly BetComposer composer = new("##slotsStake");
    private readonly int[] restingGrid = new int[SlotsRules.CellCount];

    private RollingValue winRoll;
    private RollingValue jackpotRoll;
    private string inlineReason = string.Empty;
    private float lineTraceSeconds;
    private float jackpotFlourishSeconds;
    private int celebratedSpinIndex = -1;
    private int soundedReelStops;
    private int autoRemaining;
    private int autoSettledSpin = -1;
    private bool resultTracked;
    private long resultNet;
    private long resultTick;
    private bool autoPickerOpen;
    private bool turbo;
    private Rect reelWindow;

    public SlotsCabinet(CasinoStore store, CasinoPlayStore play, Action openCashier)
    {
        this.store = store;
        this.play = play;
        this.openCashier = openCashier;
        for (var cellIndex = 0; cellIndex < SlotsRules.CellCount; cellIndex++)
        {
            restingGrid[cellIndex] = DecorativeCycle[cellIndex % DecorativeCycle.Length];
        }

        composer.Reset(SlotsRules.DefaultStake);
    }

    public long CurrentStake => composer.Amount;

    public bool PayTableOpen => payTable.IsOpen;

    public void OpenPayTable()
    {
        payTable.Open();
    }

    public void ClosePayTable()
    {
        payTable.Close();
    }

    public void Gate()
    {
        payTable.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        payTable.Draw(screen, ui, CurrentStake);
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        play.RecoverPendingRound();
    }

    public void Reset()
    {
        playback.Reset();
        particles.Clear();
        payTable.Close();
        inlineReason = string.Empty;
        celebratedSpinIndex = -1;
        autoRemaining = 0;
        autoSettledSpin = -1;
        autoPickerOpen = false;
        winRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        ConsumeResults();
        playback.Update(delta);
        TrackResult();
        SoundReelStops();
        particles.Update(delta);
        lineTraceSeconds += delta;
        CelebrateSettledSpin();

        var state = store.State;
        if (state is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var y = body.Min.Y + Metrics.Space.Sm * scale;

        y = DrawInfoRow(drawList, ui, state, left, y, width, scale, delta);
        y += Metrics.Space.Sm * scale;

        var cellWidth = (width - 12f * scale) / SlotsRules.ReelCount;
        var cellHeight = MathF.Min(cellWidth * 1.02f, 82f * scale);
        var frameHeight = cellHeight * SlotsRules.RowCount + 12f * scale;
        var frame = new Rect(new Vector2(left, y), new Vector2(left + width, y + frameHeight));
        var jackpotFlourish = playback.JackpotLanded && ShowingJackpotBanner;
        if (jackpotFlourish)
        {
            jackpotFlourishSeconds += delta;
            frame = frame.Translate(JackpotShake(jackpotFlourishSeconds, scale));
        }

        reelWindow = frame;
        DrawReels(drawList, ui, frame, scale, jackpotFlourish ? jackpotFlourishSeconds : -1f);
        y = frame.Max.Y + Metrics.Space.Sm * scale;

        y = DrawBanner(drawList, ui, left, y, width, scale, delta);
        y += Metrics.Space.Sm * scale;

        var sitting = state.Sitting;
        var slotsSeated = sitting is not null;
        if (!slotsSeated)
        {
            StopAuto();
            DrawSeatMissing(drawList, ui, left, y, width, scale);
        }
        else
        {
            AdvanceAuto(state, sitting!);
            y = DrawStakeRow(drawList, ui, sitting!, left, y, width, scale, delta);
            y += Metrics.Space.Md * scale;
            DrawSpinControls(drawList, ui, state, sitting!, left, y, width, scale);
        }

        particles.Draw(drawList, scale);
    }

    private void ConsumeResults()
    {
        var result = play.TakeSpinResult();
        if (result is not null)
        {
            if (result.Granted && playback.Begin(result))
            {
                inlineReason = string.Empty;
                winRoll.Snap(0);
                celebratedSpinIndex = -1;
                soundedReelStops = 0;
                UiFeedback.Play(UiSound.CasinoChips);
            }
            else
            {
                inlineReason = result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable;
            }
        }

        if (play.TakeRoundFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }
    }

    private void SoundReelStops()
    {
        if (playback.Phase != SlotsPlaybackPhase.Spinning)
        {
            if (playback.Phase == SlotsPlaybackPhase.Presenting && soundedReelStops > 0
                && soundedReelStops < SlotsRules.ReelCount)
            {
                UiFeedback.Play(UiSound.GameTick);
            }

            soundedReelStops = playback.Phase == SlotsPlaybackPhase.Presenting ? SlotsRules.ReelCount : 0;
            return;
        }

        var choreography = playback.Choreography;
        var stopped = 0;
        while (stopped < SlotsRules.ReelCount && choreography.ReelStopped(stopped))
        {
            stopped++;
        }

        if (stopped > soundedReelStops)
        {
            UiFeedback.Play(UiSound.GameTick);
        }

        soundedReelStops = stopped;
    }

    public bool TryRecentResult(out long net)
    {
        net = resultNet;
        return resultTick != 0 && Environment.TickCount64 - resultTick < ResultHoldMilliseconds;
    }

    private void TrackResult()
    {
        if (playback.Phase != SlotsPlaybackPhase.Finished)
        {
            resultTracked = false;
            return;
        }

        if (resultTracked)
        {
            return;
        }

        resultTracked = true;
        resultNet = playback.TotalWin - playback.Stake;
        resultTick = Environment.TickCount64;
    }

    private void CelebrateSettledSpin()
    {
        if (playback.Phase != SlotsPlaybackPhase.Presenting || celebratedSpinIndex == playback.SpinIndex)
        {
            return;
        }

        celebratedSpinIndex = playback.SpinIndex;
        lineTraceSeconds = 0f;
        var spin = playback.CurrentSpin;
        Array.Copy(spin.Grid, restingGrid, SlotsRules.CellCount);
        if (spin.Win <= 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var origin = new Vector2(reelWindow.Center.X, reelWindow.Min.Y + reelWindow.Height * 0.3f);
        if (playback.JackpotLanded && playback.SpinIndex == 0)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            jackpotFlourishSeconds = 0f;
            particles.Confetti(origin, 160, ConfettiPalette, 420f * scale, 6f, 2.4f);
            particles.Sparkle(origin, 48, Gold, 260f * scale, 5f, 1.6f);
            return;
        }

        if (spin.Win >= playback.Stake * SlotsRoundPlayback.BigWinMultiple)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            particles.Confetti(origin, 90, ConfettiPalette, 330f * scale, 5f, 1.6f);
            particles.Sparkle(origin, 24, Gold, 190f * scale, 4f, 1.0f);
        }
        else if (spin.Win >= playback.Stake * SmallCelebrationMultiple)
        {
            UiFeedback.Play(UiSound.GameWin);
            particles.Confetti(origin, 36, ConfettiPalette, 250f * scale, 4f, 1.2f);
        }
        else
        {
            UiFeedback.Play(UiSound.GameMatch);
            particles.Sparkle(origin, 10, Gold, 130f * scale, 3f, 0.8f);
        }
    }

    private float DrawInfoRow(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, float left, float y,
        float width, float scale, float delta)
    {
        var height = InfoRowHeight * scale;
        var pillWidth = width * 0.46f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + pillWidth, y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.25f)), Metrics.Stroke.Hairline);

        var label = Loc.T(L.Casino.SlotsChips);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, y + 7f * scale), label, ui.MutedInk,
            TextStyles.Caption1);
        var stackText = NumberText.Group(DisplayStack(state));
        CurrencyGlyph.DrawAmount(drawList, new Vector2(min.X + 16f * scale, y + 21f * scale), stackText,
            CurrencyKind.Chips, ui.TitleInk, TextStyles.SubheadlineEmphasized);

        if (playback.InBonus && playback.Phase != SlotsPlaybackPhase.Finished)
        {
            var counter = Loc.T(L.Casino.SlotsFreeSpinCounter, GameNumber.Label(playback.SpinIndex),
                GameNumber.Label(playback.FreeSpinCount));
            var counterSize = Typography.Measure(counter, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(left + width - counterSize.X, y + 9f * scale), counter, Gold,
                TextStyles.FootnoteEmphasized);
            if (playback.Phase == SlotsPlaybackPhase.Presenting && playback.CurrentSpin.SpinsAdded > 0)
            {
                var extra = Loc.T(L.Casino.SlotsExtraSpins, GameNumber.Label(playback.CurrentSpin.SpinsAdded));
                var extraSize = Typography.Measure(extra, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(left + width - extraSize.X, y + 27f * scale), extra, Gold,
                    TextStyles.Caption1);
            }

            return y + height;
        }

        DrawJackpotPill(drawList, ui, state.Jackpot, left + width, y, width * 0.50f, height, scale, delta);
        return y + height;
    }

    private void DrawJackpotPill(ImDrawListPtr drawList, AppSkin ui, long jackpot, float right, float y,
        float pillWidth, float height, float scale, float delta)
    {
        if (jackpot <= 0)
        {
            return;
        }

        var max = new Vector2(right, y + height);
        var min = new Vector2(right - pillWidth, y);
        var rounding = height * 0.5f;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.12f)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.32f + 0.22f * Pulse.Wave(Pulse.Breath))),
            Metrics.Stroke.Hairline);

        var label = Loc.T(L.Casino.JackpotEyebrow);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, y + 7f * scale), label,
            Palette.WithAlpha(Gold, 0.85f), TextStyles.Caption1);

        jackpotRoll.Update((int)CasinoChipLots.CoinsFor(jackpot), delta);
        var amount = NumberText.Group(jackpotRoll.Display);
        var amountHeight = Typography.Measure(amount, TextStyles.SubheadlineEmphasized).Y;
        var reserve = CurrencyGlyph.Reserve(amountHeight);
        var fitted = Typography.FitText(amount, pillWidth - 32f * scale - reserve,
            TextStyles.SubheadlineEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(min.X + 16f * scale, y + 21f * scale), fitted,
            CurrencyKind.Coins, Gold, TextStyles.SubheadlineEmphasized);
    }

    private long DisplayStack(CasinoStateDto state)
    {
        var stack = state.Sitting?.Stack ?? 0;
        var replaying = playback.Phase == SlotsPlaybackPhase.Spinning
            || playback.Phase == SlotsPlaybackPhase.Presenting
            || playback.Phase == SlotsPlaybackPhase.BonusIntro;
        if (!replaying)
        {
            return stack;
        }

        return Math.Max(0, stack - (playback.TotalWin - playback.CommittedWin));
    }

    private void DrawReels(ImDrawListPtr drawList, AppSkin ui, Rect frame, float scale, float jackpotSeconds)
    {
        var rounding = Metrics.Radius.Card * scale;
        var jackpotGlow = jackpotSeconds >= 0f ? JackpotGlow(jackpotSeconds) : 0f;
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, ImGui.GetColorU32(FeltTop),
            ImGui.GetColorU32(FeltBottom));
        var strokeInk = jackpotGlow > 0f
            ? Gold with { W = 0.45f + 0.45f * jackpotGlow }
            : Palette.WithAlpha(ui.Accent, 0.28f);
        Squircle.Stroke(drawList, frame.Min, frame.Max, rounding, ImGui.GetColorU32(strokeInk),
            Metrics.Stroke.Thin * scale);

        var inset = 6f * scale;
        var inner = new Rect(frame.Min + new Vector2(inset, inset), frame.Max - new Vector2(inset, inset));
        if (jackpotGlow > 0f)
        {
            drawList.AddRectFilled(inner.Min, inner.Max, ImGui.GetColorU32(Gold with { W = 0.45f * jackpotGlow }),
                rounding * 0.6f);
        }

        var cellWidth = inner.Width / SlotsRules.ReelCount;
        var cellHeight = inner.Height / SlotsRules.RowCount;
        var choreography = playback.Choreography;
        var spinningPhase = playback.Phase == SlotsPlaybackPhase.Spinning;
        var grid = spinningPhase || playback.Phase == SlotsPlaybackPhase.Presenting ? playback.CurrentSpin.Grid
            : restingGrid;

        Span<bool> winningCells = stackalloc bool[SlotsRules.CellCount];
        var presenting = PresentingWins();
        if (presenting)
        {
            MarkWinningCells(playback.CurrentSpin, winningCells);
        }

        for (var reel = 0; reel < SlotsRules.ReelCount; reel++)
        {
            var reelMin = new Vector2(inner.Min.X + reel * cellWidth, inner.Min.Y);
            var reelMax = new Vector2(reelMin.X + cellWidth, inner.Max.Y);
            if (reel > 0)
            {
                drawList.AddLine(new Vector2(reelMin.X, inner.Min.Y + 4f * scale),
                    new Vector2(reelMin.X, inner.Max.Y - 4f * scale),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.07f)), 1f * scale);
            }

            drawList.PushClipRect(reelMin, reelMax, true);
            if (!spinningPhase || choreography.ReelStopped(reel))
            {
                DrawRestedReel(drawList, grid, reel, reelMin, cellWidth, cellHeight, presenting, winningCells,
                    jackpotGlow);
            }
            else
            {
                var landing = choreography.LandingProgress(reel);
                if (landing <= 0f)
                {
                    DrawSpinningReel(drawList, choreography, reel, reelMin, cellWidth, cellHeight);
                }
                else
                {
                    DrawLandingReel(drawList, playback.CurrentSpin.Grid, reel, reelMin, cellWidth, cellHeight,
                        landing);
                }
            }

            drawList.PopClipRect();
        }

        if (choreography.Anticipating && spinningPhase)
        {
            var lastMin = new Vector2(inner.Min.X + (SlotsRules.ReelCount - 1) * cellWidth, inner.Min.Y);
            var pulse = 0.24f + 0.14f * MathF.Sin(choreography.ElapsedSeconds * 6f);
            Squircle.Stroke(drawList, lastMin, inner.Max, 8f * scale,
                ImGui.GetColorU32(Gold with { W = pulse }), 2f * scale);
        }

        if (presenting)
        {
            DrawLineTraces(drawList, playback.CurrentSpin, inner.Min, cellWidth, cellHeight, scale);
        }

        if (jackpotSeconds >= 0f)
        {
            DrawJackpotChase(drawList, frame, rounding, scale, jackpotSeconds);
        }
    }

    private static Vector2 JackpotShake(float seconds, float scale)
    {
        if (seconds >= JackpotShakeSeconds)
        {
            return Vector2.Zero;
        }

        var envelope = 1f - seconds / JackpotShakeSeconds;
        var amplitude = JackpotShakeAmplitude * scale * envelope * envelope;
        return new Vector2(MathF.Sin(seconds * 70f) * amplitude, MathF.Cos(seconds * 53f) * amplitude * 0.6f);
    }

    private static float JackpotGlow(float seconds)
    {
        var flash = MathF.Max(0f, 1f - seconds / JackpotFlashSeconds);
        return MathF.Max(flash * flash, 0.35f + 0.15f * MathF.Sin(seconds * 4f));
    }

    private static void DrawJackpotChase(ImDrawListPtr drawList, Rect frame, float rounding, float scale,
        float seconds)
    {
        var spacing = JackpotBulbSpacing * scale;
        var phase = (int)(seconds * JackpotChaseStepsPerSecond);
        var allLit = seconds < JackpotFlashSeconds * 0.5f;
        var halo = ImGui.GetColorU32(Gold with { W = 0.30f });
        var lit = ImGui.GetColorU32(Gold);
        var dim = ImGui.GetColorU32(Gold with { W = 0.28f });
        var bulbIndex = 0;
        DrawBulbEdge(drawList, new Vector2(frame.Min.X + rounding, frame.Min.Y),
            new Vector2(frame.Max.X - rounding, frame.Min.Y), spacing, scale, phase, allLit, halo, lit, dim,
            ref bulbIndex);
        DrawBulbEdge(drawList, new Vector2(frame.Max.X, frame.Min.Y + rounding),
            new Vector2(frame.Max.X, frame.Max.Y - rounding), spacing, scale, phase, allLit, halo, lit, dim,
            ref bulbIndex);
        DrawBulbEdge(drawList, new Vector2(frame.Max.X - rounding, frame.Max.Y),
            new Vector2(frame.Min.X + rounding, frame.Max.Y), spacing, scale, phase, allLit, halo, lit, dim,
            ref bulbIndex);
        DrawBulbEdge(drawList, new Vector2(frame.Min.X, frame.Max.Y - rounding),
            new Vector2(frame.Min.X, frame.Min.Y + rounding), spacing, scale, phase, allLit, halo, lit, dim,
            ref bulbIndex);
    }

    private static void DrawBulbEdge(ImDrawListPtr drawList, Vector2 start, Vector2 end, float spacing,
        float scale, int phase, bool allLit, uint halo, uint lit, uint dim, ref int bulbIndex)
    {
        var length = Vector2.Distance(start, end);
        if (length < spacing * 0.5f)
        {
            return;
        }

        var count = Math.Max(1, (int)(length / spacing));
        var direction = (end - start) / length;
        var step = length / count;
        for (var bulb = 0; bulb < count; bulb++)
        {
            var center = start + direction * ((bulb + 0.5f) * step);
            if (allLit || (bulbIndex + phase) % 3 == 0)
            {
                drawList.AddCircleFilled(center, 6f * scale, halo, 12);
                drawList.AddCircleFilled(center, 2.6f * scale, lit, 12);
            }
            else
            {
                drawList.AddCircleFilled(center, 2f * scale, dim, 10);
            }

            bulbIndex++;
        }
    }

    private bool PresentingWins()
    {
        return playback.HasSpins
            && (playback.Phase == SlotsPlaybackPhase.Presenting
                || playback.Phase == SlotsPlaybackPhase.BonusIntro
                || playback.Phase == SlotsPlaybackPhase.Finished);
    }

    private static void MarkWinningCells(in SlotsSpinView spin, Span<bool> winningCells)
    {
        for (var winIndex = 0; winIndex < spin.LineWins.Length; winIndex++)
        {
            var win = spin.LineWins[winIndex];
            if (win.Line < 0 || win.Line >= SlotsRules.PaylineCount)
            {
                continue;
            }

            var rows = SlotsRules.Paylines[win.Line];
            var count = Math.Min(win.Count, SlotsRules.ReelCount);
            for (var reel = 0; reel < count; reel++)
            {
                winningCells[reel * SlotsRules.RowCount + rows[reel]] = true;
            }
        }

        if (spin.ScatterCount >= 3)
        {
            for (var cellIndex = 0; cellIndex < SlotsRules.CellCount; cellIndex++)
            {
                if (spin.Grid[cellIndex] == SlotsRules.ScatterSymbol)
                {
                    winningCells[cellIndex] = true;
                }
            }
        }
    }

    private void DrawRestedReel(ImDrawListPtr drawList, int[] grid, int reel, Vector2 reelMin, float cellWidth,
        float cellHeight, bool presenting, ReadOnlySpan<bool> winningCells, float jackpotGlow)
    {
        var hasWins = presenting && playback.HasSpins
            && (playback.CurrentSpin.LineWins.Length > 0 || playback.CurrentSpin.ScatterCount >= 3);
        var extent = MathF.Min(cellWidth, cellHeight) * 0.32f;
        for (var row = 0; row < SlotsRules.RowCount; row++)
        {
            var cellIndex = reel * SlotsRules.RowCount + row;
            var center = new Vector2(reelMin.X + cellWidth * 0.5f, reelMin.Y + (row + 0.5f) * cellHeight);
            var winning = hasWins && winningCells[cellIndex];
            if (jackpotGlow > 0f)
            {
                drawList.AddCircleFilled(center, MathF.Min(cellWidth, cellHeight) * 0.46f,
                    ImGui.GetColorU32(Gold with { W = 0.06f + 0.22f * jackpotGlow }), 24);
            }
            else if (winning)
            {
                var glow = 0.10f + 0.06f * MathF.Sin(lineTraceSeconds * 5f);
                drawList.AddCircleFilled(center, MathF.Min(cellWidth, cellHeight) * 0.46f,
                    ImGui.GetColorU32(Gold with { W = glow }), 24);
            }

            var alpha = hasWins && !winning && jackpotGlow <= 0f ? 0.4f : 1f;
            SlotsSymbolArt.Draw(drawList, grid[cellIndex], center, extent, alpha);
        }
    }

    private static void DrawSpinningReel(ImDrawListPtr drawList, SlotsChoreography choreography, int reel,
        Vector2 reelMin, float cellWidth, float cellHeight)
    {
        var rows = ScrollRows(choreography, reel);
        var wholeRows = (int)rows;
        var fraction = rows - wholeRows;
        var extent = MathF.Min(cellWidth, cellHeight) * 0.32f;
        for (var slot = -1; slot <= SlotsRules.RowCount; slot++)
        {
            var symbol = DecorativeCycle[Mod(wholeRows + slot + reel * 3, DecorativeCycle.Length)];
            var centerY = reelMin.Y + (slot + fraction - 0.5f + 1f) * cellHeight;
            var center = new Vector2(reelMin.X + cellWidth * 0.5f, centerY);
            SlotsSymbolArt.Draw(drawList, symbol, center with { Y = center.Y - cellHeight * 0.22f }, extent, 0.14f);
            SlotsSymbolArt.Draw(drawList, symbol, center, extent, 0.55f);
        }
    }

    private static void DrawLandingReel(ImDrawListPtr drawList, int[] grid, int reel, Vector2 reelMin,
        float cellWidth, float cellHeight, float landing)
    {
        var eased = Easing.EaseOutCubic(landing);
        var offset = (eased - 1f) * SlotsRules.RowCount * cellHeight;
        var extent = MathF.Min(cellWidth, cellHeight) * 0.32f;
        for (var row = 0; row < SlotsRules.RowCount; row++)
        {
            var center = new Vector2(reelMin.X + cellWidth * 0.5f,
                reelMin.Y + (row + 0.5f) * cellHeight + offset);
            SlotsSymbolArt.Draw(drawList, grid[reel * SlotsRules.RowCount + row], center, extent);
        }

        for (var trailingRow = 0; trailingRow < SlotsRules.RowCount; trailingRow++)
        {
            var symbol = DecorativeCycle[Mod(trailingRow + reel * 3, DecorativeCycle.Length)];
            var center = new Vector2(reelMin.X + cellWidth * 0.5f,
                reelMin.Y + (SlotsRules.RowCount + trailingRow + 0.5f) * cellHeight + offset);
            SlotsSymbolArt.Draw(drawList, symbol, center, extent, 0.55f);
        }
    }

    private static float ScrollRows(SlotsChoreography choreography, int reel)
    {
        if (!choreography.HoldsLastReel || reel != SlotsRules.ReelCount - 1)
        {
            return choreography.ElapsedSeconds * SpinRowsPerSecond;
        }

        var slowFrom = choreography.StopSeconds(SlotsRules.ReelCount - 2);
        if (choreography.ElapsedSeconds <= slowFrom)
        {
            return choreography.ElapsedSeconds * SpinRowsPerSecond;
        }

        return slowFrom * SpinRowsPerSecond
            + (choreography.ElapsedSeconds - slowFrom) * AnticipationRowsPerSecond;
    }

    private static int Mod(int value, int length)
    {
        var remainder = value % length;
        return remainder < 0 ? remainder + length : remainder;
    }

    private void DrawLineTraces(ImDrawListPtr drawList, in SlotsSpinView spin, Vector2 innerMin, float cellWidth,
        float cellHeight, float scale)
    {
        if (spin.LineWins.Length == 0)
        {
            return;
        }

        var activeIndex = (int)(lineTraceSeconds / LineTraceCycleSeconds) % spin.LineWins.Length;
        var win = spin.LineWins[activeIndex];
        if (win.Line < 0 || win.Line >= SlotsRules.PaylineCount)
        {
            return;
        }

        var rows = SlotsRules.Paylines[win.Line];
        var count = Math.Min(win.Count, SlotsRules.ReelCount);
        if (count < 2)
        {
            return;
        }

        Span<Vector2> points = stackalloc Vector2[SlotsRules.ReelCount];
        for (var reel = 0; reel < count; reel++)
        {
            points[reel] = new Vector2(innerMin.X + (reel + 0.5f) * cellWidth,
                innerMin.Y + (rows[reel] + 0.5f) * cellHeight);
        }

        var glow = ImGui.GetColorU32(Gold with { W = 0.22f });
        var core = ImGui.GetColorU32(Gold);
        for (var segment = 0; segment < count - 1; segment++)
        {
            drawList.AddLine(points[segment], points[segment + 1], glow, 7f * scale);
            drawList.AddLine(points[segment], points[segment + 1], core, 2.4f * scale);
        }

        for (var reel = 0; reel < count; reel++)
        {
            drawList.AddCircleFilled(points[reel], 3.2f * scale, core, 12);
        }
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale,
        float delta)
    {
        var height = BannerHeight * scale;
        var center = new Vector2(left + width * 0.5f, y + height * 0.5f);
        if (playback.Phase == SlotsPlaybackPhase.BonusIntro)
        {
            var banner = Loc.T(L.Casino.SlotsFreeSpinsBanner, GameNumber.Label(playback.BonusAwarded));
            DrawBannerLine(drawList, BonusBannerMarqueeId, banner, center.X, center.Y - 10f * scale, width,
                TextStyles.Title2, Gold);
            DrawBannerLine(drawList, BonusSubMarqueeId, Loc.T(L.Casino.SlotsBonusSub), center.X,
                center.Y + 16f * scale, width, TextStyles.Footnote, ui.MutedInk);
            DrawSkip(drawList, ui, left, y, width, scale);
            return y + height;
        }

        if (playback.JackpotLanded && ShowingJackpotBanner)
        {
            var pulse = 0.78f + 0.22f * Pulse.Wave(Pulse.Fast);
            DrawBannerLine(drawList, JackpotTitleMarqueeId, Loc.T(L.Casino.JackpotWon), center.X,
                center.Y - 16f * scale, width, TextStyles.Title1, Palette.WithAlpha(Gold, pulse));
            var coins = NumberText.Group(CasinoChipLots.CoinsFor(playback.Jackpot));
            DrawBannerLine(drawList, JackpotAmountMarqueeId, Loc.T(L.Casino.JackpotWonAmount, coins), center.X,
                center.Y + 16f * scale, width, TextStyles.SubheadlineEmphasized, Gold);
            DrawSkip(drawList, ui, left, y, width, scale);
            return y + height;
        }

        winRoll.Update((int)playback.CommittedWin, delta, CountUpSpeedFor(playback.CommittedWin));
        if (playback.Phase != SlotsPlaybackPhase.Idle && winRoll.Display > 0)
        {
            var bigWin = playback.CommittedWin >= playback.Stake * SlotsRoundPlayback.BigWinMultiple;
            if (bigWin)
            {
                DrawBannerLine(drawList, BigWinMarqueeId, Loc.T(L.Casino.SlotsBigWin), center.X,
                    center.Y - 20f * scale, width, TextStyles.FootnoteEmphasized, Gold);
            }

            var amount = "+" + NumberText.Group((long)winRoll.Display);
            Typography.DrawCentered(drawList, center with { Y = center.Y + (bigWin ? 4f : -4f) * scale }, amount,
                Gold, TextStyles.Title1.Scale * winRoll.PopScale, TextStyles.Title1.Weight);
            if (playback.CapApplied && playback.Phase == SlotsPlaybackPhase.Finished)
            {
                DrawBannerLine(drawList, CapNoteMarqueeId,
                    Loc.T(L.Casino.SlotsCapNote, SlotsRules.PayoutCapMultiple.ToString(Loc.Culture)), center.X,
                    center.Y + 24f * scale, width, TextStyles.Caption1, ui.MutedInk);
            }
        }

        DrawSkip(drawList, ui, left, y, width, scale);
        return y + height;
    }

    private static void DrawBannerLine(ImDrawListPtr drawList, MarqueeId id, string text, float centerX,
        float centerY, float maxWidth, in TextStyle style, Vector4 color)
    {
        var top = centerY - Typography.Measure(text, style).Y * 0.5f;
        Marquee.DrawCenteredAuto(drawList, id, text, centerX, top, maxWidth, style, color);
    }

    private void DrawSkip(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale)
    {
        if (!playback.SkipAvailable)
        {
            return;
        }

        var label = Loc.T(L.Casino.SlotsSkip);
        var chipHeight = Button.SmallHeight * scale;
        var chipMax = new Vector2(left + width, y + chipHeight);
        var chipMin = new Vector2(chipMax.X - Button.WidthFor(label, ButtonSize.Small), y);
        if (Button.Draw(drawList, new Rect(chipMin, chipMax), label, ui.Ink, ButtonStyle.Gray, id: "casino.slots.skip"))
        {
            playback.Skip();
            winRoll.Snap((int)playback.TotalWin);
            Array.Copy(playback.CurrentSpin.Grid, restingGrid, SlotsRules.CellCount);
            celebratedSpinIndex = playback.SpinIndex;
        }
    }

    private float DrawStakeRow(ImDrawListPtr drawList, AppSkin ui, CasinoSittingDto sitting, float left, float y,
        float width, float scale, float delta)
    {
        var label = Loc.T(L.Casino.SlotsStake);
        Typography.Draw(drawList, new Vector2(left, y), label, ui.MutedInk, TextStyles.FootnoteEmphasized);
        y += 20f * scale;
        var changeable = !play.RoundInFlight && !PlaybackBusy;
        var bounds = new Rect(new Vector2(left, y),
            new Vector2(left + width, y + BetComposer.AmountHeightFor(scale)));
        return composer.DrawAmount(ui, bounds, SlotsRules.MinStake, SlotsRules.MaxStake, sitting.Stack,
            SlotsRules.StakeStep, changeable, delta);
    }

    public bool AutoRunning => autoRemaining > 0;

    private void StopAuto()
    {
        autoRemaining = 0;
        autoPickerOpen = false;
    }

    private void AdvanceAuto(CasinoStateDto state, CasinoSittingDto sitting)
    {
        if (autoRemaining <= 0)
        {
            return;
        }

        if (inlineReason.Length > 0 || state.StakesPaused || state.Draining)
        {
            StopAuto();
            return;
        }

        if (play.RoundInFlight || PlaybackBusy)
        {
            return;
        }

        if (playback.Phase == SlotsPlaybackPhase.Finished && autoSettledSpin != playback.SpinIndex)
        {
            autoSettledSpin = playback.SpinIndex;
            if (StopsAutoAfterRound())
            {
                StopAuto();
                return;
            }
        }

        var stake = CurrentStake;
        if (stake < SlotsRules.MinStake || sitting.Stack < stake
            || (state.LossHeadroom > 0 && state.LossHeadroom < stake))
        {
            StopAuto();
            return;
        }

        autoRemaining--;
        autoSettledSpin = -1;
        play.SpinSlots(stake);
    }

    private bool StopsAutoAfterRound()
    {
        if (playback.JackpotLanded || playback.BonusAwarded > 0)
        {
            return true;
        }

        var stake = playback.Stake;
        return stake > 0 && playback.TotalWin >= stake * SlotsRoundPlayback.BigWinMultiple;
    }

    private bool ShowingJackpotBanner => playback.Phase == SlotsPlaybackPhase.Finished
        || (playback.SpinIndex == 0 && playback.Phase == SlotsPlaybackPhase.Presenting);

    private bool PlaybackBusy => playback.Phase == SlotsPlaybackPhase.Spinning
        || playback.Phase == SlotsPlaybackPhase.Presenting
        || playback.Phase == SlotsPlaybackPhase.BonusIntro;

    private void DrawSpinControls(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto sitting, float left, float y, float width, float scale)
    {
        if (inlineReason.Length > 0)
        {
            y = DrawReasonCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), left, y, width, scale);
            y += Metrics.Space.Sm * scale;
        }

        var stake = CurrentStake;
        var lowStack = stake < SlotsRules.MinStake || sitting.Stack < stake;
        var blocked = state.StakesPaused || state.Draining;
        var canSpin = !play.RoundInFlight && !PlaybackBusy && !blocked && !lowStack;
        var running = AutoRunning;
        var label = running
            ? Loc.T(L.Casino.SlotsAutoStop, GameNumber.Label(autoRemaining))
            : Loc.T(L.Casino.SlotsSpin);
        var sideWidth = (TurboWidth + AutoWidth + Metrics.Space.Xs) * scale;
        var pillRect = new Rect(new Vector2(left, y),
            new Vector2(left + width - sideWidth - Metrics.Space.Sm * scale, y + SpinPillHeight * scale));
        if (ui.ActionPill(pillRect, label, running || canSpin, TextStyles.Headline))
        {
            if (running)
            {
                StopAuto();
            }
            else
            {
                inlineReason = string.Empty;
                play.SpinSlots(stake);
            }
        }

        var autoRect = new Rect(new Vector2(pillRect.Max.X + Metrics.Space.Sm * scale, y),
            new Vector2(pillRect.Max.X + Metrics.Space.Sm * scale + AutoWidth * scale,
                y + SpinPillHeight * scale));
        if (DrawAutoToggle(drawList, ui, autoRect, scale))
        {
            if (running)
            {
                StopAuto();
            }
            else
            {
                autoPickerOpen = !autoPickerOpen;
            }
        }

        var turboRect = new Rect(new Vector2(autoRect.Max.X + Metrics.Space.Xs * scale, y),
            new Vector2(left + width, y + SpinPillHeight * scale));
        if (DrawTurboToggle(drawList, ui, turboRect, scale))
        {
            turbo = !turbo;
            playback.Turbo = turbo;
        }

        if (turbo && !running && canSpin && UiInteract.Hover(pillRect.Min, pillRect.Max)
            && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            inlineReason = string.Empty;
            play.SpinSlots(stake);
        }

        y = pillRect.Max.Y + Metrics.Space.Sm * scale;
        if (autoPickerOpen && !running)
        {
            y = DrawAutoPicker(drawList, ui, left, y, width, sitting, stake, scale);
        }

        if (blocked)
        {
            var notice = state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : Loc.T(L.Casino.DrainingTitle);
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 8f * scale), notice,
                ui.MutedInk, TextStyles.Footnote);
            return;
        }

        if (!lowStack || play.RoundInFlight || PlaybackBusy)
        {
            return;
        }

        var hint = Loc.T(L.Casino.SlotsLowStack);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(new Vector2(left, y), hint, ui.MutedInk, TextStyles.Footnote, width);
        y += hintBlock.Y + Metrics.Space.Sm * scale;
        var cashierLabel = Loc.T(L.Casino.Cashier);
        var cashierRect = new Rect(new Vector2(left + width * 0.25f, y),
            new Vector2(left + width * 0.75f, y + 38f * scale));
        if (ui.GhostButton(cashierRect, cashierLabel))
        {
            openCashier();
        }
    }

    private float CountUpSpeedFor(long win)
    {
        var stake = playback.Stake;
        if (stake <= 0 || win <= 0)
        {
            return 14f;
        }

        var multiple = win / stake;
        if (multiple < 2)
        {
            return 14f;
        }

        if (multiple < 10)
        {
            return 9f;
        }

        return multiple < 50 ? 5f : 3f;
    }

    private float DrawAutoPicker(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width,
        CasinoSittingDto sitting, long stake, float scale)
    {
        var gap = AutoChipGap * scale;
        var chipWidth = (width - gap * (AutoRounds.Length - 1)) / AutoRounds.Length;
        var height = AutoChipHeight * scale;
        for (var index = 0; index < AutoRounds.Length; index++)
        {
            var rounds = AutoRounds[index];
            var min = new Vector2(left + index * (chipWidth + gap), y);
            var max = new Vector2(min.X + chipWidth, y + height);
            var affordable = stake > 0 && sitting.Stack >= stake;
            if (Button.Draw(drawList, new Rect(min, max), GameNumber.Label(rounds), ui.Ink, ButtonStyle.Gray,
                    enabled: affordable))
            {
                inlineReason = string.Empty;
                autoRemaining = rounds;
                autoSettledSpin = -1;
                autoPickerOpen = false;
            }
        }

        y += height + Metrics.Space.Xs * scale;
        var note = Loc.T(L.Casino.SlotsAutoStopsOn);
        var block = Typography.MeasureWrappedBlock(note, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(new Vector2(left, y), note, ui.MutedInk, TextStyles.Caption2, width);
        return y + block.Y + Metrics.Space.Sm * scale;
    }

    private bool DrawAutoToggle(ImDrawListPtr drawList, AppSkin ui, Rect rect, float scale) =>
        DrawSideToggle(drawList, ui, rect, Loc.T(L.Casino.SlotsAuto), AutoRunning || autoPickerOpen,
            "casino.slots.auto", scale);

    private bool DrawTurboToggle(ImDrawListPtr drawList, AppSkin ui, Rect rect, float scale) =>
        DrawSideToggle(drawList, ui, rect, Loc.T(L.Casino.SlotsTurbo), turbo, "casino.slots.turbo", scale);

    private static bool DrawSideToggle(ImDrawListPtr drawList, AppSkin ui, Rect rect, string label, bool lit,
        string id, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink, lit ? ButtonStyle.Tinted : ButtonStyle.Gray,
            ButtonRole.Normal, true, hovered, ImGui.GetID(id));
        var area = face.Face;
        var maxLabelWidth = MathF.Max(1f, area.Width - Metrics.Space.Sm * scale * 2f);
        var labelHeight = Typography.Measure(label, TextStyles.FootnoteEmphasized).Y;
        Marquee.DrawCenteredAuto(drawList, id, label, area.Center.X, area.Center.Y - labelHeight * 0.5f,
            maxLabelWidth, TextStyles.FootnoteEmphasized, face.LabelInk);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static float DrawReasonCard(ImDrawListPtr drawList, AppSkin ui, string message, float left, float y,
        float width, float scale)
    {
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, width - pad * 2f);
        return max.Y;
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale)
    {
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var hint = Loc.T(L.Casino.CabinetNoChipsHint);
        var pad = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f);
        var cardHeight = titleSize.Y + hintBlock.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + cardHeight);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (ui.PillButton(pillRect, Loc.T(L.Casino.Cashier), true, true))
        {
            openCashier();
        }
    }
}

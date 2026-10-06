using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Pairs;

internal sealed class PairsApp : IMiniGame
{
    private const string GameId = "memory";
    private const string AttemptsStatId = "memory.attempts";
    private const float FlipSeconds = 0.22f;
    private const float GlowSeconds = 0.6f;
    private const float FlightSeconds = 0.5f;
    private const float FlightArc = 18f;
    private const float ShakeAmplitude = 5f;
    private const float ShakeCycles = 8f;
    private const int StreakShown = 2;
    private const int PunchStreak = 3;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private const float CapsuleSectionGap = 8f;
    private const ulong IdleSeed = 7;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Pairs, GameGenre.Brain, L.Pairs.Hook,
        Backdrop.Felt, HudStyle.Standard, ScoreKind.Time);
    private static readonly Vector4 Warm = new(1f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 WinSparkle = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4[] WinPalette =
    {
        Core.Theme.Accent.Mint, Core.Theme.Accent.Amber, Core.Theme.Accent.Pink, Core.Theme.Accent.Blue,
    };
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private readonly PairsBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] flip = new float[PairsBoard.CardCount];
    private readonly float[] glow = new float[PairsBoard.CardCount];
    private readonly float[] flight = new float[PairsBoard.CardCount];
    private GameGrid grid;
    private Rect tray;
    private ulong idleSeed = IdleSeed;
    private float entrance;
    private float shake;
    private int hoveredCard = -1;
    private bool finished;

    public PairsApp()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        idleSeed = start.Seed;
        board.Reset(start.Random);
        ClearVisuals();
        particles.Clear();
        fx.Clear();
        entrance = 0f;
        finished = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        SyncIdle(context.Session.Seed);
        var scale = UiScale.Current;
        Layout(context.Safe, scale);
        DrawBoard(ImGui.GetWindowDrawList(), 1f, scale, context.Backdrop.Ink);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rawSeconds = context.RawDeltaSeconds;
        particles.Update(rawSeconds);
        fx.Update(rawSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds);
        AdvanceVisuals(rawSeconds);
        Layout(StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale)), scale);
        if (!finished)
        {
            Step(scale, context);
        }

        DrawBoard(drawList, entrance, scale, context.Backdrop.Ink);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawHud(drawList, context.Theme, scale, context);
        context.Session.Report((int)board.Elapsed);
    }

    private void SyncIdle(ulong seed)
    {
        if (idleSeed == seed)
        {
            return;
        }

        idleSeed = seed;
        board.Reset(GameRandom.FromSeed(seed));
        ClearVisuals();
    }

    private void ClearVisuals()
    {
        Array.Clear(flip);
        Array.Clear(glow);
        Array.Clear(flight);
        shake = 0f;
        hoveredCard = -1;
    }

    private void Layout(Rect area, float scale)
    {
        var trayHeight = PairsRenderer.TrayHeight * scale;
        var trayGap = PairsRenderer.TrayGap * scale;
        var pitch = MathF.Min(area.Width / PairsBoard.Columns,
            (area.Height - trayHeight - trayGap) / PairsBoard.Rows);
        var gridWidth = pitch * PairsBoard.Columns;
        var gridHeight = pitch * PairsBoard.Rows;
        var blockTop = area.Center.Y - (gridHeight + trayHeight + trayGap) * 0.5f;
        var gridMin = new Vector2(area.Center.X - gridWidth * 0.5f, blockTop + trayHeight + trayGap);
        grid = GameGrid.Centered(new Rect(gridMin, gridMin + new Vector2(gridWidth, gridHeight)), PairsBoard.Columns,
            PairsBoard.Rows, PairsRenderer.GapFraction);
        tray = new Rect(new Vector2(grid.Origin.X, blockTop), new Vector2(grid.Origin.X + grid.Width, blockTop + trayHeight));
    }

    private void AdvanceVisuals(float deltaSeconds)
    {
        shake = MathF.Max(0f, shake - deltaSeconds / PairsBoard.MismatchSeconds);
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            var state = board.State(index);
            var target = state == CardState.FaceDown ? 0f : 1f;
            var step = deltaSeconds / FlipSeconds;
            flip[index] = target > flip[index]
                ? MathF.Min(target, flip[index] + step)
                : MathF.Max(target, flip[index] - step);
            glow[index] = MathF.Max(0f, glow[index] - deltaSeconds / GlowSeconds);
            if (state == CardState.Matched)
            {
                flight[index] = MathF.Min(1f, flight[index] + deltaSeconds / FlightSeconds);
            }
        }
    }

    private void Step(float scale, in GameContext context)
    {
        board.Step(context.DeltaSeconds);
        if (board.MatchedThisStep)
        {
            OnMatch(scale, context);
        }

        if (board.MismatchedThisStep)
        {
            OnMismatch();
        }

        if (board.Over)
        {
            OnWin(scale, context);
            return;
        }

        hoveredCard = -1;
        if (context.Session.State != StageFlow.Playing)
        {
            return;
        }

        hoveredCard = HoveredCard();
        if (hoveredCard < 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !board.Reveal(hoveredCard))
        {
            return;
        }

        UiFeedback.Play(UiSound.GameCardFlip);
    }

    private int HoveredCard()
    {
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            if (!board.CanReveal(index) || flip[index] > 0.02f)
            {
                continue;
            }

            var cell = CellOf(index);
            if (UiInteract.Hover(cell.Min, cell.Max))
            {
                return index;
            }
        }

        return -1;
    }

    private void OnMatch(float scale, in GameContext context)
    {
        glow[board.FirstCard] = 1f;
        glow[board.SecondCard] = 1f;
        UiFeedback.Play(UiSound.GameMatch);
        EmitAtCard(board.FirstCard, scale);
        EmitAtCard(board.SecondCard, scale);
        if (board.Streak == PunchStreak)
        {
            GameSfx.ComboTierUp();
        }

        if (board.Streak >= PunchStreak)
        {
            context.Fx.Flash(Accent, 0.1f);
            context.Fx.Punch(0.05f);
            return;
        }

        context.Fx.Punch(0.03f);
    }

    private void OnMismatch()
    {
        shake = 1f;
        UiFeedback.Play(UiSound.GameWrong);
        fx.AddTrauma(0.1f);
    }

    private void OnWin(float scale, in GameContext context)
    {
        finished = true;
        UiFeedback.Play(UiSound.GameClear);
        context.Fx.Sweep();
        context.Fx.Flash(Accent, 0.25f);
        particles.Confetti(new Vector2(tray.Center.X, tray.Min.Y), 72, WinPalette, 260f * scale, 4f, 1.3f);
        particles.Sparkle(grid.Center, 16, WinSparkle, 200f * scale, 2.6f, 0.9f);
        fx.Shockwave(grid.Center, grid.Width * 0.6f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
        context.Session.Finish(new GameOutcome(Math.Max(1, (int)board.Elapsed), ScoreKind.Time, GameId)
            .WithStat(L.Games.Attempts, GameNumber.Label(board.Attempts))
            .WithStat(L.Pairs.Streak, GameNumber.Label(board.BestStreak))
            .WithSecondary(AttemptsStatId, board.Attempts, ScoreKind.Time));
    }

    private void EmitAtCard(int index, float scale)
    {
        var center = CellOf(index).Center;
        var color = PairsRenderer.ColorFor(board.Symbol(index));
        particles.Burst(center, 14, color, 170f * scale, 3.2f, 0.6f, 240f);
        particles.Sparkle(center, 6, GamePalette.Lighten(color, 0.35f), 130f * scale, 2.2f, 0.7f);
        fx.Shockwave(center, grid.Pitch * 0.7f, GamePalette.Lighten(color, 0.3f), 0.4f, 2.4f);
    }

    private void DrawBoard(ImDrawListPtr drawList, float dealProgress, float scale, StageInk ink)
    {
        var plate = BoardPlate.Around(new Rect(tray.Min, grid.Bounds.Max), scale);
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, Accent, ink);
        PairsRenderer.DrawTray(drawList, board, tray, flight, Accent, scale);
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            var cell = CellOf(index);
            if (board.State(index) == CardState.Matched)
            {
                PairsRenderer.DrawEmptyCell(drawList, cell, Accent, scale);
                continue;
            }

            var pop = GameJuice.PopIn(GameJuice.Stagger(dealProgress, index, PairsBoard.CardCount));
            if (pop <= 0.01f)
            {
                continue;
            }

            var shakeX = shake > 0f && board.IsSelected(index)
                ? MathF.Sin(shake * MathF.PI * ShakeCycles) * ShakeAmplitude * scale * shake
                : 0f;
            PairsRenderer.DrawCard(drawList, Dealt(cell, pop), board.Symbol(index), flip[index], glow[index], shakeX,
                hoveredCard == index, Accent, scale);
        }

        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            if (board.State(index) != CardState.Matched || flight[index] >= 1f)
            {
                continue;
            }

            DrawFlight(drawList, index, scale);
        }
    }

    private void DrawFlight(ImDrawListPtr drawList, int index, float scale)
    {
        var cell = CellOf(index);
        var slot = PairsRenderer.TraySlot(tray, board.TraySlot(board.Symbol(index)), scale);
        var progress = Easing.EaseInOutCubic(flight[index]);
        var center = Vector2.Lerp(cell.Center, slot.Center, progress);
        center.Y -= MathF.Sin(progress * MathF.PI) * FlightArc * scale;
        var size = Vector2.Lerp(cell.Size, slot.Size, progress);
        PairsRenderer.DrawFlyer(drawList, center, size, board.Symbol(index), progress, scale);
    }

    private Rect Dealt(Rect cell, float pop)
    {
        if (pop >= 1f)
        {
            return cell;
        }

        var center = new Vector2(cell.Center.X, Easing.Lerp(tray.Center.Y, cell.Center.Y, pop));
        var half = cell.Size * 0.5f * MathF.Max(0.05f, pop);
        return new Rect(center - half, center + half);
    }

    private void DrawHud(ImDrawListPtr drawList, PhoneTheme theme, float scale, in GameContext context)
    {
        context.Hud.Clock(board.Elapsed);
        context.Hud.Best(context.Session.Best);
        var attemptsLabel = GameNumber.Label(board.Attempts);
        var streakShown = board.Streak >= StreakShown;
        var streakLabel = GameNumber.Label(board.Streak);
        var width = CapsulePadX * 2f + SectionWidth(attemptsLabel, scale);
        if (streakShown)
        {
            width += CapsuleSectionGap + SectionWidth(streakLabel, scale);
        }

        context.Hud.Custom(width);
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var centerY = rect.Center.Y;
        var left = rect.Min.X + CapsulePadX * scale;
        left = DrawSection(drawList, left, centerY, FontAwesomeIcon.Clone, Accent, attemptsLabel, theme.TextStrong,
            scale);
        if (streakShown)
        {
            DrawSection(drawList, left + CapsuleSectionGap * scale, centerY, FontAwesomeIcon.Fire, Warm, streakLabel,
                Warm, scale);
        }
    }

    private static float SectionWidth(string text, float scale) =>
        CapsuleIconSize + CapsuleIconGap + Typography.Measure(text, CapsuleStyle).X / scale;

    private static float DrawSection(ImDrawListPtr drawList, float left, float centerY, FontAwesomeIcon icon,
        Vector4 iconColor, string text, Vector4 ink, float scale)
    {
        var iconSize = CapsuleIconSize * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), icon, iconColor, iconSize);
        left += iconSize + CapsuleIconGap * scale;
        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f), text, ink,
            CapsuleStyle);
        return left + Typography.Measure(text, CapsuleStyle).X;
    }

    private Rect CellOf(int index) => grid.Cell(index % PairsBoard.Columns, index / PairsBoard.Columns);
}

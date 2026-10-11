using Aetherphone.Apps.Casino.Stage;
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

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class MinesSkin : IOriginalsSkin
{
    private const float TileGap = 6f;
    private const float BoardPad = 10f;
    private const float HoverLift = 2f;
    private const int RevealSparkles = 10;
    private const float IdleTileShimmer = 1.4f;

    private readonly MinesBoard board = new();
    private readonly bool[] planned = new bool[OriginalsRules.MinesTiles];
    private readonly bool[] open = new bool[OriginalsRules.MinesTiles];
    private readonly LabelSlot[] pickCaptions = new LabelSlot[OriginalsRules.MinesTiles];
    private readonly OriginalsStepper stepper = new("casino.originals.mines");

    private OriginalsPicker picker = OriginalsPicker.FromSeed(GameSeed.Fresh());
    private OriginalsLabel nextLabel;
    private OriginalsLabel chanceLabel;
    private int mineCount = OriginalsRules.DefaultMines;
    private int plannedCount;
    private int pendingTile = -1;
    private bool autoRun;
    private bool autoTab;
    private bool noticePending;
    private LocString notice;
    private Rect grid;
    private float idleTime;

    public string GameId => CasinoGames.Mines;

    public LocString Title => L.Originals.GameMines;

    public CasinoSign Sign => CasinoSign.Mines;

    public LocString Action => L.Strip.BetFor;

    public LocString Hint => board.Live
        ? L.Originals.MinesHint
        : autoTab ? L.Originals.MinesAutoHint : L.Originals.MinesIdleHint;

    public bool Knob => true;

    public bool AutoAvailable => true;

    public bool Live => board.Live;

    public bool Busy => pendingTile >= 0 || board.Animating || autoRun;

    public bool CanCashOut => board.CanCashOut && pendingTile < 0;

    public long CashOutValue => board.CashOutValue;

    public LocString LiveSecondary => L.Originals.RandomPick;

    public Vector2 Focus => grid.Center;

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public void Enter()
    {
        noticePending = false;
        pendingTile = -1;
        picker = OriginalsPicker.FromSeed(GameSeed.Fresh());
    }

    public void Reset()
    {
        board.Clear();
        pendingTile = -1;
        autoRun = false;
        noticePending = false;
    }

    public void Snap()
    {
        board.Snap();
    }

    public void Resume(CasinoOriginalsOpenDto open)
    {
        var round = open.Mines;
        if (round is null || board.Live || !board.Resume(round))
        {
            return;
        }

        mineCount = round.Mines;
        Raise(L.Originals.Resumed);
    }

    public void Consume(CasinoOriginalsStore originals, bool instant)
    {
        var round = originals.TakeMines();
        if (round is not null)
        {
            pendingTile = -1;
            if (!round.Granted)
            {
                autoRun = false;
                Raise(OriginalsControls.ReasonNotice(round.Reason));
                if (string.Equals(round.Reason, CasinoReasons.InvalidMove, StringComparison.Ordinal))
                {
                    originals.LoadOpen();
                }
            }
            else if (!board.Apply(round, instant))
            {
                autoRun = false;
                Raise(L.Casino.ReasonGeneric);
            }
        }

        if (!originals.TakeFailure(OriginalsGame.Mines))
        {
            return;
        }

        pendingTile = -1;
        autoRun = false;
        Raise(L.Casino.ReasonUnreachable);
    }

    public void Advance(float deltaSeconds)
    {
        board.Advance(deltaSeconds);
    }

    public int FillLadder(Span<LadderStep> steps, out int focus)
    {
        var mines = board.HasRound ? board.Mines : mineCount;
        var picks = board.HasRound ? board.SafePicks : 0;
        var count = Math.Min(steps.Length, OriginalsRules.SafeTiles(mines));
        for (var index = 0; index < count; index++)
        {
            var pick = index + 1;
            var state = pick < picks ? LadderState.Past
                : pick == picks ? LadderState.Hit
                : pick == picks + 1 && board.Live ? LadderState.Current
                : LadderState.Upcoming;
            steps[index] = new LadderStep(pickCaptions[index].Get(L.Originals.PickCaption, pick),
                OriginalsText.MultiplierHundredths(OriginalsRules.MinesHundredths(mines, pick)), state);
        }

        focus = Math.Clamp(picks, 0, Math.Max(0, count - 1));
        return count;
    }

    public void DrawWorld(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        autoTab = frame.AutoTab;
        var world = frame.World;
        var statHeight = OriginalsControls.StatHeight * scale;
        DrawStats(drawList, new Rect(world.Min, new Vector2(world.Max.X, world.Min.Y + statHeight)), ui, scale);
        var top = world.Min.Y + statHeight + Metrics.Space.Sm * scale;
        var side = MathF.Max(0f, MathF.Min(world.Width, world.Max.Y - top));
        var boardRect = new Rect(new Vector2(world.Center.X - side * 0.5f, top),
            new Vector2(world.Center.X + side * 0.5f, top + side));
        OriginalsArt.Panel(drawList, boardRect, scale);
        grid = boardRect.Inset(BoardPad * scale);
        PlayEvents(frame, scale);
        DrawTiles(drawList, frame, ui, scale);
    }

    public void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable)
    {
        var value = stepper.Draw(drawList, rect, Loc.T(L.Originals.MinesKnob), mineCount, OriginalsRules.MinMines,
            OriginalsRules.MaxMines, ui, changeable && !board.Live, UiScale.Current);
        if (value == mineCount)
        {
            return;
        }

        mineCount = value;
        if (!board.Live)
        {
            board.Clear();
        }

        TrimPlan();
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    public bool Play(CasinoOriginalsStore originals, long stake, bool auto)
    {
        if (board.Live)
        {
            return false;
        }

        if (auto && plannedCount == 0)
        {
            Raise(L.Originals.MinesAutoEmpty);
            return false;
        }

        board.Clear();
        autoRun = auto;
        originals.StartMines(stake, mineCount);
        CasinoSfx.Play(UiSound.ChipSlide);
        return true;
    }

    public void CashOut(CasinoOriginalsStore originals)
    {
        if (!CanCashOut || originals.InFlight)
        {
            return;
        }

        originals.CashOutMines(board.RoundId);
    }

    public void Secondary(CasinoOriginalsStore originals)
    {
        if (!board.Live || pendingTile >= 0 || originals.InFlight)
        {
            return;
        }

        for (var tile = 0; tile < open.Length; tile++)
        {
            open[tile] = board.Tile(tile) == MinesTile.Hidden;
        }

        Reveal(originals, picker.PickOne(open));
    }

    public void Step(CasinoOriginalsStore originals, bool auto)
    {
        if (!autoRun)
        {
            return;
        }

        if (!auto)
        {
            autoRun = false;
            return;
        }

        if (!board.Live)
        {
            if (board.HasRound && !originals.InFlight)
            {
                autoRun = false;
            }

            return;
        }

        if (originals.InFlight || pendingTile >= 0 || board.Animating)
        {
            return;
        }

        for (var tile = 0; tile < planned.Length; tile++)
        {
            if (planned[tile] && board.Tile(tile) == MinesTile.Hidden)
            {
                Reveal(originals, tile);
                return;
            }
        }

        autoRun = false;
        CashOut(originals);
    }

    public bool TakeSettled(out OriginalsOutcome outcome)
    {
        if (!board.TakeSettled(out outcome))
        {
            return false;
        }

        autoRun = false;
        return true;
    }

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
        var side = MathF.Min(rect.Width, rect.Height) * 0.86f;
        var area = new Rect(rect.Center - new Vector2(side * 0.5f, side * 0.5f),
            rect.Center + new Vector2(side * 0.5f, side * 0.5f));
        var cell = (side - TileGap * scale * (OriginalsRules.MinesSide - 1)) / OriginalsRules.MinesSide;
        var lit = (int)(idleTime * 2f) % OriginalsRules.MinesTiles;
        for (var tile = 0; tile < OriginalsRules.MinesTiles; tile++)
        {
            var rectTile = TileRect(area, tile, cell, scale);
            var rounding = cell * 0.2f;
            if (tile == lit || tile == (lit * 7 + 3) % OriginalsRules.MinesTiles)
            {
                OriginalsArt.OpenTile(drawList, rectTile, rounding, OriginalsArt.Gem, 0.18f);
                OriginalsArt.DrawGem(drawList, rectTile.Center, cell * 0.26f, 1f);
                continue;
            }

            var shimmer = 0.5f + 0.5f * MathF.Sin(idleTime * IdleTileShimmer + tile * 0.6f);
            OriginalsArt.HiddenTile(drawList, rectTile, rounding, shimmer > 0.92f, 0f, scale);
        }
    }

    private void DrawStats(ImDrawListPtr drawList, Rect row, AppSkin ui, float scale)
    {
        var mines = board.HasRound ? board.Mines : mineCount;
        var picks = board.HasRound ? board.SafePicks : 0;
        var next = OriginalsRules.MinesHundredths(mines, picks + 1);
        var chance = OriginalsRules.MinesNextChanceBasisPoints(mines, picks);
        var finished = board.HasRound && !board.Live;
        var nextText = nextLabel.Get(L.Originals.Next, OriginalsText.MultiplierHundredths(next));
        var chanceText = chanceLabel.Get(L.Originals.Chance, OriginalsText.Percent(chance));
        OriginalsControls.StatRow(drawList, row, string.Empty, nextText, finished ? ui.MutedInk : CasinoColors.Money,
            string.Empty, chanceText, finished ? ui.MutedInk : CasinoColors.LightB, ui, scale);
    }

    private void PlayEvents(in OriginalsFrame frame, float scale)
    {
        if (board.TakeSafeReveal(out var step))
        {
            CasinoSfx.Pitched(UiSound.TileSafe, step);
            var tile = board.NewestTile;
            if (tile >= 0)
            {
                frame.Stage.Particles.Emit(CasinoLights.Sparkle(scale), TileCenter(tile, scale), RevealSparkles);
            }
        }

        if (board.TakeBust())
        {
            CasinoSfx.Play(UiSound.Bust);
        }
    }

    private void DrawTiles(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui, float scale)
    {
        var cell = CellSize(scale);
        var rounding = cell * 0.2f;
        var planning = frame.AutoTab && !board.Live && frame.Interactive;
        var revealing = board.Live && pendingTile < 0 && !autoRun && frame.Interactive;
        var finished = board.HasRound && !board.Live;
        for (var tile = 0; tile < OriginalsRules.MinesTiles; tile++)
        {
            var rect = TileRect(grid, tile, cell, scale);
            var state = board.Tile(tile);
            if (state == MinesTile.Hidden)
            {
                var hovered = (planning || revealing) && UiInteract.Hover(rect.Min, rect.Max);
                var lift = tile == pendingTile ? 0f : hovered ? HoverLift * scale : 0f;
                OriginalsArt.HiddenTile(drawList, rect, rounding, hovered, lift, scale);
                if (finished)
                {
                    Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                        ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)));
                }

                DrawMarks(drawList, rect, tile, rounding, scale);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (!UiInteract.Click(rect.Min, rect.Max, hovered))
                {
                    continue;
                }

                if (planning)
                {
                    TogglePlan(tile);
                    continue;
                }

                Reveal(frame.Originals, tile);
                continue;
            }

            DrawOpened(drawList, rect, tile, state, rounding, cell, scale);
        }
    }

    private void DrawMarks(ImDrawListPtr drawList, Rect rect, int tile, float rounding, float scale)
    {
        if (tile == pendingTile)
        {
            var pulse = 0.4f + 0.4f * Pulse.Wave(Pulse.Breath);
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(CasinoColors.LightB with { W = pulse }), 2f * scale);
            return;
        }

        if (!planned[tile])
        {
            return;
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(CasinoColors.LightB with { W = 0.85f }),
            1.6f * scale);
        drawList.AddCircleFilled(rect.Center, rect.Width * 0.08f,
            ImGui.GetColorU32(CasinoColors.LightB with { W = 0.85f }), 12);
    }

    private void DrawOpened(ImDrawListPtr drawList, Rect rect, int tile, MinesTile state, float rounding, float cell,
        float scale)
    {
        var flip = board.Flip(tile);
        var squash = MathF.Abs(MathF.Cos(MathF.PI * flip));
        var half = new Vector2(rect.Width * 0.5f * MathF.Max(0.04f, squash), rect.Height * 0.5f);
        var face = new Rect(rect.Center - half, rect.Center + half);
        if (flip < 0.5f)
        {
            OriginalsArt.HiddenTile(drawList, face, MathF.Min(rounding, face.Width * 0.5f), false, 0f, scale);
            return;
        }

        var faceRounding = MathF.Min(rounding, face.Width * 0.5f);
        switch (state)
        {
            case MinesTile.Safe:
                OriginalsArt.OpenTile(drawList, face, faceRounding, OriginalsArt.Gem, 0.16f);
                OriginalsArt.DrawGem(drawList, face.Center, cell * 0.24f * squash + 0.01f, 1f);
                break;
            case MinesTile.Boom:
                var pulse = 0.22f + 0.18f * Pulse.Wave(Pulse.Breath);
                OriginalsArt.OpenTile(drawList, face, faceRounding, OriginalsArt.Boom, pulse);
                OriginalsArt.DrawMine(drawList, face.Center, cell * 0.2f * squash + 0.01f, OriginalsArt.Boom, 1f);
                break;
            default:
                OriginalsArt.OpenTile(drawList, face, faceRounding, CasinoColors.InkMuted, 0.06f);
                OriginalsArt.DrawMine(drawList, face.Center, cell * 0.18f * squash + 0.01f, CasinoColors.InkMuted,
                    0.75f);
                break;
        }
    }

    private void Reveal(CasinoOriginalsStore originals, int tile)
    {
        if (tile < 0 || board.Tile(tile) != MinesTile.Hidden)
        {
            return;
        }

        pendingTile = tile;
        originals.RevealMine(board.RoundId, tile);
    }

    private void TogglePlan(int tile)
    {
        if (planned[tile])
        {
            planned[tile] = false;
            plannedCount--;
            CasinoSfx.Play(UiSound.ChipSlide);
            return;
        }

        if (plannedCount >= OriginalsRules.SafeTiles(mineCount))
        {
            return;
        }

        planned[tile] = true;
        plannedCount++;
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void TrimPlan()
    {
        var limit = OriginalsRules.SafeTiles(mineCount);
        for (var tile = planned.Length - 1; tile >= 0 && plannedCount > limit; tile--)
        {
            if (!planned[tile])
            {
                continue;
            }

            planned[tile] = false;
            plannedCount--;
        }
    }

    private void Raise(LocString message)
    {
        notice = message;
        noticePending = true;
    }

    private float CellSize(float scale) =>
        MathF.Max(1f, (grid.Width - TileGap * scale * (OriginalsRules.MinesSide - 1)) / OriginalsRules.MinesSide);

    private Vector2 TileCenter(int tile, float scale) => TileRect(grid, tile, CellSize(scale), scale).Center;

    private static Rect TileRect(Rect area, int tile, float cell, float scale)
    {
        var column = tile % OriginalsRules.MinesSide;
        var row = tile / OriginalsRules.MinesSide;
        var gap = TileGap * scale;
        var min = new Vector2(area.Min.X + column * (cell + gap), area.Min.Y + row * (cell + gap));
        return new Rect(min, min + new Vector2(cell, cell));
    }
}

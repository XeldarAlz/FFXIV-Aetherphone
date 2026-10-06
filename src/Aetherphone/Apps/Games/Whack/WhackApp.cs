using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Whack;

internal sealed class WhackApp : IMiniGame
{
    private const string GameId = "whack";
    private const float UrgentSeconds = 10f;
    private const float FrenzyBannerSeconds = 1.4f;
    private const float HitReach = 0.15f;
    private const int PunchMultiplier = 3;
    private const int MaxGain = WhackBoard.MolePoints * ComboMeter.MaxMultiplier * 2;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Whack, GameGenre.Arcade, L.Whack.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
    private static readonly string?[] GainLabels = new string?[MaxGain + 1];
    private static readonly string BombLabel = string.Concat("-", GameNumber.Label(WhackBoard.BombPenalty));
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 Dirt = new(0.95f, 0.82f, 0.45f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.65f, 1f);
    private static readonly Vector4 Ember = new(0.95f, 0.4f, 0.32f, 1f);
    private static readonly Vector4 Flame = new(1f, 0.7f, 0.4f, 1f);
    private static readonly Vector4 Ring = new(1f, 0.9f, 0.5f, 0.9f);
    private readonly WhackBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private float entrance;
    private float frenzyBanner = 1f;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        fx.Clear();
        entrance = 0f;
        frenzyBanner = 1f;
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
        var grid = GameGrid.Centered(context.Safe, WhackBoard.Columns, WhackBoard.Rows, WhackRenderer.GapFraction);
        WhackRenderer.DrawBoard(ImGui.GetWindowDrawList(), board, grid, 1f, UiScale.Current, Accent,
            context.Backdrop.Ink);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        frenzyBanner = GameBanner.Advance(frenzyBanner, context.RawDeltaSeconds, FrenzyBannerSeconds);
        var area = Grow(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var grid = GameGrid.Centered(area, WhackBoard.Columns, WhackBoard.Rows, WhackRenderer.GapFraction);
        if (!finished)
        {
            Step(grid, scale, context);
        }

        WhackRenderer.DrawBoard(drawList, board, grid, entrance, scale, Accent, context.Backdrop.Ink);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, grid.Center, Loc.T(L.Whack.Frenzy), Gold, context.Theme, frenzyBanner);
        context.Hud.Score(board.Score);
        context.Hud.Timer(board.TimeLeft, WhackBoard.RoundSeconds, board.TimeLeft <= UrgentSeconds);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void Step(in GameGrid grid, float scale, in GameContext context)
    {
        board.Step(context.DeltaSeconds);
        if (board.Over)
        {
            finished = true;
            context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
                .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
                .WithStat(L.Whack.Moles, GameNumber.Label(board.MolesWhacked)));
            return;
        }

        if (board.TimeLeft <= UrgentSeconds)
        {
            context.Fx.Vignette(Danger, 0.10f + 0.12f * Pulse.Wave(Pulse.Fast), 0.3f);
        }
        else if (board.Frenzy)
        {
            context.Fx.Vignette(Gold, 0.16f * board.FrenzyFraction, 0.3f);
        }

        if (context.Session.State != StageFlow.Playing || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var hole = HoleAt(grid);
        if (hole < 0)
        {
            return;
        }

        var multiplierBefore = board.Combo.Multiplier;
        var result = board.Whack(hole);
        if (result == WhackResult.None)
        {
            return;
        }

        var center = MoleCenter(grid, hole);
        if (result == WhackResult.Bomb)
        {
            OnBomb(center, scale, context);
        }
        else
        {
            OnMole(hole, center, scale, board.Combo.Multiplier > multiplierBefore, context);
        }

        var chain = board.ChainMask;
        for (var knocked = 0; knocked < WhackBoard.HoleCount; knocked++)
        {
            if ((chain & (1 << knocked)) == 0)
            {
                continue;
            }

            OnKnocked(knocked, MoleCenter(grid, knocked), scale);
        }

        if (board.FrenzyStarted)
        {
            OnFrenzy(grid.Center, scale, context);
        }
    }

    private static int HoleAt(in GameGrid grid)
    {
        var reach = grid.Pitch * HitReach;
        for (var hole = WhackBoard.HoleCount - 1; hole >= 0; hole--)
        {
            var cell = grid.Cell(hole % WhackBoard.Columns, hole / WhackBoard.Columns);
            if (UiInteract.Hover(cell.Min - new Vector2(0f, reach), cell.Max))
            {
                return hole;
            }
        }

        return -1;
    }

    private static Vector2 MoleCenter(in GameGrid grid, int hole) =>
        grid.CellCenter(hole % WhackBoard.Columns, hole / WhackBoard.Columns) + new Vector2(0f, -grid.Pitch * 0.12f);

    private void OnMole(int hole, Vector2 center, float scale, bool tierUp, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameHitSoft);
        var tint = board.Frenzy ? Gold : Dirt;
        particles.Burst(center, 12, tint, 170f * scale, 3f, 0.5f, 320f);
        particles.Sparkle(center, 7, Spark, 130f * scale, 2.4f, 0.6f);
        fx.Shockwave(center, 44f * scale, Ring, 0.38f, 2.6f);
        fx.AddText(GainLabel(board.GainAt(hole)), center, Accent, 1.1f);
        fx.AddTrauma(0.08f);
        if (tierUp)
        {
            GameSfx.ComboTierUp();
        }

        if (board.Combo.Multiplier >= PunchMultiplier)
        {
            context.Fx.Punch(0.04f);
        }
    }

    private void OnBomb(Vector2 center, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        particles.Burst(center, 24, Ember, 280f * scale, 4f, 0.7f, 360f);
        particles.Streaks(center, 12, Flame, 420f * scale, 2.6f, 0.5f);
        fx.Shockwave(center, 100f * scale, Flame, 0.55f, 3.4f);
        fx.AddText(BombLabel, center, Danger, 1.2f);
        fx.AddTrauma(0.6f);
        context.Fx.Flash(Danger, 0.35f);
    }

    private void OnKnocked(int hole, Vector2 center, float scale)
    {
        if (board.KindAt(hole) == Occupant.Bomb)
        {
            particles.Burst(center, 16, Ember, 240f * scale, 3.4f, 0.6f, 360f);
            fx.Shockwave(center, 70f * scale, Flame, 0.45f, 3f);
            fx.AddTrauma(0.2f);
            return;
        }

        particles.Burst(center, 8, Dirt, 140f * scale, 2.6f, 0.45f, 320f);
        fx.AddText(GainLabel(board.GainAt(hole)), center, Accent, 0.95f);
    }

    private void OnFrenzy(Vector2 center, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        particles.Sparkle(center, 26, Gold, 220f * scale, 3f, 0.9f);
        context.Fx.Sweep();
        context.Fx.Flash(Gold, 0.22f);
        context.Fx.Punch(0.06f);
        frenzyBanner = 0f;
    }

    private static string GainLabel(int points)
    {
        var index = Math.Clamp(points, 0, MaxGain);
        return GainLabels[index] ??= string.Concat("+", GameNumber.Label(index));
    }

    private static Rect Grow(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }
}

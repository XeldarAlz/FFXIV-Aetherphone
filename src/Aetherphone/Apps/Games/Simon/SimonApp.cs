using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Simon;

internal sealed class SimonApp : IMiniGame
{
    private const string GameId = "simon";
    private const float LitDecay = 4.2f;
    private const float ShowingDim = 0.16f;
    private const float PerfectBannerSeconds = 1.3f;
    private const ulong IdleSeed = 11;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Simon, GameGenre.Brain, L.Simon.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
    private static readonly UiSound[] PadTones =
    {
        UiSound.SimonTone1, UiSound.SimonTone2, UiSound.SimonTone3, UiSound.SimonTone4,
    };
    private static readonly string?[] GainLabels = new string?[SimonBoard.MaxLength + 1];
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.65f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);

    private readonly SimonBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] lit = new float[SimonBoard.PadCount];
    private float entrance;
    private float perfectBanner = 1f;
    private bool finished;

    public SimonApp()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        Array.Clear(lit);
        particles.Clear();
        fx.Clear();
        entrance = 0f;
        perfectBanner = 1f;
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
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var grid = GameGrid.Centered(context.Safe, 2, 2, SimonRenderer.GapFraction);
        SimonRenderer.DrawBoard(drawList, grid, lit, -1, 1f, 0f, Accent, context.Backdrop.Ink, scale);
        SimonRenderer.DrawHub(drawList, grid.Center, grid.Pitch * SimonRenderer.HubRadiusFraction,
            GameNumber.Label(board.Round), Loc.Upper(Loc.T(L.Games.Watch)), Accent, context.Theme, scale, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rawSeconds = context.RawDeltaSeconds;
        particles.Update(rawSeconds);
        fx.Update(rawSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds);
        perfectBanner = GameBanner.Advance(perfectBanner, rawSeconds, PerfectBannerSeconds);
        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            lit[pad] = MathF.Max(0f, lit[pad] - rawSeconds * LitDecay);
        }

        var area = Grow(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var grid = GameGrid.Centered(area, 2, 2, SimonRenderer.GapFraction);
        var pressed = finished ? -1 : Step(grid, scale, context);
        var showing = board.Phase == SimonPhase.Showing;
        var input = board.Phase == SimonPhase.Input;
        SimonRenderer.DrawBoard(drawList, grid, lit, pressed, entrance, showing ? ShowingDim : 0f, Accent,
            context.Backdrop.Ink, scale);
        SimonRenderer.DrawHub(drawList, grid.Center, grid.Pitch * SimonRenderer.HubRadiusFraction,
            GameNumber.Label(board.Round), Loc.Upper(Loc.T(input ? L.Games.YourTurn : L.Games.Watch)), Accent,
            context.Theme, scale, input);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, grid.Center, Loc.T(L.Games.Perfect), Gold, context.Theme, perfectBanner);
        context.Hud.Score(board.Score);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private int Step(in GameGrid grid, float scale, in GameContext context)
    {
        board.Step(context.DeltaSeconds);
        if (board.PadLitThisStep)
        {
            OnShow(board.LitPad, grid, scale);
        }

        if (context.Session.State != StageFlow.Playing || board.Phase != SimonPhase.Input)
        {
            return -1;
        }

        var hovered = PadHitTest(grid);
        if (hovered < 0)
        {
            return -1;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? hovered : -1;
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return pressed;
        }

        OnPress(hovered, board.Press(hovered), grid, scale, context);
        return pressed;
    }

    private static int PadHitTest(in GameGrid grid)
    {
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            return -1;
        }

        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            var rect = SimonRenderer.PadRect(grid, pad);
            if (UiInteract.Hover(rect.Min, rect.Max))
            {
                return pad;
            }
        }

        return -1;
    }

    private void OnShow(int pad, in GameGrid grid, float scale)
    {
        lit[pad] = 1f;
        UiFeedback.Play(PadTones[pad]);
        fx.Shockwave(SimonRenderer.PadRect(grid, pad).Center, grid.Pitch * 0.42f,
            SimonRenderer.ColorOf(pad) with { W = 0.7f }, 0.42f, 2.4f);
    }

    private void OnPress(int pad, SimonPress result, in GameGrid grid, float scale, in GameContext context)
    {
        if (result == SimonPress.Wrong)
        {
            OnFail(grid, scale, context);
            return;
        }

        if (result == SimonPress.Ignored)
        {
            return;
        }

        lit[pad] = 1f;
        UiFeedback.Play(PadTones[pad]);
        BurstPad(grid, pad, 10, scale);
        fx.Shockwave(SimonRenderer.PadRect(grid, pad).Center, grid.Pitch * 0.5f,
            SimonRenderer.ColorOf(pad) with { W = 0.8f }, 0.36f, 2.4f);
        if (result == SimonPress.RoundComplete)
        {
            OnRoundComplete(grid, scale, context);
        }
    }

    private void OnRoundComplete(in GameGrid grid, float scale, in GameContext context)
    {
        fx.AddTrauma(0.12f);
        particles.Sparkle(grid.Center, 14, Spark, 160f * scale, 2.6f, 0.8f);
        fx.Shockwave(grid.Center, grid.Pitch * 0.9f, GamePalette.Lighten(Accent, 0.3f), 0.55f, 3f);
        fx.AddText(GainLabel(board.Score), grid.Center - new Vector2(0f, grid.Pitch * 0.3f), Accent, 1.15f);
        context.Fx.Punch(0.03f);
        if (board.Score % SimonBoard.RampEvery != 0)
        {
            return;
        }

        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Flash(Gold, 0.18f);
        context.Fx.Punch(0.06f);
        perfectBanner = 0f;
    }

    private void OnFail(in GameGrid grid, float scale, in GameContext context)
    {
        finished = true;
        UiFeedback.Play(UiSound.GameWrong);
        fx.AddTrauma(0.7f);
        context.Fx.Flash(Danger, 0.4f);
        context.Fx.Vignette(Danger, 0.5f, 0.5f);
        fx.Shockwave(grid.Center, grid.Pitch * 1.3f, Danger, 0.6f, 3.4f);
        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            BurstPad(grid, pad, 14, scale);
        }

        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)context.Session.PlaySeconds)));
    }

    private void BurstPad(in GameGrid grid, int pad, int count, float scale)
    {
        var center = SimonRenderer.PadRect(grid, pad).Center;
        particles.Burst(center, count, SimonRenderer.ColorOf(pad), 150f * scale, 3f, 0.5f, 240f);
    }

    private static string GainLabel(int points)
    {
        var index = Math.Clamp(points, 0, SimonBoard.MaxLength);
        return GainLabels[index] ??= string.Concat("+", GameNumber.Label(index));
    }

    private static Rect Grow(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }
}

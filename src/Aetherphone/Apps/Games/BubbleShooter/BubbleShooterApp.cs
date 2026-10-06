using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleShooterApp : IMiniGame
{
    private const string GameId = "bubbles";
    private const int SlowMoClearCount = 6;
    private const float SlowMoFactor = 0.6f;
    private const float SlowMoSeconds = 0.2f;
    private const float BigClearPunch = 0.04f;
    private const int DangerRowsLeft = 1;
    private const float MinAimY = -0.2f;
    private const float RowShake = 0.12f;
    private const float BlastShake = 0.55f;
    private const ulong IdleSeed = 0x425542424C4553UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Bubbles, GameGenre.Puzzle, L.BubbleShooter.Hook,
        Backdrop.Cavern, HudStyle.Standard, ScoreKind.Score);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.70f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Blast = new(1f, 0.70f, 0.32f, 1f);
    private static readonly Vector4 BlastFlash = new(1f, 0.72f, 0.36f, 0.42f);
    private static readonly Vector4 Gold = new(1f, 0.86f, 0.42f, 1f);
    private static readonly ParticleSpec BlastSparkle = new(Gold, Blast with { W = 0f }, 0.012f, 0.8f, 0.65f, 1.2f, 2f,
        6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec ChainSparkle = new(Spark, White, 0.010f, 0.5f, 0.7f, 0.6f, 2f, 6f,
        shape: ParticleShape.Star, additive: true);
    private readonly BubbleBoard board = new();
    private readonly BubbleRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private LabelSlot comboLabel;
    private bool finished;

    public BubbleShooterApp()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        camera = Camera2D.Create();
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        renderer.Draw(ImGui.GetWindowDrawList(), board, in camera, UiScale.Current, Vector2.Zero, context.Theme);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        PlaceCamera(context);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        var playing = context.Session.State == StageFlow.Playing;
        var aim = ComputeAim();
        if (!finished)
        {
            Step(simDelta, aim, playing, context);
        }

        renderer.Draw(drawList, board, in camera, scale, playing && !finished ? aim : Vector2.Zero, context.Theme);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawNextCapsule(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(BubbleRenderer.NextCapsuleWidth(scale));
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, BubbleBoard.FieldWidth, BubbleBoard.FieldHeight, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private Vector2 ComputeAim()
    {
        var direction = camera.ToWorld(ImGui.GetMousePos()) - BubbleBoard.LauncherPosition;
        if (direction.LengthSquared() < 0.0001f)
        {
            return new Vector2(0f, -1f);
        }

        if (direction.Y > MinAimY)
        {
            direction.Y = MinAimY;
        }

        return Vector2.Normalize(direction);
    }

    private void Step(float deltaSeconds, Vector2 aim, bool playing, in GameContext context)
    {
        if (playing)
        {
            HandleInput(aim, context);
        }

        board.Update(deltaSeconds);
        ReactToEvents(context);
        if (!board.GameOver)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
            .WithStat(L.BubbleShooter.Popped, GameNumber.Label(board.TotalPopped))
            .WithStat(L.BubbleShooter.Shots, GameNumber.Label(board.ShotsFired)));
    }

    private void HandleInput(Vector2 aim, in GameContext context)
    {
        var full = context.Full;
        var hitMin = new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * UiScale.Current);
        if (!UiInteract.Hover(hitMin, full.Max))
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            if (board.Swap())
            {
                UiFeedback.Play(UiSound.GameTick);
                fx.Shockwave(camera.ToScreen(BubbleBoard.LauncherPosition), camera.Px(BubbleBoard.Radius * 1.8f),
                    White with { W = 0.7f }, 0.24f, 2f);
            }

            return;
        }

        if (board.Flying || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        board.Fire(aim);
        UiFeedback.Play(UiSound.GameShoot);
    }

    private void ReactToEvents(in GameContext context)
    {
        if (board.BlastedThisShot)
        {
            OnBlast(context);
        }

        if (board.RowAddedThisFrame)
        {
            camera.Shake(RowShake);
        }

        if (board.PopCount == 0)
        {
            if (board.LandedThisFrame)
            {
                UiFeedback.Play(UiSound.GameHitSoft);
            }

            PulseDanger(context);
            return;
        }

        var sum = Vector2.Zero;
        for (var index = 0; index < board.PopCount; index++)
        {
            var center = board.PopPosition(index);
            sum += center;
            var color = BubbleRenderer.ColorOf(board.PopColor(index));
            particles.Burst(center, 9, color, 0.4f, 0.011f, 0.5f, 1.5f);
            fx.Shockwave(camera.ToScreen(center), camera.Px(BubbleBoard.Radius * 1.2f), GamePalette.Lighten(color, 0.3f),
                0.3f, 2f);
        }

        var burstCenter = sum / board.PopCount;
        var burstScreen = camera.ToScreen(burstCenter);
        UiFeedback.Play(UiSound.GamePop);
        camera.Shake(MathF.Min(0.35f, 0.04f * board.PopCount));
        if (board.LastShotScore > 0)
        {
            fx.AddText(GameNumber.Label(board.LastShotScore), burstScreen, Accent, 1.25f);
        }

        if (board.TierUpThisShot)
        {
            GameSfx.ComboTierUp();
            fx.AddText(comboLabel.Get(L.Stage.Times, board.Combo.Multiplier),
                burstScreen - new Vector2(0f, camera.Px(BubbleBoard.Radius * 1.6f)), Gold, 1.15f);
        }

        if (board.ClearedThisShot >= SlowMoClearCount)
        {
            context.Fx.SlowMo(SlowMoFactor, SlowMoSeconds);
            context.Fx.Punch(BigClearPunch);
            fx.HitStop(0.05f);
            particles.Emit(ChainSparkle, burstCenter, 12);
        }

        PulseDanger(context);
    }

    private void OnBlast(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        var blast = board.BlastPosition;
        fx.Shockwave(camera.ToScreen(blast), camera.Px(BubbleBoard.Radius * 6f), Blast, 0.45f, 4f);
        context.Fx.Flash(BlastFlash, 0.55f);
        camera.Shake(BlastShake);
        fx.HitStop(0.07f);
        particles.Emit(BlastSparkle, blast, 20);
    }

    private void PulseDanger(in GameContext context)
    {
        if (board.GameOver || board.RowsToDanger > DangerRowsLeft)
        {
            return;
        }

        context.Fx.Vignette(Danger, 0.10f + 0.12f * Pulse.Wave(Pulse.Fast), 0.3f);
    }

    private void DrawNextCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        BubbleRenderer.DrawNext(drawList, rect, board, scale, context.Theme);
    }
}

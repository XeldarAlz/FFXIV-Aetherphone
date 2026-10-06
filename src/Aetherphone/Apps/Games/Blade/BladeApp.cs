using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Blade;

internal sealed class BladeApp : IMiniGame
{
    private const string GameId = "blade";
    private const ulong IdleSeed = 3;
    private const float IdleSpin = 0.45f;
    private const float ResultDelaySeconds = 0.5f;
    private const float VibrationSeconds = 0.15f;
    private const float SkyStart = 0.2f;
    private const float SkyEnd = 0.8f;
    private const float SkyLevels = 19f;
    private const float RibbonWidth = 0.07f;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Blade, GameGenre.Arcade, L.Blade.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
    private static readonly string AppleLabel = string.Concat("+", GameNumber.Label(BladeBoard.ApplePoints));
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 WoodChip = new(0.72f, 0.54f, 0.38f, 1f);
    private static readonly Vector4 Steel = new(0.88f, 0.90f, 0.96f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.94f, 0.72f, 1f);
    private static readonly Vector4 ImpactRing = new(1f, 0.95f, 0.85f, 0.8f);
    private static readonly Vector4 BlockedRing = new(1f, 0.78f, 0.42f, 1f);
    private static readonly Vector4 AppleRed = new(0.92f, 0.26f, 0.30f, 1f);
    private static readonly Vector4 AppleLeaf = new(0.45f, 0.78f, 0.40f, 1f);
    private static readonly ParticleSpec WoodChips = new(WoodChip, WoodChip with { W = 0f }, 0.05f, 1.6f, 0.4f, 7f,
        1.6f, 8f, MathF.PI * 0.9f, MathF.PI * 0.5f, ParticleShape.Shard);
    private static readonly ParticleSpec AppleShards = new(AppleRed, AppleLeaf with { W = 0.4f }, 0.06f, 2.2f, 0.6f,
        6f, 1.4f, 9f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec SteelShards = new(Steel, Steel with { W = 0f }, 0.06f, 3.2f, 0.7f, 8f, 1.4f,
        10f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec SteelStreaks = new(Spark, Spark with { W = 0f }, 0.07f, 4.5f, 0.5f, 6f, 1.2f,
        shape: ParticleShape.Streak);
    private static readonly ParticleSpec ClearSparkle = new(Spark, Spark with { W = 0f }, 0.08f, 2.4f, 0.8f, 1.5f,
        2f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec ClearStreaks = new(Steel, Steel with { W = 0f }, 0.05f, 3.5f, 0.5f, 2f, 1.2f,
        shape: ParticleShape.Streak);
    private readonly BladeBoard board = new();
    private readonly ParticleSystem particles = new(288);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private Camera2D camera = Camera2D.Create();
    private LabelSlot levelLabel;
    private float vibration;
    private float vibrationTime;
    private float resultDelay;
    private bool finished;

    public BladeApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        camera = Camera2D.Create();
        vibration = 0f;
        resultDelay = 0f;
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        context.Backdrop.SetSky(SkyAt(board.Level));
        var idleDelta = context.RawDeltaSeconds * IdleSpin;
        board.Step(idleDelta);
        PushRim(idleDelta);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        vibration = MathF.Max(0f, vibration - context.RawDeltaSeconds / VibrationSeconds);
        vibrationTime += context.RawDeltaSeconds;
        PlaceCamera(context);
        context.Backdrop.SetSky(SkyAt(board.Level));
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        DrawPips(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Level(board.Level);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        ribbon.Clear();
        vibration = 0f;
    }

    private static float SkyAt(int level) => SkyStart + (SkyEnd - SkyStart) * Easing.Clamp01((level - 1) / SkyLevels);

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, BladeRenderer.WorldWidth, BladeRenderer.WorldHeight, FitMode.Contain);
        camera.Place(BladeRenderer.WorldCenter);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void PushRim(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        ribbon.Push(BladeRenderer.RimPoint(board.WheelAngle + BladeRenderer.RimPhase));
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        var result = board.Step(deltaSeconds);
        PushRim(deltaSeconds);
        switch (result)
        {
            case BladeThrow.Stuck:
                OnStuck(context);
                break;
            case BladeThrow.LevelCleared:
                OnLevelCleared(context);
                break;
            case BladeThrow.Blocked:
                OnBlocked(context);
                break;
            default:
                break;
        }

        if (board.AppleHitThisStep)
        {
            OnApple(context);
        }

        if (board.ReversedThisStep)
        {
            OnReversed(context);
        }

        if (board.State != BladeState.Over)
        {
            return;
        }

        resultDelay -= deltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Blade.Apples, GameNumber.Label(board.Apples))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)context.Session.PlaySeconds)));
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing || board.State != BladeState.Playing)
        {
            return;
        }

        var safe = context.Safe;
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(safe.Min, safe.Max))
        {
            return;
        }

        if (!board.Throw())
        {
            return;
        }

        UiFeedback.Play(UiSound.GameShoot);
        camera.Shake(0.05f);
    }

    private void OnStuck(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameHitWood);
        var impact = BladeRenderer.ImpactPoint;
        camera.Shake(0.18f);
        fx.HitStop(0.03f);
        context.Fx.Punch(0.04f);
        fx.Shockwave(camera.ToScreen(impact), camera.Px(0.3f), ImpactRing, 0.32f, 2.2f);
        particles.Emit(WoodChips, impact, 8);
        vibration = 1f;
    }

    private void OnApple(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameCollect);
        var world = BladeRenderer.ApplePoint(board.AppleHitAngle);
        particles.Emit(AppleShards, world, 14);
        fx.AddText(AppleLabel, camera.ToScreen(world), GamePalette.Lighten(AppleRed, 0.3f), 1.1f);
        context.Fx.Punch(0.03f);
    }

    private void OnReversed(in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        context.Fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.2f);
        camera.Shake(0.25f);
        ribbon.Clear();
    }

    private void OnLevelCleared(in GameContext context)
    {
        GameSfx.LevelClear();
        var scale = UiScale.Current;
        var center = camera.ToScreen(Vector2.Zero);
        var radius = camera.Px(1f);
        fx.HitStop(0.06f);
        camera.Shake(0.32f);
        context.Fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.28f);
        context.Fx.Sweep();
        fx.Shockwave(center, radius * 2.1f, GamePalette.Lighten(Accent, 0.45f), 0.55f, 3.2f, radius);
        fx.AddText(levelLabel.Get(L.Stage.LevelShort, board.Level + 1),
            new Vector2(center.X, center.Y - radius - 20f * scale), GamePalette.Lighten(Accent, 0.5f), 1.15f);
        particles.Emit(ClearSparkle, Vector2.Zero, 20);
        for (var index = 0; index < board.StuckCount; index++)
        {
            var angle = board.StuckAngle(index);
            particles.Emit(ClearStreaks.WithDirection(angle, 0.5f), BladeRenderer.RimPoint(angle), 3);
        }
    }

    private void OnBlocked(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameWrong);
        var impact = BladeRenderer.ImpactPoint;
        context.Fx.Flash(Danger, 0.5f);
        context.Fx.SlowMo(0.4f, 0.3f);
        camera.Shake(0.75f);
        fx.HitStop(0.07f);
        fx.Shockwave(camera.ToScreen(impact), camera.Px(1.1f), BlockedRing, 0.6f, 3.4f);
        particles.Emit(SteelShards, impact, 20);
        particles.Emit(SteelStreaks, impact, 12);
        resultDelay = ResultDelaySeconds;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        BladeRenderer.Draw(drawList, in camera, board, Accent, vibration, vibrationTime, scale);
        ribbon.Draw(drawList, in camera, GamePalette.Lighten(Accent, 0.3f) with { W = 0.55f }, camera.Px(RibbonWidth),
            additive: true);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawPips(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        context.Hud.Custom(BladeRenderer.PipsWidth(board.LevelBlades));
        var slot = context.Hud.CustomRect(0);
        if (slot.Width <= 0f)
        {
            return;
        }

        BladeRenderer.DrawPips(drawList, slot, board, Accent, scale);
    }
}

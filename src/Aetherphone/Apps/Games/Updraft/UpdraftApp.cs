using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Updraft;

internal sealed class UpdraftApp : IMiniGame
{
    internal const string HeightStatId = "updraft.height";
    private const string GameId = "updraft";
    private const float BannerSeconds = 1.5f;
    private const float PowerUpRadius = 15f;
    private const float SkyMetres = 1500f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float BoostStreakRate = 66f;
    private const float BoostGlowRate = 33f;
    private const float GlideSparkleRate = 33f;
    private const float CrystalPulseDecay = 3f;
    private const int ConfettiCount = 90;
    private const float ConfettiSpeed = 7f;
    private const float ConfettiSize = 0.09f;
    private const float ConfettiLife = 1.4f;
    private const float ConfettiGravity = 12.6f;
    private const ulong IdleSeed = 0x55504452UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Updraft.Title, GameGenre.Arcade, L.Updraft.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly TextStyle HeightStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 PuffColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Vector4 FragileColor = new(0.86f, 0.82f, 0.96f, 0.9f);
    private static readonly Vector4 ZapFlash = new(0.75f, 0.80f, 1f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.84f, 0.38f, 1f), new(0.50f, 0.93f, 1f, 1f), new(1f, 0.52f, 0.64f, 1f), new(0.62f, 0.90f, 0.46f, 1f),
        new(0.80f, 0.62f, 0.98f, 1f), new(1f, 1f, 1f, 1f),
    };

    private static readonly ParticleSpec Puff = new(PuffColor, PuffColor with { W = 0f }, 0.08f, 2.5f, 0.45f, -0.9f,
        1.6f, 12f, MathF.PI, MathF.PI * 0.5f);
    private static readonly ParticleSpec FragileShards = new(FragileColor, FragileColor, 0.075f, 2.8f, 0.7f, 8.8f, 1.6f,
        12f, MathF.PI, MathF.PI * 0.5f);
    private static readonly ParticleSpec ZapStreaks = new(UpdraftArt.BoltColor, UpdraftArt.BoltColor, 0.055f, 8.8f, 0.4f,
        5f, 1.6f, 12f, shape: ParticleShape.Streak);
    private static readonly ParticleSpec ShieldGlow = new(UpdraftArt.BubbleColor with { W = 0.8f },
        UpdraftArt.BubbleColor with { W = 0f }, 0.06f, 4.6f, 0.6f, 1.4f, 1.6f, 12f, shape: ParticleShape.GlowCircle);
    private static readonly ParticleSpec GoldSparkle = new(UpdraftArt.GoldColor, UpdraftArt.GoldColor, 0.07f, 5.1f, 0.8f,
        0.9f, 2.4f, 6f, shape: ParticleShape.Star);
    private static readonly ParticleSpec FeatherSparkle = new(UpdraftArt.GoldColor, UpdraftArt.GoldColor, 0.06f, 3.7f,
        0.8f, 0.9f, 2.4f, 6f, shape: ParticleShape.Star);
    private static readonly ParticleSpec GlideSparkle = new(UpdraftArt.GoldColor, UpdraftArt.GoldColor, 0.045f, 0.9f,
        0.5f, 0.9f, 2.4f, 6f, shape: ParticleShape.Star);
    private static readonly ParticleSpec BoostStreak = new(PuffColor with { W = 0.8f }, PuffColor with { W = 0.8f },
        0.05f, 3.7f, 0.35f, 5f, 1.6f, 12f, 0.5f, MathF.PI * 0.5f, ParticleShape.Streak);
    private static readonly ParticleSpec GoldBoostStreak = new(UpdraftArt.GoldColor with { W = 0.8f },
        UpdraftArt.GoldColor with { W = 0.8f }, 0.05f, 3.7f, 0.35f, 5f, 1.6f, 12f, 0.5f, MathF.PI * 0.5f,
        ParticleShape.Streak);
    private static readonly ParticleSpec BoostGlow = new(PuffColor with { W = 0.5f }, PuffColor with { W = 0f }, 0.1f,
        0.45f, 0.35f, shape: ParticleShape.GlowCircle);
    private static readonly ParticleSpec GoldBoostGlow = new(UpdraftArt.GoldColor with { W = 0.5f },
        UpdraftArt.GoldColor with { W = 0f }, 0.1f, 0.45f, 0.35f, shape: ParticleShape.GlowCircle);

    private readonly UpdraftBoard board = new();
    private readonly UpdraftBoard idleBoard = new();
    private readonly UpdraftRenderer renderer = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private Emitter boostStreaks = new(BoostStreak, BoostStreakRate);
    private Emitter boostGlow = new(BoostGlow, BoostGlowRate);
    private Emitter glideSparkle = new(GlideSparkle, GlideSparkleRate);
    private LabelSlot heightLabel;
    private LabelSlot milestoneLabel;
    private ulong pendingSeed;
    private bool startPending;
    private bool finished;
    private bool pointerMode;
    private bool idleReady;
    private float bannerProgress = 1f;
    private float crystalPulse;
    private float time;
    private string bannerText = string.Empty;
    private Vector2 lastMouse;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        pendingSeed = start.Seed;
        startPending = true;
        finished = false;
        pointerMode = false;
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        boostStreaks.Reset();
        boostGlow.Reset();
        glideSparkle.Reset();
        bannerProgress = 1f;
        bannerText = string.Empty;
        crystalPulse = 0f;
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
        if (!idleReady)
        {
            idleBoard.StartGame(SeedOf(IdleSeed), 0f);
            idleReady = true;
        }

        time += context.RawDeltaSeconds;
        var cameraBottom = UpdraftBoard.StartCamera;
        PlaceCamera(context, cameraBottom);
        var progress = SkyProgress(cameraBottom);
        context.Backdrop.SetSky(progress);
        DrawWorld(ImGui.GetWindowDrawList(), context, idleBoard, cameraBottom, UpdraftRenderer.ToneAt(progress), false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (startPending)
        {
            BeginRun(context);
        }

        time += raw;
        var input = finished ? UpdraftInput.Keys(0f) : ReadInput(context);
        if (!finished && !board.Launched && context.Session.State == StageFlow.Playing)
        {
            board.Launch();
        }

        board.Tick(simDelta, input);
        var cameraBottom = UpdraftRenderer.InterpolatedCamera(board);
        PlaceCamera(context, cameraBottom);
        ReactToEvents(context, scale);
        EmitTrails(simDelta);
        renderer.Animate(board, context.DeltaSeconds, time);
        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        crystalPulse = MathF.Max(0f, crystalPulse - raw * CrystalPulseDecay);
        if (board.GameOver && !finished)
        {
            finished = true;
            Finish(context);
        }

        var progress = SkyProgress(cameraBottom);
        context.Backdrop.SetSky(progress);
        DrawWorld(drawList, context, board, cameraBottom, UpdraftRenderer.ToneAt(progress), true);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f),
            bannerText, Accent, context.Theme, bannerProgress);
        DrawHeightCapsule(drawList, context, scale);
        DrawPowerUps(drawList, context.Safe, scale);
        context.Hud.Score(board.Score);
        context.Hud.Custom(HeightCapsuleWidth(scale));
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void BeginRun(in GameContext context)
    {
        var bestMetres = context.Session.SecondaryBest(HeightStatId);
        board.StartGame(SeedOf(pendingSeed), bestMetres / UpdraftBoard.MetresPerUnit);
        renderer.Reset();
        startPending = false;
    }

    private static int SeedOf(ulong seed) => unchecked((int)(seed ^ (seed >> 32)));

    private static float SkyProgress(float cameraBottom) =>
        Math.Clamp((cameraBottom + UpdraftBoard.ViewHeight * 0.5f) * UpdraftBoard.MetresPerUnit / SkyMetres, 0f, 1f);

    private void PlaceCamera(in GameContext context, float cameraBottom)
    {
        var scale = UiScale.Current;
        camera.Fit(context.Full, UpdraftBoard.FieldWidth, UpdraftBoard.ViewHeight, FitMode.CoverWidth);
        var halfHeightUnits = context.Full.Height / (2f * camera.Zoom);
        camera.Place(new Vector2(UpdraftBoard.FieldWidth * 0.5f, -(cameraBottom + halfHeightUnits)));
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private UpdraftInput ReadInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            pointerMode = false;
            return UpdraftInput.Keys(0f);
        }

        var left = GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var axis = (right ? 1f : 0f) - (left ? 1f : 0f);
        var mouse = ImGui.GetMousePos();
        var full = context.Full;
        var hovering = UiInteract.Hover(full.Min, full.Max) && !context.ChromeHit(mouse);
        if (left || right)
        {
            pointerMode = false;
        }
        else if (hovering && Vector2.DistanceSquared(mouse, lastMouse) > 1f)
        {
            pointerMode = true;
        }

        lastMouse = mouse;
        return pointerMode && hovering
            ? UpdraftInput.Pointer(UpdraftBoard.Wrap(camera.ToWorld(mouse).X))
            : UpdraftInput.Keys(axis);
    }

    private static Vector2 World(float x, float y) => new(x, -y);

    private void ReactToEvents(in GameContext context, float scale)
    {
        var events = board.Events;
        for (var index = 0; index < events.Length; index++)
        {
            ref readonly var item = ref events[index];
            var world = World(item.X, item.Y);
            var screen = camera.ToScreen(world);
            switch (item.Kind)
            {
                case UpdraftEventKind.Bounce:
                    OnBounce(item.CloudKind, world, screen, scale, context);
                    break;
                case UpdraftEventKind.Break:
                    UiFeedback.Play(UiSound.GameBreak);
                    particles.Emit(FragileShards, world + new Vector2(0f, 0.4f), 14);
                    break;
                case UpdraftEventKind.Zap:
                    OnZap(world, screen, scale, context);
                    break;
                case UpdraftEventKind.ShieldBlock:
                    UiFeedback.Play(UiSound.GamePop);
                    fx.Shockwave(screen, camera.Px(1.6f), UpdraftArt.BubbleColor, 0.5f, 3f);
                    particles.Emit(ShieldGlow, world, 16);
                    fx.AddText(Loc.T(L.Updraft.Blocked), screen - new Vector2(0f, 24f * scale), UpdraftArt.BubbleColor, 1.1f);
                    camera.Shake(0.2f);
                    break;
                case UpdraftEventKind.Crystal:
                    OnCrystal(item, world, screen, scale);
                    break;
                case UpdraftEventKind.Feather:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    particles.Emit(FeatherSparkle, world, 16);
                    fx.AddText(Loc.T(L.Updraft.Feather), screen - new Vector2(0f, 24f * scale), UpdraftArt.GoldColor, 1.1f);
                    break;
                case UpdraftEventKind.Shield:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    fx.Shockwave(screen, camera.Px(1.25f), UpdraftArt.BubbleColor, 0.45f, 2.6f);
                    fx.AddText(Loc.T(L.Updraft.Shield), screen - new Vector2(0f, 24f * scale), UpdraftArt.BubbleColor, 1.1f);
                    break;
                case UpdraftEventKind.Milestone:
                    UiFeedback.Play(UiSound.GameClear);
                    bannerText = milestoneLabel.Get(L.Updraft.Metres, item.Value);
                    bannerProgress = 0f;
                    context.Fx.Sweep();
                    particles.Emit(new ParticleSpec(GamePalette.Lighten(Accent, 0.4f), White, 0.06f, 3.5f, 0.7f, 0.9f,
                        2.4f, 6f, shape: ParticleShape.Star), world, 12);
                    break;
                case UpdraftEventKind.PassedBest:
                    UiFeedback.Play(UiSound.GameClear);
                    bannerText = Loc.T(L.Updraft.PassedBest);
                    bannerProgress = 0f;
                    EmitConfetti(camera.ToWorld(new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.2f)));
                    context.Fx.Flash(GamePalette.Lighten(Accent, 0.3f), 0.16f);
                    break;
                case UpdraftEventKind.Fell:
                    UiFeedback.Play(UiSound.GameWrong);
                    camera.Shake(0.3f);
                    break;
            }
        }
    }

    private void OnBounce(UpdraftCloudKind kind, Vector2 world, Vector2 screen, float scale, in GameContext context)
    {
        renderer.OnBounce(kind);
        particles.Emit(Puff, world, 9);
        switch (kind)
        {
            case UpdraftCloudKind.Golden:
                UiFeedback.Play(UiSound.GamePowerUp);
                context.Fx.Flash(UpdraftArt.GoldColor, 0.22f);
                context.Fx.Punch(0.05f);
                fx.Shockwave(screen, camera.Px(2.1f), UpdraftArt.GoldColor, 0.5f, 3.4f);
                particles.Emit(GoldSparkle, world, 18);
                fx.AddText(Loc.T(L.Updraft.SuperBounce), screen - new Vector2(0f, 30f * scale), UpdraftArt.GoldColor, 1.2f);
                camera.Shake(0.12f);
                return;
            case UpdraftCloudKind.Spring:
                UiFeedback.Play(UiSound.GamePop);
                fx.Shockwave(screen, camera.Px(1.4f), UpdraftArt.CoilColor, 0.4f, 2.6f);
                camera.Shake(0.08f);
                return;
            default:
                UiFeedback.Play(UiSound.GameJump);
                return;
        }
    }

    private void OnZap(Vector2 world, Vector2 screen, float scale, in GameContext context)
    {
        renderer.OnZap();
        UiFeedback.Play(UiSound.GameHitSoft);
        camera.Shake(0.55f);
        fx.HitStop(0.06f);
        context.Fx.Flash(ZapFlash, 0.35f);
        particles.Emit(ZapStreaks, world, 14);
        fx.AddText(Loc.T(L.Updraft.Zap), screen - new Vector2(0f, 24f * scale), ZapFlash, 1.15f);
    }

    private void OnCrystal(in UpdraftEvent item, Vector2 world, Vector2 screen, float scale)
    {
        UiFeedback.Play(UiSound.GameCollect);
        crystalPulse = 1f;
        var chain = Math.Min(item.Detail, UpdraftBoard.MaxChain);
        var climb = chain / (float)UpdraftBoard.MaxChain;
        var color = Vector4.Lerp(UpdraftArt.CrystalColor, UpdraftArt.GoldColor, climb);
        particles.Emit(new ParticleSpec(color, color, 0.055f, 2.8f, 0.6f, 0.9f, 2.4f, 6f, shape: ParticleShape.Star), world,
            6 + item.Detail * 2);
        fx.AddText(GameNumber.Signed(UpdraftBoard.CrystalPoints * Math.Max(1, chain)), screen - new Vector2(0f, 14f * scale),
            color, 0.9f + 0.12f * item.Detail, 46f + 10f * item.Detail);
    }

    private void EmitTrails(float deltaSeconds)
    {
        if (!board.Launched || board.GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        var bird = UpdraftRenderer.InterpolatedBird(board);
        var world = World(bird.X, bird.Y);
        if (board.Boosting)
        {
            var golden = board.LastBounceKind == UpdraftCloudKind.Golden;
            boostStreaks.Spec = golden ? GoldBoostStreak : BoostStreak;
            boostGlow.Spec = golden ? GoldBoostGlow : BoostGlow;
            boostStreaks.Advance(deltaSeconds, world + new Vector2(0f, UpdraftBoard.BirdRadius), particles);
            boostGlow.Advance(deltaSeconds, world, particles);
            return;
        }

        if (board.Gliding)
        {
            glideSparkle.Advance(deltaSeconds, world + new Vector2(0f, UpdraftBoard.BirdRadius * 0.6f), particles);
        }
    }

    private void EmitConfetti(Vector2 origin)
    {
        particles.Confetti(origin, ConfettiCount, CelebrationPalette, ConfettiSpeed, ConfettiSize, ConfettiLife,
            ConfettiGravity);
    }

    private void Finish(in GameContext context)
    {
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithSecondary(HeightStatId, board.HeightMetres, ScoreKind.Score)
            .WithStat(L.Updraft.Height, heightLabel.Get(L.Updraft.Metres, board.HeightMetres))
            .WithStat(L.Updraft.Crystals, GameNumber.Label(board.Crystals))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)context.Session.PlaySeconds)));
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, UpdraftBoard target, float cameraBottom,
        in UpdraftTone tone, bool live)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        drawList.PushClipRect(full.Min, full.Max, true);
        renderer.Draw(drawList, target, in camera, cameraBottom, in tone, context.Backdrop.Ground, Accent, time, scale);
        if (live)
        {
            particles.Draw(drawList, in camera);
            fx.DrawRings(drawList, scale);
            fx.DrawText();
        }

        drawList.PopClipRect();
    }

    private float HeightCapsuleWidth(float scale)
    {
        var text = Typography.Measure(heightLabel.Get(L.Updraft.Metres, board.HeightMetres), HeightStyle).X / scale;
        return CapsulePadX * 2f + IconSize + IconGap + text;
    }

    private void DrawHeightCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        var ink = board.PassedBest ? UpdraftArt.GoldColor : Accent;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.ArrowUp, ink,
            iconSize);
        var origin = new Vector2(left + iconSize + IconGap * scale, rect.Center.Y - Typography.LineHeight(HeightStyle) * 0.5f);
        var pop = 1f + 0.08f * crystalPulse;
        Typography.Draw(drawList, origin, heightLabel.Get(L.Updraft.Metres, board.HeightMetres), context.Theme.TextStrong,
            HeightStyle.Scale * pop, HeightStyle.Weight);
    }

    private void DrawPowerUps(ImDrawListPtr drawList, Rect safe, float scale)
    {
        var radius = PowerUpRadius * scale;
        var center = new Vector2(safe.Min.X + radius, safe.Min.Y + radius);
        var corner = new Vector2(radius, radius);
        if (board.Gliding)
        {
            Material.Frosted(drawList, center - corner, center + corner, radius, scale);
            var ringRadius = radius - 2.5f * scale;
            ProgressRing.Track(drawList, center, ringRadius, 3f * scale, White with { W = 0.15f });
            ProgressRing.Fill(drawList, center, ringRadius, 3f * scale, board.FeatherFraction, UpdraftArt.GoldColor);
            UpdraftArt.DrawFeather(drawList, center, radius * 0.4f, 0f);
            center.X += radius * 2f + 8f * scale;
        }

        if (!board.HasShield)
        {
            return;
        }

        Material.Frosted(drawList, center - corner, center + corner, radius, scale);
        UpdraftArt.DrawBubble(drawList, center, radius * 0.62f, time, 1f);
    }
}

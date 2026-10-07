using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Fling;

internal sealed class FlingApp : IMiniGame
{
    private const string GameId = "fling";
    private const string SurfaceId = "fling.sling";
    private const float ViewWidth = 13f;
    private const float GroundLine = 0.8f;
    private const float SlingFraction = 0.27f;
    private const float GrabRadius = 1.4f;
    private const float AimReadyDistance = 1.6f;
    private const float IntroHoldSeconds = 1.1f;
    private const float FollowSeconds = 0.14f;
    private const float PanSeconds = 0.45f;
    private const float FollowLead = 0.12f;
    private const float FlightHeadroom = 0.3f;
    private const float ResultDelaySeconds = 1.8f;
    private const float BannerSeconds = 1.4f;
    private const float HopSeconds = 0.35f;
    private const float HopHeight = 0.9f;
    private const float IdleLaunchSeconds = 3.2f;
    private const float RibbonWidth = 0.34f;
    private const float SkyStart = 0.12f;
    private const float SkySpan = 0.62f;
    private const float StrongImpact = 40f;
    private const int IdleLevel = 3;
    private const ulong IdleSeed = 0x464C494E47UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Fling.Title, GameGenre.Puzzle, L.Fling.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Level, keyboard: true, levelCount: FlingLevels.Count);
    private static readonly Vector2 IdlePull = new(-1.4f, 0.55f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 DustColor = new(0.86f, 0.80f, 0.70f, 0.55f);
    private static readonly Vector4[] ConfettiColors =
    {
        FlingRenderer.NormalBird, FlingRenderer.SplitterBird, FlingRenderer.GoblinSkin, Gold,
        new(0.95f, 0.45f, 0.55f, 1f),
    };
    private static readonly ParticleSpec WoodChips = new(FlingRenderer.Wood, FlingRenderer.Wood with { W = 0f }, 0.12f,
        4f, 0.8f, 9f, 1.2f, 10f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec GlassShards = new(White, FlingRenderer.Glass with { W = 0f }, 0.1f, 5f, 0.7f,
        8f, 1.2f, 12f, shape: ParticleShape.Shard, additive: true);
    private static readonly ParticleSpec StoneChunks = new(FlingRenderer.Stone, GamePalette.Darken(FlingRenderer.Stone,
        0.3f) with { W = 0f }, 0.13f, 3.5f, 0.9f, 10f, 1.2f, 6f, shape: ParticleShape.Square);
    private static readonly ParticleSpec Dust = new(DustColor, DustColor with { W = 0f }, 0.35f, 1.3f, 0.7f, -0.6f,
        2.2f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec GoblinPuff = new(FlingRenderer.GoblinSkin with { W = 0.75f },
        FlingRenderer.GoblinSkin with { W = 0f }, 0.45f, 1.8f, 0.6f, -0.8f, 2.2f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Stars = new(Gold, White with { W = 0f }, 0.14f, 3.4f, 0.8f, 2f, 1.8f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec Poof = new(White with { W = 0.6f }, White with { W = 0f }, 0.4f, 1f, 0.5f,
        -0.5f, 2.4f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec NormalFeathers = Feathers(FlingRenderer.NormalBird);
    private static readonly ParticleSpec SplitterFeathers = Feathers(FlingRenderer.SplitterBird);
    private static readonly ParticleSpec HeavyFeathers = Feathers(FlingRenderer.HeavyBird);
    private readonly FlingBoard board = new();
    private readonly ParticleSystem particles = new(768);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] ribbons = { new(), new(), new() };
    private readonly Vector2[] preview = new Vector2[FlingBoard.PreviewPoints];
    private Camera2D camera = Camera2D.Create();
    private LabelPairSlot goblinLabel;
    private LocString banner = L.Fling.Cleared;
    private Vector4 bannerTint = Gold;
    private Vector2 pull;
    private float bannerProgress = 1f;
    private float introHold;
    private float panOffset;
    private float panStartOffset;
    private float panStartPointer;
    private float followX;
    private float resultDelay;
    private float idleTimer;
    private float hop = 1f;
    private float time;
    private int level = 1;
    private int ribbonBirds;
    private bool dragging;
    private bool panning;
    private bool placeCamera = true;
    private bool finished;

    public FlingApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    private static float SlingViewX => FlingBoard.SlingAnchor.X + (0.5f - SlingFraction) * ViewWidth;

    public void Start(in GameStart start)
    {
        level = Math.Clamp(start.Level, 1, FlingLevels.Count);
        board.Load(FlingLevels.Get(level), start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ClearRibbons();
        camera = Camera2D.Create();
        placeCamera = true;
        introHold = IntroHoldSeconds;
        panOffset = 0f;
        followX = SlingViewX;
        pull = Vector2.Zero;
        dragging = false;
        panning = false;
        hop = 1f;
        bannerProgress = 1f;
        resultDelay = ResultDelaySeconds;
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
        time += context.RawDeltaSeconds;
        context.Backdrop.SetSky(SkyFor(IdleLevel));
        if (board.Phase == FlingPhase.Over)
        {
            BuildIdle();
        }

        PlaceCamera(context);
        idleTimer += context.RawDeltaSeconds;
        if (board.CanAim && !board.Settling && idleTimer >= IdleLaunchSeconds)
        {
            idleTimer = 0f;
            board.Launch(IdlePull + new Vector2(0f, 0.06f * (board.Shots % 3)));
        }

        board.Step(context.RawDeltaSeconds);
        board.ClearEvents();
        PushRibbons(context.RawDeltaSeconds);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        hop = MathF.Min(1f, hop + context.RawDeltaSeconds / HopSeconds);
        time += context.RawDeltaSeconds;
        context.Backdrop.SetSky(SkyFor(level));
        if (context.Session.State == StageFlow.Playing)
        {
            introHold = MathF.Max(0f, introHold - context.RawDeltaSeconds);
        }

        PlaceCamera(context);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        DrawSplitHint(drawList);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f),
            Loc.T(banner), bannerTint, context.Theme, bannerProgress);
        DrawHud(drawList, context, scale);
    }

    private static float SkyFor(int levelNumber) =>
        SkyStart + SkySpan * Math.Clamp((levelNumber - 1) / (float)(FlingLevels.Count - 1), 0f, 1f);

    private void BuildIdle()
    {
        board.Load(FlingLevels.Get(IdleLevel), GameRandom.FromSeed(IdleSeed));
        ClearRibbons();
        idleTimer = IdleLaunchSeconds * 0.6f;
        introHold = 0f;
        panOffset = 0f;
        followX = SlingViewX;
        pull = Vector2.Zero;
        dragging = false;
        placeCamera = true;
    }

    private void PlaceCamera(in GameContext context)
    {
        var full = context.Full;
        var viewHeight = ViewWidth * full.Height / MathF.Max(1f, full.Width);
        camera.Fit(full, ViewWidth, viewHeight, FitMode.CoverWidth);
        var target = CameraTarget(viewHeight);
        if (placeCamera)
        {
            placeCamera = false;
            camera.Place(target);
        }

        var smooth = board.Phase == FlingPhase.Flying && introHold <= 0f ? FollowSeconds : PanSeconds;
        camera.Follow(target, Vector2.Zero, smooth, context.RawDeltaSeconds);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private Vector2 CameraTarget(float viewHeight)
    {
        var groundY = -(GroundLine - 0.5f) * viewHeight;
        var slingX = SlingViewX;
        var structureX = MathF.Max(slingX, board.FocusX);
        if (introHold > 0f)
        {
            return new Vector2(structureX, groundY);
        }

        switch (board.Phase)
        {
            case FlingPhase.Flying when board.FlyingCount > 0:
            {
                var bird = board.RenderBirdPosition(0);
                followX = MathF.Max(slingX, bird.X + board.BirdVelocity(0).X * FollowLead);
                return new Vector2(followX, MathF.Min(groundY, bird.Y + viewHeight * FlightHeadroom));
            }
            case FlingPhase.Flying:
                return new Vector2(followX, groundY);
            case FlingPhase.Over:
                return new Vector2(structureX, groundY);
            default:
                return new Vector2(slingX + panOffset, groundY);
        }
    }

    private float MaxPan => MathF.Max(0f, board.RightEdge + 1.5f - (SlingViewX + ViewWidth * 0.5f));

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Step(deltaSeconds);
        for (var index = 0; index < board.EventCount; index++)
        {
            React(board.Event(index), context);
        }

        board.ClearEvents();
        PushRibbons(deltaSeconds);
        if (board.Phase != FlingPhase.Over)
        {
            return;
        }

        resultDelay -= context.RawDeltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Level, GameId, board.Won)
            .WithStars(board.Stars)
            .WithStat(L.Games.Score, GameNumber.Label(board.Score))
            .WithStat(L.Fling.Goblins, goblinLabel.Get(L.Fling.GoblinsCount, board.Popped, board.GoblinCount))
            .WithStat(L.Fling.BirdsUsed, GameNumber.Label(board.Shots))
            .WithStat(L.Fling.Broken, GameNumber.Label(board.Destroyed)));
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            dragging = false;
            panning = false;
            return;
        }

        var pointer = ImGui.GetMousePos();
        PressSurface.Claim(SurfaceId, context.Full, out var activated);
        var pressed = activated && !context.ChromeHit(pointer);
        var held = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (board.Phase == FlingPhase.Flying)
        {
            dragging = false;
            panning = false;
            if (pressed || GameInput.Pressed(ImGuiKey.Space))
            {
                board.Ability();
            }

            return;
        }

        var ready = board.CanAim && introHold <= 0f &&
                    MathF.Abs(camera.Origin.X - (SlingViewX + panOffset)) < AimReadyDistance;
        if (!ready)
        {
            dragging = false;
            panning = false;
            return;
        }

        var world = camera.ToWorld(pointer);
        if (pressed)
        {
            if (Vector2.Distance(world, FlingBoard.SlingAnchor) <= GrabRadius)
            {
                dragging = true;
                panOffset = 0f;
                UiFeedback.Play(UiSound.GameTick);
            }
            else
            {
                panning = true;
                panStartPointer = pointer.X;
                panStartOffset = panOffset;
            }
        }

        if (dragging)
        {
            if (held)
            {
                pull = FlingBoard.ClampPull(world - FlingBoard.SlingAnchor);
                return;
            }

            dragging = false;
            board.Launch(pull);
            pull = Vector2.Zero;
            return;
        }

        if (!panning)
        {
            return;
        }

        if (!held)
        {
            panning = false;
            return;
        }

        panOffset = Math.Clamp(panStartOffset - camera.Units(pointer.X - panStartPointer), 0f, MaxPan);
    }

    private void React(in FlingEvent entry, in GameContext context)
    {
        switch (entry.Kind)
        {
            case FlingEventKind.Launch:
                UiFeedback.Play(UiSound.GameShoot);
                particles.Emit(FeathersFor(entry.Bird), entry.Position, 8);
                camera.Shake(0.08f);
                return;
            case FlingEventKind.Split:
                UiFeedback.Play(UiSound.GamePowerUp);
                particles.Emit(SplitterFeathers, entry.Position, 16);
                fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(1.4f), FlingRenderer.SplitterBird, 0.35f, 2.5f);
                context.Fx.Flash(FlingRenderer.SplitterBird, 0.12f);
                return;
            case FlingEventKind.Impact:
                OnImpact(entry, context);
                return;
            case FlingEventKind.Crack:
                UiFeedback.Play(UiSound.GameHitWood);
                particles.Emit(DebrisFor(entry.Material), entry.Position, 3);
                return;
            case FlingEventKind.Break:
                OnBreak(entry);
                return;
            case FlingEventKind.GoblinPop:
                OnGoblinPop(entry, context);
                return;
            case FlingEventKind.BirdGone:
                particles.Emit(Poof, entry.Position, 6);
                particles.Emit(FeathersFor(entry.Bird), entry.Position, 5);
                return;
            case FlingEventKind.NextBird:
                UiFeedback.Play(UiSound.GameJump);
                hop = 0f;
                panOffset = 0f;
                return;
            case FlingEventKind.BirdBonus:
                UiFeedback.Play(UiSound.GameCollect);
                particles.Emit(Stars, entry.Position, 12);
                particles.Emit(FeathersFor(entry.Bird), entry.Position, 6);
                fx.AddText(GameNumber.Signed(entry.Value), camera.ToScreen(entry.Position + new Vector2(0f, -0.8f)), Gold,
                    1.3f);
                return;
            case FlingEventKind.Won:
                GameSfx.LevelClear();
                context.Fx.Sweep();
                particles.Confetti(new Vector2(board.FocusX, -6f), 80, ConfettiColors, 9f, 0.16f, 1.6f, 9f);
                return;
            case FlingEventKind.Lost:
                UiFeedback.Play(UiSound.GameWrong);
                ShowBanner(L.Fling.OutOfBirds, Danger);
                context.Fx.Vignette(Danger, 0.3f, 0.9f);
                return;
            default:
                return;
        }
    }

    private void OnImpact(in FlingEvent entry, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameHitWood);
        particles.Emit(Dust, entry.Position, 4);
        camera.Shake(MathF.Min(0.45f, entry.Value * 0.006f));
        if (entry.Value < StrongImpact)
        {
            return;
        }

        fx.HitStop(0.035f);
        context.Fx.Punch(0.03f);
        fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(1.2f), White with { W = 0.7f }, 0.3f, 2.4f);
    }

    private void OnBreak(in FlingEvent entry)
    {
        UiFeedback.Play(entry.Material == FlingMaterial.Stone ? UiSound.GameExplosion : UiSound.GameBreak);
        var half = board.PieceHalfExtents(entry.Index);
        var along = half.X >= half.Y ? entry.Angle : entry.Angle + MathF.PI * 0.5f;
        var axis = new Vector2(MathF.Cos(along), MathF.Sin(along));
        var reach = MathF.Max(half.X, half.Y);
        var spec = DebrisFor(entry.Material);
        for (var step = -2; step <= 2; step++)
        {
            particles.Emit(spec, entry.Position + axis * (reach * step * 0.45f), 3);
        }

        particles.Emit(Dust, entry.Position, 4);
        camera.Shake(entry.Material == FlingMaterial.Stone ? 0.3f : 0.14f);
        var tint = GamePalette.Lighten(FlingRenderer.MaterialColor(entry.Material), 0.25f);
        fx.AddText(GameNumber.Signed(entry.Value), camera.ToScreen(entry.Position), tint, 1f);
    }

    private void OnGoblinPop(in FlingEvent entry, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePop);
        var screen = camera.ToScreen(entry.Position);
        particles.Emit(GoblinPuff, entry.Position, 10);
        particles.Emit(Stars, entry.Position, 10);
        fx.Shockwave(screen, camera.Px(1.6f), FlingRenderer.GoblinSkin, 0.45f, 3f);
        fx.AddText(GameNumber.Signed(entry.Value), screen - new Vector2(0f, camera.Px(0.7f)), Gold, 1.35f);
        fx.HitStop(0.045f);
        context.Fx.Punch(0.05f);
        if (board.GoblinsLeft > 0)
        {
            return;
        }

        ShowBanner(L.Fling.Cleared, Gold);
        context.Fx.SlowMo(0.35f, 0.9f);
        context.Fx.Flash(Gold, 0.18f);
        context.Fx.Sweep();
    }

    private void ShowBanner(LocString text, Vector4 tint)
    {
        banner = text;
        bannerTint = tint;
        bannerProgress = 0f;
    }

    private void PushRibbons(float deltaSeconds)
    {
        var count = board.FlyingCount;
        if (count < ribbonBirds)
        {
            ClearRibbons();
        }
        else
        {
            for (var bird = ribbonBirds; bird < count; bird++)
            {
                ribbons[bird].Clear();
            }
        }

        ribbonBirds = count;
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var bird = 0; bird < count; bird++)
        {
            ribbons[bird].Push(board.RenderBirdPosition(bird));
        }
    }

    private void ClearRibbons()
    {
        for (var index = 0; index < ribbons.Length; index++)
        {
            ribbons[index].Clear();
        }

        ribbonBirds = 0;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        FlingRenderer.DrawGround(drawList, in camera);
        FlingRenderer.DrawTrail(drawList, in camera, board);
        DrawPieces(drawList, true);
        DrawPieces(drawList, false);
        var aiming = board.CanAim;
        var pouch = dragging ? FlingBoard.Pouch(pull) : FlingBoard.SlingAnchor;
        FlingRenderer.DrawSlingBack(drawList, in camera, pouch);
        if (aiming)
        {
            DrawLoadedBird(drawList, pouch);
        }

        FlingRenderer.DrawSlingFront(drawList, in camera, pouch);
        DrawQueue(drawList, aiming);
        DrawFlyingBirds(drawList);
        if (dragging && pull.Length() >= FlingBoard.MinPull)
        {
            board.PreviewPath(pull, preview);
            FlingRenderer.DrawPreview(drawList, in camera, preview, pull.Length() / FlingBoard.MaxPull);
        }

        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawPieces(ImDrawListPtr drawList, bool rocks)
    {
        var lookAt = board.FlyingCount > 0 ? board.RenderBirdPosition(0) : FlingBoard.SlingAnchor;
        for (var piece = 0; piece < board.PieceCount; piece++)
        {
            if (!board.PieceAlive(piece))
            {
                continue;
            }

            var kind = board.PieceKind(piece);
            var material = board.PieceMaterial(piece);
            var isRock = kind == FlingPieceKind.Block && material == FlingMaterial.Rock;
            if (isRock != rocks)
            {
                continue;
            }

            var position = board.RenderPiecePosition(piece);
            var half = board.PieceHalfExtents(piece);
            if (isRock)
            {
                FlingRenderer.DrawRock(drawList, in camera, position, half);
                continue;
            }

            if (kind == FlingPieceKind.Block)
            {
                FlingRenderer.DrawBlock(drawList, in camera, position, board.RenderPieceAngle(piece), half, material,
                    board.PieceDamage(piece), board.PieceSeed(piece));
                continue;
            }

            var bob = MathF.Sin(time * 3f + (board.PieceSeed(piece) & 63) * 0.1f);
            FlingRenderer.DrawGoblin(drawList, in camera, position, board.RenderPieceAngle(piece), half.X,
                kind == FlingPieceKind.Knight, lookAt, bob, board.PieceDamage(piece));
        }
    }

    private void DrawLoadedBird(ImDrawListPtr drawList, Vector2 pouch)
    {
        var bird = board.BirdAt(0);
        var position = pouch;
        if (hop < 1f)
        {
            var from = FlingBoard.QueuePosition(0, bird);
            position = Vector2.Lerp(from, pouch, hop) + new Vector2(0f, -HopHeight * MathF.Sin(hop * MathF.PI));
        }

        var facing = dragging && pull.LengthSquared() > 0.01f ? -pull : Vector2.UnitX;
        var stretch = dragging ? pull.Length() / FlingBoard.MaxPull : 0f;
        FlingRenderer.DrawBird(drawList, in camera, position, bird, facing, stretch);
    }

    private void DrawQueue(ImDrawListPtr drawList, bool aiming)
    {
        if (board.Phase == FlingPhase.Over && board.Won)
        {
            return;
        }

        var first = aiming ? 1 : 0;
        for (var queueIndex = first; queueIndex < board.BirdsLeft; queueIndex++)
        {
            var bird = board.BirdAt(queueIndex);
            var slot = queueIndex - first;
            var bounce = MathF.Max(0f, MathF.Sin(time * 4f + slot * 1.3f)) * 0.08f;
            FlingRenderer.DrawBird(drawList, in camera, FlingBoard.QueuePosition(slot, bird) - new Vector2(0f, bounce),
                bird, Vector2.UnitX, 0f);
        }
    }

    private void DrawFlyingBirds(ImDrawListPtr drawList)
    {
        var width = camera.Px(RibbonWidth);
        for (var bird = 0; bird < board.FlyingCount; bird++)
        {
            var kind = board.FlyingKind(bird);
            ribbons[bird].Draw(drawList, in camera, FlingRenderer.BirdColor(kind) with { W = 0.45f }, width,
                additive: true);
            FlingRenderer.DrawBird(drawList, in camera, board.RenderBirdPosition(bird), kind, board.BirdVelocity(bird),
                0f);
        }
    }

    private void DrawSplitHint(ImDrawListPtr drawList)
    {
        if (!board.AbilityReady)
        {
            return;
        }

        var bird = camera.ToScreen(board.RenderBirdPosition(0) + new Vector2(0f, -0.9f));
        var alpha = 0.55f + 0.45f * Pulse.Wave(Pulse.Fast);
        Typography.DrawCentered(drawList, bird, Loc.T(L.Fling.TapToSplit), White with { W = alpha },
            TextStyles.FootnoteEmphasized.Scale, FontWeight.Bold);
    }

    private void DrawHud(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var birdsLabel = GameNumber.Label(board.BirdsLeft);
        var goblinsLabel = GameNumber.Label(board.GoblinsLeft);
        context.Hud.Score(board.Score);
        context.Hud.Level(level);
        context.Hud.Custom(StatCapsule.Width(birdsLabel, scale));
        context.Hud.Custom(StatCapsule.Width(goblinsLabel, scale));
        if (context.Hud.CustomPlaced(0))
        {
            StatCapsule.Draw(drawList, context.Hud.CustomRect(0), FontAwesomeIcon.Feather, birdsLabel,
                board.BirdsLeft <= 1 ? Danger : FlingRenderer.NormalBird, scale);
        }

        if (context.Hud.CustomPlaced(1))
        {
            StatCapsule.Draw(drawList, context.Hud.CustomRect(1), FontAwesomeIcon.Skull, goblinsLabel,
                FlingRenderer.GoblinSkin, scale);
        }

        context.Session.Report(board.Score);
    }

    private static ParticleSpec DebrisFor(FlingMaterial material) => material switch
    {
        FlingMaterial.Glass => GlassShards,
        FlingMaterial.Stone => StoneChunks,
        _ => WoodChips,
    };

    private static ParticleSpec FeathersFor(FlingBird bird) => bird switch
    {
        FlingBird.Splitter => SplitterFeathers,
        FlingBird.Heavy => HeavyFeathers,
        _ => NormalFeathers,
    };

    private static ParticleSpec Feathers(Vector4 color) =>
        new(color, color with { W = 0f }, 0.1f, 3f, 0.9f, 2f, 2.5f, 8f, shape: ParticleShape.Shard);
}

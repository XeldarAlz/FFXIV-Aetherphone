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

namespace Aetherphone.Apps.Games.Snip;

internal sealed class SnipApp : IMiniGame
{
    private const string GameId = "snip";
    private const string BladeSurfaceId = "snip.blade";
    private const int IdleLevel = 1;
    private const ulong IdleSeed = 0x534E4950UL;
    private const float FinishDelaySeconds = 1.3f;
    private const float RestartDelaySeconds = 1f;
    private const float BannerSeconds = 1.2f;
    private const float StarFlightSeconds = 0.55f;
    private const float StarFlightGlyph = 22f;
    private const float BladeCoreWidth = 0.06f;
    private const float BladeGlowWidth = 0.2f;
    private const float IdleCutSeconds = 2.2f;
    private const float IdleLoopSeconds = 4.6f;
    private const float SlowMoRange = 1.15f;
    private const float StarFlying = 0f;
    private const float StarResting = -1f;
    private static readonly GameSpec StageSpec = new(GameId, L.Snip.Title, GameGenre.Puzzle, L.Snip.Hook,
        Backdrop.Paper, HudStyle.Standard, ScoreKind.Level, clocked: true, levelCount: SnipLevels.Count);
    private static readonly Rect WorldRect = new(Vector2.Zero, new Vector2(SnipBoard.WorldWidth, SnipBoard.WorldHeight));
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ink = new(0.22f, 0.20f, 0.24f, 1f);
    private static readonly Vector4 Danger = new(0.92f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.86f, 0.52f, 1f);
    private static readonly ParticleSpec CutSparks = new(Spark, Spark with { W = 0f }, 0.06f, 4.5f, 0.35f, 6f, 2f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec StarSparkle = new(White, GamePalette.Star with { W = 0f }, 0.11f, 3.4f, 0.75f,
        1.5f, 2.2f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec BubbleDrops = new(SnipRenderer.BubbleTint, SnipRenderer.BubbleTint with { W = 0f },
        0.07f, 4f, 0.55f, 9f, 1.4f);
    private static readonly ParticleSpec PuffCloud = new(SnipRenderer.PuffTint, SnipRenderer.PuffTint with { W = 0f }, 0.2f,
        3.2f, 0.6f, 0f, 3f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow);
    private static readonly ParticleSpec SpikeShards = new(SnipRenderer.Steel, SnipRenderer.Steel with { W = 0f }, 0.07f,
        5f, 0.5f, 8f, 1.6f, 9f, shape: ParticleShape.Shard);
    private readonly SnipBoard board = new();
    private readonly ParticleSystem particles = new(384);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon blade = new();
    private readonly float[] starFlights = new float[SnipLevel.StarCount];
    private readonly Vector2[] starOrigins = new Vector2[SnipLevel.StarCount];
    private Camera2D camera = Camera2D.Create();
    private GameRandom seed;
    private LabelPairSlot starsLabel;
    private LocString bannerText = L.Snip.Kupo;
    private Vector4 bannerColor = White;
    private Vector2 bladePoint;
    private int level = IdleLevel;
    private int attempts;
    private int totalCuts;
    private int arrivedMask;
    private float bannerProgress = 1f;
    private float endSeconds;
    private float eatProgress;
    private float idleSeconds;
    private float time;
    private bool bladeActive;
    private bool slowed;
    private bool finished;
    private bool preview;

    public SnipApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        level = Math.Clamp(start.Level, 1, SnipLevels.Count);
        seed = start.Random;
        preview = false;
        attempts = 1;
        totalCuts = 0;
        finished = false;
        bannerProgress = 1f;
        camera = Camera2D.Create();
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ResetAttempt();
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
        var deltaSeconds = context.RawDeltaSeconds;
        time += deltaSeconds;
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        idleSeconds += deltaSeconds;
        if (idleSeconds >= IdleCutSeconds && board.AnyRopeHolds())
        {
            CutAcross(0);
        }

        board.Step(deltaSeconds);
        AdvanceEnd(deltaSeconds);
        React(context, false);
        if (idleSeconds >= IdleLoopSeconds)
        {
            BuildIdle();
        }

        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        time += context.RawDeltaSeconds;
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        AdvanceStarFlights(context.RawDeltaSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(SnipBoard.WorldWidth * 0.5f, SnipBoard.WorldHeight * 0.42f)),
            Loc.T(bannerText), bannerColor, context.Theme, bannerProgress);
        FillHud(drawList, context, scale);
        context.Session.Report(board.StarCount);
    }

    private void BuildIdle()
    {
        preview = true;
        level = IdleLevel;
        seed = GameRandom.FromSeed(IdleSeed);
        idleSeconds = 0f;
        ResetAttempt();
    }

    private void ResetAttempt()
    {
        board.Load(SnipLevels.Get(level), seed);
        blade.Clear();
        bladeActive = false;
        endSeconds = 0f;
        eatProgress = 0f;
        slowed = false;
        arrivedMask = 0;
        for (var index = 0; index < starFlights.Length; index++)
        {
            starFlights[index] = StarResting;
        }
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, WorldRect, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleBlade(context);
        board.Step(deltaSeconds);
        React(context, true);
        AdvanceEnd(context.DeltaSeconds);
        if (board.State == SnipState.Lost && endSeconds >= RestartDelaySeconds)
        {
            attempts++;
            ResetAttempt();
            return;
        }

        if (board.State != SnipState.Eaten || endSeconds < FinishDelaySeconds)
        {
            return;
        }

        finished = true;
        var collected = board.StarCount;
        var stars = Math.Max(1, collected);
        context.Session.Finish(new GameOutcome(stars, ScoreKind.Level, GameId)
            .WithStars(stars)
            .WithStat(L.Snip.Stars, starsLabel.Get(L.Stage.StarsOf, collected, SnipLevel.StarCount))
            .WithStat(L.Snip.Attempts, GameNumber.Label(attempts))
            .WithStat(L.Snip.Cuts, GameNumber.Label(totalCuts))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.Elapsed)));
    }

    private void AdvanceEnd(float deltaSeconds)
    {
        if (board.State == SnipState.Playing || deltaSeconds <= 0f)
        {
            return;
        }

        endSeconds += deltaSeconds;
        eatProgress = MathF.Min(1f, eatProgress + deltaSeconds / SnipRenderer.EatSeconds);
    }

    private void HandleBlade(in GameContext context)
    {
        var mouse = ImGui.GetMousePos();
        if (context.Session.State != StageFlow.Playing || board.State != SnipState.Playing)
        {
            ReleaseBlade();
            return;
        }

        var full = context.Full;
        var surface = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * UiScale.Current), full.Max);
        PressSurface.Claim(BladeSurfaceId, surface, out var activated);
        var point = camera.ToWorld(mouse);
        if (activated && !context.ChromeHit(mouse))
        {
            bladeActive = true;
            bladePoint = point;
            blade.Clear();
            blade.Push(point);
            board.Tap(point);
        }

        if (!bladeActive || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ReleaseBlade();
            return;
        }

        if (Vector2.DistanceSquared(point, bladePoint) < 0.0004f)
        {
            return;
        }

        totalCuts += board.Cut(bladePoint, point);
        blade.Push(point);
        bladePoint = point;
    }

    private void ReleaseBlade()
    {
        if (!bladeActive)
        {
            return;
        }

        bladeActive = false;
        blade.Clear();
    }

    private void CutAcross(int ropeIndex)
    {
        var world = board.World;
        var rope = board.Rope(ropeIndex);
        var middle = world.RopeSegments(rope) / 2;
        var start = world.RopePoint(rope, middle);
        var end = world.RopePoint(rope, middle + 1);
        var along = end - start;
        if (along.LengthSquared() <= 0.000001f)
        {
            return;
        }

        var across = Vector2.Normalize(new Vector2(-along.Y, along.X)) * 0.35f;
        var center = (start + end) * 0.5f;
        board.Cut(center - across, center + across);
    }

    private void React(in GameContext context, bool live)
    {
        if (board.CutThisFrame > 0)
        {
            particles.Emit(CutSparks, board.CutPoint, 8 + board.CutThisFrame * 4);
            camera.Shake(0.05f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameHitSoft);
            }

            if (board.FreedThisFrame && live)
            {
                context.Fx.Punch(0.025f);
            }
        }

        if (board.StarsThisFrame != 0)
        {
            OnStars(context, live);
        }

        if (board.CapturedThisFrame >= 0)
        {
            fx.Shockwave(camera.ToScreen(board.CandyPosition), camera.Px(SnipBoard.BubbleRadius * 1.5f),
                SnipRenderer.BubbleTint, 0.4f, 2.5f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameHitSoft);
            }
        }

        if (board.PoppedThisFrame)
        {
            particles.Emit(BubbleDrops, board.CandyPosition, 18);
            fx.Shockwave(camera.ToScreen(board.CandyPosition), camera.Px(SnipBoard.BubbleRadius * 1.8f),
                SnipRenderer.BubbleTint, 0.35f, 2f);
            if (live)
            {
                UiFeedback.Play(UiSound.GamePop);
            }
        }

        if (board.PuffedThisFrame >= 0)
        {
            OnPuff(board.PuffedThisFrame, live);
        }

        if (board.EatenThisFrame)
        {
            OnEaten(context, live);
        }

        if (board.LostThisFrame)
        {
            OnLost(context, live);
        }

        if (!live || slowed || board.State != SnipState.Playing)
        {
            return;
        }

        var toMouth = board.Level.Moogle - board.CandyPosition;
        if (toMouth.Length() <= SlowMoRange && Vector2.Dot(toMouth, board.World.Velocity(board.Candy)) > 0.5f)
        {
            slowed = true;
            context.Fx.SlowMo(0.55f, 0.28f);
        }
    }

    private void OnStars(in GameContext context, bool live)
    {
        var stars = board.Level.Stars;
        for (var index = 0; index < stars.Length; index++)
        {
            if ((board.StarsThisFrame & (1 << index)) == 0)
            {
                continue;
            }

            var screen = camera.ToScreen(stars[index]);
            particles.Emit(StarSparkle, stars[index], 16);
            fx.Shockwave(screen, camera.Px(0.9f), GamePalette.Star, 0.45f, 3f);
            starOrigins[index] = screen;
            starFlights[index] = StarFlying;
        }

        if (!live)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameCollect);
        context.Fx.Punch(0.02f);
        if (board.StarCount < SnipLevel.StarCount)
        {
            return;
        }

        GameSfx.ComboTierUp();
        ShowBanner(L.Snip.Perfect, GamePalette.Star);
    }

    private void OnPuff(int index, bool live)
    {
        var cushion = board.Level.Cushions[index];
        var angle = MathF.Atan2(cushion.Direction.Y, cushion.Direction.X);
        var nozzle = cushion.Position + cushion.Direction * SnipBoard.CushionRadius * 0.9f;
        particles.Emit(PuffCloud.WithDirection(angle, 0.7f), nozzle, 12);
        if (board.PuffHit)
        {
            camera.Shake(0.06f);
        }

        if (live)
        {
            UiFeedback.Play(UiSound.GameJump);
        }
    }

    private void OnEaten(in GameContext context, bool live)
    {
        var mouth = board.Level.Moogle;
        var screen = camera.ToScreen(mouth);
        particles.Emit(new ParticleSpec(GamePalette.Lighten(Accent, 0.4f), Accent with { W = 0f }, 0.09f, 4.6f, 0.6f,
            7f, 1.4f, 9f, shape: ParticleShape.Shard), mouth, 18);
        particles.Emit(StarSparkle, mouth, 14);
        fx.Shockwave(screen, camera.Px(1.6f), Accent, 0.5f, 3.4f);
        if (!live)
        {
            return;
        }

        GameSfx.LevelClear();
        fx.HitStop(0.06f);
        camera.Shake(0.18f);
        context.Fx.Punch(0.08f);
        context.Fx.Sweep();
        ShowBanner(L.Snip.Kupo, Accent);
    }

    private void OnLost(in GameContext context, bool live)
    {
        if (board.Loss == SnipLoss.Spikes)
        {
            particles.Emit(new ParticleSpec(GamePalette.Lighten(Accent, 0.3f), Accent with { W = 0f }, 0.1f, 6f, 0.8f,
                9f, 1.2f, 10f, shape: ParticleShape.Shard), board.EndPosition, 22);
            particles.Emit(SpikeShards, board.EndPosition, 10);
            camera.Shake(0.55f);
        }

        if (!live)
        {
            return;
        }

        if (board.Loss == SnipLoss.Spikes)
        {
            UiFeedback.Play(UiSound.GameBreak);
            context.Fx.Flash(Danger, 0.3f);
            context.Fx.SlowMo(0.4f, 0.3f);
            ShowBanner(L.Snip.Shattered, Danger);
            return;
        }

        UiFeedback.Play(UiSound.GameWrong);
        context.Fx.Vignette(Danger, 0.3f, 0.6f);
        ShowBanner(L.Snip.Missed, Danger);
    }

    private void ShowBanner(LocString text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void AdvanceStarFlights(float deltaSeconds)
    {
        for (var index = 0; index < starFlights.Length; index++)
        {
            if (starFlights[index] < StarFlying)
            {
                continue;
            }

            starFlights[index] += deltaSeconds / StarFlightSeconds;
            if (starFlights[index] < 1f)
            {
                continue;
            }

            starFlights[index] = StarResting;
            arrivedMask |= 1 << index;
        }
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        var level = board.Level;
        SnipRenderer.DrawSpikes(drawList, in camera, level);
        SnipRenderer.DrawCushions(drawList, in camera, level, board);
        SnipRenderer.DrawFreeBubbles(drawList, in camera, level, board, time);
        SnipRenderer.DrawRopes(drawList, in camera, board);
        SnipRenderer.DrawStars(drawList, in camera, level, board, time);
        var chomp = board.State == SnipState.Eaten ? MathF.Max(0.01f, eatProgress) : 0f;
        SnipRenderer.DrawMoogle(drawList, in camera, level.Moogle, board.MouthOpen, chomp, board.Blinking, time);
        DrawCandy(drawList);
        particles.Draw(drawList, in camera);
        DrawBlade(drawList);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawCandy(ImDrawListPtr drawList)
    {
        var radius = camera.Px(SnipBoard.CandyRadius);
        switch (board.State)
        {
            case SnipState.Eaten:
            {
                var travel = Easing.EaseOutCubic(eatProgress);
                var position = Vector2.Lerp(board.EndPosition, board.Level.Moogle, travel);
                SnipRenderer.DrawCandy(drawList, camera.ToScreen(position), radius * (1f - travel), time * 6f, Accent,
                    1f - travel * 0.5f);
                return;
            }
            case SnipState.Lost when board.Loss == SnipLoss.Spikes:
                return;
            default:
            {
                var position = board.CandyRenderPosition;
                if (board.InBubble)
                {
                    SnipRenderer.DrawCandy(drawList, camera.ToScreen(position), radius, board.CandyAngle, Accent, 1f);
                    SnipRenderer.DrawHeldBubble(drawList, in camera, position, time);
                    return;
                }

                SnipRenderer.DrawCandy(drawList, camera.ToScreen(position), radius, board.CandyAngle, Accent, 1f);
                return;
            }
        }
    }

    private void DrawBlade(ImDrawListPtr drawList)
    {
        if (blade.Count < 2 || preview)
        {
            return;
        }

        blade.Draw(drawList, in camera, GamePalette.Lighten(Accent, 0.3f) with { W = 0.5f }, camera.Px(BladeGlowWidth),
            additive: true);
        blade.Draw(drawList, in camera, Ink with { W = 0.85f }, camera.Px(BladeCoreWidth));
    }

    private void FillHud(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var hud = context.Hud;
        hud.Score(level, L.Games.Level);
        hud.Custom(SnipRenderer.StarCapsuleWidth);
        if (!hud.CustomPlaced(0))
        {
            return;
        }

        var rect = hud.CustomRect(0);
        SnipRenderer.DrawStarCapsule(drawList, rect, BitCount(arrivedMask), scale);
        for (var index = 0; index < starFlights.Length; index++)
        {
            var flight = starFlights[index];
            if (flight < StarFlying)
            {
                continue;
            }

            var eased = Easing.EaseOutCubic(flight);
            var target = SnipRenderer.CapsuleStarCenter(rect, BitCount(arrivedMask), scale);
            var arc = new Vector2(0f, -MathF.Sin(flight * MathF.PI) * 60f * scale);
            var position = Vector2.Lerp(starOrigins[index], target, eased) + arc;
            var size = StarFlightGlyph * scale * (1.2f - 0.6f * eased);
            ProgressRing.Glow(position, size, GamePalette.Star, 0.5f);
            ProgressRing.CenterIcon(drawList, position, FontAwesomeIcon.Star, GamePalette.Star, size);
        }
    }

    private static int BitCount(int mask) => BitOperations.PopCount((uint)mask);
}

using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Fuse;

internal sealed class FuseApp : IMiniGame
{
    private const string GameId = "fuse";
    private const float SpectateSpeed = 2.5f;
    private const float RoundBannerSeconds = 1.4f;
    private const float EndBannerSeconds = 2f;
    private const float PadGap = 6f;
    private const float WickSparkRate = 18f;
    private const float FuseTickWindow = 1f;
    private const float RevealedAge = 10f;
    private const ulong IdleSeed = 0x4655534555UL;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Hard };
    private static readonly FuseSkill[] ModeSkills = { FuseSkill.Easy, FuseSkill.Hard };
    private static readonly GameSpec StageSpec = new(GameId, L.Fuse.Title, GameGenre.Action, L.Fuse.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Streak, Modes, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 AccentColor = AppAccents.For(GameId);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Smoke = new(0.36f, 0.34f, 0.33f, 0.55f);
    private static readonly Vector4 Dust = new(0.86f, 0.78f, 0.6f, 0.7f);
    private static readonly Vector2 Wick = new(0.3f, -0.44f);
    private static readonly Vector4[] Celebration =
    {
        FuseArt.Gold, FuseArt.Flame, GameSeats.Color(0), GameSeats.Color(1), GameSeats.Color(2), FuseArt.White,
    };

    private static readonly ParticleSpec WickSpark = new(FuseArt.Spark, FuseArt.Flame with { W = 0f }, 0.05f, 1.6f, 0.35f,
        -1.5f, 2f, 0f, 1.8f, -MathF.PI * 0.5f, ParticleShape.Spark, SizeCurve.Shrink, true);
    private static readonly ParticleSpec Ember = new(FuseArt.Spark, FuseArt.Flame with { W = 0f }, 0.11f, 5f, 0.55f, 0f, 3f,
        shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec SmokePuff = new(Smoke, Smoke with { W = 0f }, 0.3f, 1.2f, 0.9f, -1.5f, 1.5f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec BlastRing = new(FuseArt.White, FuseArt.Spark with { W = 0f }, 0.5f, 0f, 0.35f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow, additive: true);
    private static readonly ParticleSpec CrateShard = new(FuseRenderer.CrateTop, FuseRenderer.CrateFront, 0.16f, 4.5f, 0.8f,
        14f, 1.2f, 14f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec CrateChip = new(FuseRenderer.CrateFront, FuseRenderer.CrateFront with { W = 0f }, 0.08f,
        5f, 0.7f, 16f, 1f, 10f, shape: ParticleShape.Square);
    private static readonly ParticleSpec DustPuff = new(Dust, Dust with { W = 0f }, 0.2f, 2.4f, 0.6f, 0f, 3f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec KnockoutPuff = new(FuseArt.White with { W = 0.85f }, FuseArt.White with { W = 0f },
        0.32f, 2.6f, 0.8f, -1f, 2.4f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec SlideTrail = new(FuseArt.White with { W = 0.6f }, FuseArt.White with { W = 0f }, 0.08f,
        0.6f, 0.3f, 0f, 2f, shape: ParticleShape.Spark);

    private readonly FuseBoard board = new();
    private readonly FuseBoard idleBoard = new();
    private readonly FusePad pad = new();
    private readonly ParticleSystem particles = new(900);
    private readonly FeedbackFx fx = new();
    private readonly Emitter[] wicks = new Emitter[FuseBoard.BombCapacity];
    private readonly float[] itemAges = new float[FuseBoard.CellCount];
    private readonly float[] idleItemAges = new float[FuseBoard.CellCount];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot roundLabel;
    private LabelSlot chainLabel;
    private string bannerText = string.Empty;
    private float bannerProgress = 1f;
    private float bannerLifetime = RoundBannerSeconds;
    private float time;
    private int landedBlocks;
    private FuseDirection lastKey;
    private bool finished;
    private bool idleReady;

    public FuseApp()
    {
        for (var slot = 0; slot < wicks.Length; slot++)
        {
            wicks[slot] = new Emitter(in WickSpark, WickSparkRate);
        }

        Array.Fill(idleItemAges, RevealedAge);
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AccentColor;

    public void Start(in GameStart start)
    {
        var mode = StageSpec.ClampMode(start.Mode);
        board.Reset(start.Random, ModeSkills[mode]);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        pad.Release();
        Array.Fill(itemAges, RevealedAge);
        for (var slot = 0; slot < wicks.Length; slot++)
        {
            wicks[slot].Reset();
        }

        lastKey = FuseDirection.None;
        landedBlocks = 0;
        bannerProgress = 1f;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        pad.Release();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.Phase == FusePhase.MatchOver)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed), FuseSkill.Hard, true);
            idleReady = true;
        }

        var scale = UiScale.Current;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        idleBoard.Step(context.RawDeltaSeconds);
        FuseRenderer.Draw(ImGui.GetWindowDrawList(), idleBoard, in camera, idleItemAges, time, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        time += raw;
        PlaceCamera(context, scale);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!board.PlayerAlive && board.Phase == FusePhase.Fighting)
        {
            simDelta *= SpectateSpeed;
        }

        if (!finished)
        {
            HandleInput(context, raw);
            board.Step(simDelta);
            ReactToEvents(context);
            EmitWicks(simDelta);
        }

        if (board.SuddenDeath && board.Phase == FusePhase.Fighting && simDelta > 0f)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.1f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        AgeItems(raw);
        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, bannerLifetime);
        FuseRenderer.Draw(drawList, board, in camera, itemAges, time, scale);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var arena = camera.ToScreen(new Vector2(FuseBoard.Columns * 0.5f, FuseBoard.Rows * 0.5f));
        GameBanner.Draw(drawList, arena, bannerText, Accent, context.Theme, bannerProgress);
        pad.Draw(drawList, in board.MoogleAt(FuseBoard.Player), Accent, context.Theme, scale, time);
        DrawHud(drawList, context, scale);
        if (board.Phase != FusePhase.MatchOver || finished)
        {
            return;
        }

        finished = true;
        Finish(context);
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        var safe = context.Safe;
        var bottom = MathF.Max(safe.Min.Y, band.Min.Y - PadGap * scale);
        camera.Fit(new Rect(safe.Min, new Vector2(safe.Max.X, bottom)), FuseRenderer.WorldRect, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
        pad.Layout(band, scale);
    }

    private void HandleInput(in GameContext context, float deltaSeconds)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            pad.Release();
            board.SetPlayerInput(FuseDirection.None, false);
            return;
        }

        pad.Update(deltaSeconds);
        var direction = pad.Direction != FuseDirection.None ? pad.Direction : KeyDirection();
        var bomb = pad.BombPressed || GameInput.Pressed(ImGuiKey.Space);
        board.SetPlayerInput(direction, bomb);
    }

    private FuseDirection KeyDirection()
    {
        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            lastKey = FuseDirection.Up;
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            lastKey = FuseDirection.Down;
        }

        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            lastKey = FuseDirection.Left;
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            lastKey = FuseDirection.Right;
        }

        if (lastKey != FuseDirection.None && KeyHeld(lastKey))
        {
            return lastKey;
        }

        var directions = FuseBoard.Directions;
        for (var index = 0; index < directions.Length; index++)
        {
            if (!KeyHeld(directions[index]))
            {
                continue;
            }

            lastKey = directions[index];
            return lastKey;
        }

        lastKey = FuseDirection.None;
        return FuseDirection.None;
    }

    private static bool KeyHeld(FuseDirection direction) => direction switch
    {
        FuseDirection.Up => GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow),
        FuseDirection.Down => GameInput.Held(ImGuiKey.S, ImGuiKey.DownArrow),
        FuseDirection.Left => GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow),
        FuseDirection.Right => GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow),
        _ => false,
    };

    private void AgeItems(float deltaSeconds)
    {
        for (var cell = 0; cell < itemAges.Length; cell++)
        {
            if (itemAges[cell] < RevealedAge)
            {
                itemAges[cell] += deltaSeconds;
            }
        }
    }

    private void EmitWicks(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var slot = 0; slot < FuseBoard.BombCapacity; slot++)
        {
            ref readonly var bomb = ref board.BombAt(slot);
            if (!bomb.Alive)
            {
                wicks[slot].Reset();
                continue;
            }

            wicks[slot].Advance(deltaSeconds, bomb.Position + Wick, particles);
            if (bomb.Sliding != FuseDirection.None)
            {
                var back = -FuseBoard.Vector(bomb.Sliding);
                particles.Emit(SlideTrail.WithDirection(MathF.Atan2(back.Y, back.X), 0.9f), bomb.Position + back * 0.3f, 1);
            }
        }
    }

    private void ReactToEvents(in GameContext context)
    {
        ReactToBombs();
        ReactToBlasts(context);
        ReactToCrates();
        ReactToItems();
        ReactToKnockouts(context);
        ReactToSuddenDeath(context);
        ReactToRounds(context);
    }

    private void ReactToBombs()
    {
        for (var index = 0; index < board.PlacedBombs.Count; index++)
        {
            if (board.PlacedBombs.Owner(index) != FuseBoard.Player)
            {
                continue;
            }

            UiFeedback.Play(UiSound.GamePiece);
            pad.Flash();
            particles.Emit(DustPuff, FuseBoard.CellCenter(board.PlacedBombs.Cell(index)) + new Vector2(0f, 0.3f), 4);
        }

        for (var index = 0; index < board.KickedBombs.Count; index++)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            particles.Emit(DustPuff, FuseBoard.CellCenter(board.KickedBombs.Cell(index)) + new Vector2(0f, 0.3f), 5);
            camera.Shake(0.08f);
        }

        for (var index = 0; index < board.BombTicks.Count; index++)
        {
            if (board.BombTicks.Owner(index) != FuseBoard.Player)
            {
                continue;
            }

            var slot = board.BombIndexAt(board.BombTicks.Cell(index));
            if (slot >= 0 && board.BombAt(slot).Fuse <= FuseTickWindow)
            {
                UiFeedback.Play(UiSound.GameTick);
                return;
            }
        }
    }

    private void ReactToBlasts(in GameContext context)
    {
        if (board.BlastCount == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameExplosion);
        context.Fx.Punch(MathF.Min(0.07f, 0.025f + 0.012f * (board.BlastCount - 1)));
        var deepest = 0;
        var deepestCell = -1;
        for (var index = 0; index < board.BlastCount; index++)
        {
            var blast = board.BlastAt(index);
            var origin = FuseBoard.CellCenter(blast.Cell);
            particles.Emit(BlastRing, origin, 1);
            particles.Emit(Ember, origin, 10);
            particles.Emit(SmokePuff, origin, 4);
            var reach = 0;
            var directions = FuseBoard.Directions;
            for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                var direction = directions[directionIndex];
                var length = blast.Length(direction);
                reach = Math.Max(reach, length);
                var step = FuseBoard.Vector(direction);
                var angle = MathF.Atan2(step.Y, step.X);
                var spec = Ember.WithDirection(angle, 0.7f);
                for (var cell = 1; cell <= length; cell++)
                {
                    particles.Emit(spec, origin + step * cell, 2);
                    if (cell == length)
                    {
                        particles.Emit(SmokePuff, origin + step * cell, 2);
                    }
                }
            }

            fx.Shockwave(camera.ToScreen(origin), camera.Px(1.2f + reach * 0.45f), FuseArt.Spark with { W = 0.6f }, 0.4f, 3f);
            camera.Shake(0.14f + 0.12f * blast.Chain);
            if (blast.Chain < deepest || blast.Chain == 0)
            {
                continue;
            }

            deepest = blast.Chain;
            deepestCell = blast.Cell;
        }

        if (deepestCell < 0)
        {
            return;
        }

        fx.AddText(chainLabel.Get(L.Fuse.Chain, deepest + 1), camera.ToScreen(FuseBoard.CellCenter(deepestCell)),
            FuseArt.Spark, 1.2f);
        context.Fx.Flash(FuseArt.Spark, 0.08f + 0.04f * deepest);
    }

    private void ReactToCrates()
    {
        if (board.BrokenCrates.Count > 0)
        {
            UiFeedback.Play(UiSound.GameBreak);
        }

        for (var index = 0; index < board.BrokenCrates.Count; index++)
        {
            var center = FuseBoard.CellCenter(board.BrokenCrates.Cell(index)) - new Vector2(0f, 0.15f);
            particles.Emit(CrateShard, center, 7);
            particles.Emit(CrateChip, center, 5);
            particles.Emit(DustPuff, center, 3);
        }
    }

    private void ReactToItems()
    {
        if (board.RevealedItems.Count > 0)
        {
            UiFeedback.Play(UiSound.GamePop);
        }

        for (var index = 0; index < board.RevealedItems.Count; index++)
        {
            var cell = board.RevealedItems.Cell(index);
            itemAges[cell] = 0f;
            var color = FuseArt.PowerUpColor(board.ItemAt(cell));
            var center = FuseBoard.CellCenter(cell);
            particles.Sparkle(center, 8, color, 2.5f, 0.1f, 0.6f, 0f);
            particles.Emit(new ParticleSpec(color, color with { W = 0f }, 0.4f, 0f, 0.4f, shape: ParticleShape.Ring,
                curve: SizeCurve.Grow, additive: true), center, 1);
        }

        for (var index = 0; index < board.BurnedItems.Count; index++)
        {
            particles.Emit(SmokePuff, FuseBoard.CellCenter(board.BurnedItems.Cell(index)), 4);
        }

        for (var index = 0; index < board.PickupCount; index++)
        {
            var pickup = board.PickupAt(index);
            var color = FuseArt.PowerUpColor(pickup.Kind);
            particles.Sparkle(pickup.Position, 12, color, 3f, 0.12f, 0.7f, 0f);
            particles.Emit(new ParticleSpec(color, color with { W = 0f }, 0.5f, 0f, 0.45f, shape: ParticleShape.Ring,
                curve: SizeCurve.Grow, additive: true), pickup.Position, 1);
            if (pickup.Moogle != FuseBoard.Player)
            {
                continue;
            }

            UiFeedback.Play(UiSound.GamePowerUp);
            fx.AddText(Loc.T(PickupLabel(pickup.Kind)), camera.ToScreen(pickup.Position - new Vector2(0f, 0.6f)), color, 1.1f);
        }
    }

    private static LocString PickupLabel(PowerUp kind) => kind switch
    {
        PowerUp.ExtraBomb => L.Fuse.ExtraBomb,
        PowerUp.Range => L.Fuse.BiggerBlast,
        PowerUp.Speed => L.Fuse.SpeedUp,
        _ => L.Fuse.Kick,
    };

    private void ReactToKnockouts(in GameContext context)
    {
        for (var index = 0; index < board.KnockoutCount; index++)
        {
            var knockout = board.KnockoutAt(index);
            var team = FuseRenderer.TeamColor(knockout.Moogle);
            var position = knockout.Position - new Vector2(0f, 0.3f);
            var screen = camera.ToScreen(position);
            particles.Emit(KnockoutPuff, position, 14);
            particles.Sparkle(position, 10, team, 3.5f, 0.12f, 0.7f, 0f);
            particles.Emit(new ParticleSpec(team, team with { W = 0f }, 0.6f, 0f, 0.45f, shape: ParticleShape.Ring,
                curve: SizeCurve.Grow, additive: true), position, 1);
            if (knockout.Crushed)
            {
                particles.Emit(DustPuff, position, 8);
            }

            if (knockout.Moogle == FuseBoard.Player)
            {
                UiFeedback.Play(UiSound.GameWrong);
                camera.Shake(0.7f);
                fx.HitStop(0.08f);
                context.Fx.Flash(Danger, 0.35f);
                context.Fx.Vignette(Danger, 0.45f, 1f);
                context.Fx.SlowMo(0.35f, 0.6f);
                ShowBanner(Loc.T(L.Fuse.KnockedOut), RoundBannerSeconds);
                continue;
            }

            UiFeedback.Play(UiSound.GameHitSoft);
            camera.Shake(0.35f);
            if (knockout.Killer != FuseBoard.Player)
            {
                continue;
            }

            UiFeedback.Play(UiSound.GameCollect);
            fx.AddText(Loc.T(L.Fuse.Knockout), screen - new Vector2(0f, camera.Px(0.4f)), FuseArt.Gold, 1.4f);
            context.Fx.Flash(FuseArt.Gold, 0.1f);
            context.Fx.Punch(0.05f);
        }
    }

    private void ReactToSuddenDeath(in GameContext context)
    {
        if (board.SuddenDeathStartedThisFrame)
        {
            UiFeedback.Play(UiSound.GameTick);
            context.Fx.Flash(Danger, 0.15f);
            ShowBanner(Loc.T(L.Fuse.SuddenDeath), RoundBannerSeconds);
        }

        for (var index = 0; index < board.LandedBlocks.Count; index++)
        {
            var cell = board.LandedBlocks.Cell(index);
            particles.Emit(DustPuff, FuseBoard.CellCenter(cell) + new Vector2(0f, 0.3f), 6);
            camera.Shake(0.06f);
            landedBlocks++;
            if (landedBlocks % 2 == 0)
            {
                UiFeedback.Play(UiSound.GameHitWood);
            }
        }
    }

    private void ReactToRounds(in GameContext context)
    {
        if (board.RoundStartedThisFrame && board.Round > 1)
        {
            UiFeedback.Play(UiSound.GameTick);
            Array.Fill(itemAges, RevealedAge);
            landedBlocks = 0;
            ShowBanner(board.MatchPoint ? Loc.T(L.Fuse.MatchPoint) : roundLabel.Get(L.Fuse.RoundNumber, board.Round),
                RoundBannerSeconds);
        }

        if (board.FightStartedThisFrame)
        {
            GameSfx.CountdownTick();
            ShowBanner(Loc.T(L.Fuse.Fight), RoundBannerSeconds * 0.7f);
        }

        if (board.RoundEndedThisFrame)
        {
            OnRoundEnd(context);
        }
    }

    private void OnRoundEnd(in GameContext context)
    {
        context.Fx.SlowMo(0.35f, 0.8f);
        context.Fx.Punch(0.06f);
        var center = new Vector2(FuseBoard.Columns * 0.5f, FuseBoard.Rows * 0.25f);
        switch (board.Verdict)
        {
            case FuseVerdict.Won:
                GameSfx.NewBest();
                context.Fx.Sweep();
                particles.Confetti(center, 90, Celebration, 14f, 0.22f, 1.6f, 18f);
                ShowBanner(Loc.T(L.Games.YouWin), EndBannerSeconds);
                return;
            case FuseVerdict.Lost:
                UiFeedback.Play(UiSound.GameWrong);
                ShowBanner(Loc.T(L.Games.Lose), EndBannerSeconds);
                return;
            case FuseVerdict.Drawn:
                ShowBanner(Loc.T(L.Games.Draw), EndBannerSeconds);
                return;
            default:
                break;
        }

        if (board.LastWinner == FuseBoard.Player)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            context.Fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.12f);
            particles.Confetti(center, 40, Celebration, 11f, 0.18f, 1.2f, 18f);
            ShowBanner(Loc.T(L.Fuse.RoundWon), EndBannerSeconds);
            return;
        }

        if (board.LastWinner == FuseBoard.NoMoogle)
        {
            ShowBanner(Loc.T(L.Games.Draw), EndBannerSeconds);
            return;
        }

        UiFeedback.Play(UiSound.GameWrong);
        ShowBanner(Loc.T(L.Fuse.RoundLost), EndBannerSeconds);
    }

    private void ShowBanner(string text, float lifetime)
    {
        bannerText = text;
        bannerLifetime = lifetime;
        bannerProgress = 0f;
    }

    private void DrawHud(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var wins = board.MoogleAt(FuseBoard.Player).Wins;
        context.Hud.Score(wins, L.Fuse.Wins);
        context.Hud.Timer(board.TimeLeft, FuseBoard.RoundSeconds, board.SuddenDeath);
        context.Hud.Custom(FuseRenderer.ScoreboardWidth);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(wins);
        if (!context.Hud.CustomPlaced(0))
        {
            return;
        }

        FuseRenderer.DrawScoreboard(drawList, context.Hud.CustomRect(0), board, scale);
    }

    private void Finish(in GameContext context)
    {
        var outcome = board.Verdict == FuseVerdict.Drawn
            ? GameOutcome.Drawn(GameId)
            : new GameOutcome(0, ScoreKind.Streak, GameId, board.Verdict == FuseVerdict.Won);
        context.Session.Finish(outcome
            .WithStat(L.Fuse.RoundsWon, GameNumber.Label(board.MoogleAt(FuseBoard.Player).Wins))
            .WithStat(L.Fuse.Knockouts, GameNumber.Label(board.PlayerKnockouts))
            .WithStat(L.Fuse.Crates, GameNumber.Label(board.PlayerCrates))
            .WithStat(L.Fuse.PowerUps, GameNumber.Label(board.PlayerPowerUps)));
    }
}

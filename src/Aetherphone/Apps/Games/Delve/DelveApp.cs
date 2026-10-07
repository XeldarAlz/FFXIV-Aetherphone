using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Delve;

internal sealed class DelveApp : IMiniGame
{
    private const string GameId = "delve";
    private const string SurfaceId = "delve.cave";
    private const float VisibleColumns = 13f;
    private const float VisibleRows = 19f;
    private const float FollowSeconds = 0.22f;
    private const float StickThreshold = 16f;
    private const float UrgentSeconds = 15f;
    private const int TickingSeconds = 10;
    private const float EndDelay = 1.5f;
    private const float BannerSeconds = 1.7f;
    private const float SquashDecay = 5f;
    private const float FlashDecay = 1.2f;
    private const ulong PreviewSeed = 11;
    private static readonly GameSpec StageSpec = new(GameId, L.Delve.Title, GameGenre.Action, L.Delve.Hook,
        Backdrop.Cavern, HudStyle.Standard, ScoreKind.Level, clocked: true, countdown: true, keyboard: true,
        levelCount: DelveLevels.Count);
    private static readonly ImGuiKey[] Keys = { ImGuiKey.W, ImGuiKey.D, ImGuiKey.S, ImGuiKey.A };
    private static readonly ImGuiKey[] Arrows =
    {
        ImGuiKey.UpArrow, ImGuiKey.RightArrow, ImGuiKey.DownArrow, ImGuiKey.LeftArrow,
    };
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Muted = new(0.72f, 0.74f, 0.80f, 0.7f);
    private static readonly Vector4 DustColor = new(0.62f, 0.58f, 0.52f, 0.6f);
    private static readonly Vector4[] ClearPalette =
    {
        DelveRenderer.GemCore, DelveRenderer.GemLight, DelveRenderer.Flame, new(0.58f, 0.44f, 0.82f, 1f),
        new(0.98f, 0.74f, 0.22f, 1f),
    };
    private static readonly ParticleSpec DirtBits = new(DelveRenderer.DirtSpeck,
        DelveRenderer.DirtSpeck with { W = 0f }, 0.07f, 2.4f, 0.4f, 9f, 2f, 6f, shape: ParticleShape.Square);
    private static readonly ParticleSpec Dust = new(DustColor, DustColor with { W = 0f }, 0.2f, 1.6f, 0.55f, -0.5f,
        3f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec GemSparkle = new(DelveRenderer.GemLight,
        DelveRenderer.GemCore with { W = 0f }, 0.13f, 3.2f, 0.65f, 1.5f, 2.4f, 6f, shape: ParticleShape.Star,
        additive: true);
    private static readonly ParticleSpec Embers = new(DelveRenderer.Flame, DelveRenderer.Ember with { W = 0f },
        0.18f, 6.5f, 0.75f, 4f, 1.8f, shape: ParticleShape.GlowCircle);
    private static readonly ParticleSpec Shards = new(DelveRenderer.Rock, DelveRenderer.Rock with { W = 0f }, 0.15f,
        7.5f, 0.8f, 14f, 1.2f, 9f, shape: ParticleShape.Shard);

    private readonly DelveBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private LabelSlot goalLabel;
    private LabelPairSlot quotaLabel;
    private Vector2 stickOrigin;
    private Vector2 focus;
    private string bannerText = string.Empty;
    private Vector4 bannerTint;
    private float banner = 1f;
    private float squash;
    private float exitFlash;
    private float clock;
    private float endTimer;
    private int level = 1;
    private int previewLevel;
    private int preferred = DelveBoard.NoDirection;
    private int stickDirection = DelveBoard.NoDirection;
    private int lastSecond;
    private int crushedFoes;
    private DelveEventKind hazard = DelveEventKind.Crushed;
    private bool stickActive;
    private bool stickMoved;
    private bool snapCamera = true;
    private bool goalShown;
    private bool ending;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        Load(Math.Max(1, start.Level), start.Random);
    }

    public void Close()
    {
        previewLevel = 0;
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var levelNumber = Math.Max(1, context.Session.Level);
        if (levelNumber != previewLevel || board.TimeLeft < 5f)
        {
            Load(levelNumber, GameRandom.FromSeed(PreviewSeed));
        }

        var scale = UiScale.Current;
        clock += context.RawDeltaSeconds;
        board.Hold(DelveBoard.NoDirection);
        board.Step(context.RawDeltaSeconds);
        PlaceCamera(context, scale);
        DrawWorld(ImGui.GetWindowDrawList(), context);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        clock += raw;
        particles.Update(raw);
        fx.Update(raw);
        banner = GameBanner.Advance(banner, raw, BannerSeconds);
        squash = MathF.Max(0f, squash - raw * SquashDecay);
        exitFlash = MathF.Max(0f, exitFlash - raw * FlashDecay);
        PlaceCamera(context, scale);
        var playing = context.Session.State == StageFlow.Playing && !ending;
        if (playing)
        {
            ShowGoal();
            HandleInput(context, scale);
        }
        else
        {
            board.Hold(DelveBoard.NoDirection);
            stickActive = false;
        }

        if (!finished)
        {
            board.Step(context.DeltaSeconds);
            ReadEvents(context, scale);
            WarnTime(context);
        }

        if (ending && !finished)
        {
            AdvanceEnd(context);
        }

        DrawWorld(drawList, context);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Safe.Min.Y + context.Safe.Height * 0.3f),
            bannerText, bannerTint, context.Theme, banner);
        DrawHud(context, drawList, scale);
    }

    private void Load(int levelNumber, GameRandom random)
    {
        level = levelNumber;
        previewLevel = levelNumber;
        board.Load(DelveLevels.Get(levelNumber), random);
        particles.Reseed(random.NextUInt());
        particles.Clear();
        fx.Clear();
        camera = Camera2D.Create();
        snapCamera = true;
        banner = 1f;
        squash = 0f;
        exitFlash = 0f;
        endTimer = 0f;
        preferred = DelveBoard.NoDirection;
        stickDirection = DelveBoard.NoDirection;
        stickActive = false;
        lastSecond = int.MaxValue;
        crushedFoes = 0;
        hazard = DelveEventKind.Crushed;
        goalShown = false;
        ending = false;
        finished = false;
        focus = PlayerCenter();
    }

    private Vector2 PlayerCenter()
    {
        var position = new Vector2(board.PlayerColumn + 0.5f, board.PlayerRow + 0.5f);
        var arrived = board.ArrivedFrom(board.PlayerCell);
        if (board.TileAt(board.PlayerCell) == DelveTile.Player && arrived >= 0)
        {
            position -= DelveBoard.Step(arrived) * (1f - board.Alpha);
        }

        return position;
    }

    private Vector2 CellCenter(int cell) => new(cell % board.Columns + 0.5f, cell / board.Columns + 0.5f);

    private static Rect PlayView(in GameContext context, float scale) =>
        new(new Vector2(context.Full.Min.X, context.Full.Min.Y + StageLayout.SafeTopStandard * scale),
            context.Full.Max);

    private void PlaceCamera(in GameContext context, float scale)
    {
        var view = PlayView(context, scale);
        camera.Fit(view, MathF.Min(VisibleColumns, board.Columns), MathF.Min(VisibleRows, board.Rows),
            FitMode.Contain);
        if (board.TileAt(board.PlayerCell) == DelveTile.Player)
        {
            focus = PlayerCenter();
        }

        var target = Clamp(focus, view);
        if (snapCamera)
        {
            camera.Place(target);
            snapCamera = false;
        }
        else
        {
            camera.Follow(target, Vector2.Zero, FollowSeconds, context.RawDeltaSeconds);
        }

        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private Vector2 Clamp(Vector2 target, Rect view)
    {
        var halfWidth = view.Width / MathF.Max(0.0001f, camera.Zoom) * 0.5f;
        var halfHeight = view.Height / MathF.Max(0.0001f, camera.Zoom) * 0.5f;
        var x = board.Columns <= halfWidth * 2f ? board.Columns * 0.5f : Math.Clamp(target.X, halfWidth, board.Columns - halfWidth);
        var y = board.Rows <= halfHeight * 2f ? board.Rows * 0.5f : Math.Clamp(target.Y, halfHeight, board.Rows - halfHeight);
        return new Vector2(x, y);
    }

    private void ShowGoal()
    {
        if (goalShown)
        {
            return;
        }

        goalShown = true;
        ShowBanner(goalLabel.Get(L.Delve.Goal, board.Quota), Accent);
    }

    private void HandleInput(in GameContext context, float scale)
    {
        var keyDirection = KeyDirection();
        var stick = StickDirection(context, scale);
        board.Hold(keyDirection != DelveBoard.NoDirection ? keyDirection : stick);
    }

    private int KeyDirection()
    {
        for (var direction = 0; direction < Keys.Length; direction++)
        {
            if (!GameInput.Pressed(Keys[direction], Arrows[direction]))
            {
                continue;
            }

            preferred = direction;
            board.Nudge(direction);
        }

        if (preferred != DelveBoard.NoDirection && GameInput.Held(Keys[preferred], Arrows[preferred]))
        {
            return preferred;
        }

        for (var direction = 0; direction < Keys.Length; direction++)
        {
            if (GameInput.Held(Keys[direction], Arrows[direction]))
            {
                preferred = direction;
                return direction;
            }
        }

        preferred = DelveBoard.NoDirection;
        return DelveBoard.NoDirection;
    }

    private int StickDirection(in GameContext context, float scale)
    {
        PressSurface.Claim(SurfaceId, PlayView(context, scale), out var activated);
        var mouse = ImGui.GetMousePos();
        if (activated && !context.ChromeHit(mouse))
        {
            stickActive = true;
            stickMoved = false;
            stickOrigin = mouse;
            stickDirection = DelveBoard.NoDirection;
            return DelveBoard.NoDirection;
        }

        if (!stickActive)
        {
            return DelveBoard.NoDirection;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            stickActive = false;
            if (!stickMoved)
            {
                Tap(mouse);
            }

            return DelveBoard.NoDirection;
        }

        var delta = mouse - stickOrigin;
        var threshold = StickThreshold * scale;
        if (MathF.Abs(delta.X) < threshold && MathF.Abs(delta.Y) < threshold)
        {
            return stickDirection;
        }

        stickMoved = true;
        stickDirection = MathF.Abs(delta.X) > MathF.Abs(delta.Y) ? (delta.X > 0f ? 1 : 3) : (delta.Y > 0f ? 2 : 0);
        var reach = delta.Length();
        if (reach > threshold * 2f)
        {
            stickOrigin = mouse - delta / reach * threshold * 2f;
        }

        return stickDirection;
    }

    private void Tap(Vector2 mouse)
    {
        var delta = mouse - camera.ToScreen(PlayerCenter());
        if (MathF.Abs(delta.X) < camera.Px(0.5f) && MathF.Abs(delta.Y) < camera.Px(0.5f))
        {
            return;
        }

        board.Nudge(MathF.Abs(delta.X) > MathF.Abs(delta.Y) ? (delta.X > 0f ? 1 : 3) : (delta.Y > 0f ? 2 : 0));
    }

    private void ReadEvents(in GameContext context, float scale)
    {
        var thuds = 0;
        var clinks = 0;
        for (var index = 0; index < board.EventCount; index++)
        {
            ref readonly var happened = ref board.Event(index);
            var world = CellCenter(happened.Cell);
            switch (happened.Kind)
            {
                case DelveEventKind.Dug:
                    particles.Emit(DirtBits, world, 4);
                    break;
                case DelveEventKind.Collected:
                    OnCollected(world, scale);
                    break;
                case DelveEventKind.Landed when happened.Tile == DelveTile.Boulder:
                    thuds++;
                    particles.Emit(Dust, world + new Vector2(0f, 0.45f), 5);
                    break;
                case DelveEventKind.Landed:
                    clinks++;
                    break;
                case DelveEventKind.Pushed:
                    squash = 1f;
                    particles.Emit(Dust, world + new Vector2(0f, 0.4f), 6);
                    UiFeedback.Play(UiSound.GameHitWood);
                    break;
                case DelveEventKind.Strained:
                    squash = MathF.Max(squash, 0.55f);
                    break;
                case DelveEventKind.Crushed:
                    OnCrushed(happened.Tile, world, scale);
                    break;
                case DelveEventKind.Touched:
                    hazard = DelveEventKind.Touched;
                    break;
                case DelveEventKind.TimedOut:
                    hazard = DelveEventKind.TimedOut;
                    break;
                case DelveEventKind.Exploded:
                    OnExploded(world, happened.Tile == DelveTile.GemBlast, context);
                    break;
                case DelveEventKind.ExitOpened:
                    OnExitOpened(world, context);
                    break;
                case DelveEventKind.Escaped:
                    OnEscaped(world, context);
                    break;
                case DelveEventKind.Died:
                    OnDied(context);
                    break;
                default:
                    break;
            }
        }

        if (thuds > 0)
        {
            camera.Shake(MathF.Min(0.3f, 0.12f + 0.04f * thuds));
            UiFeedback.Play(UiSound.GameHitSoft);
        }

        if (clinks > 0)
        {
            UiFeedback.Play(UiSound.GameTick);
        }
    }

    private void OnCollected(Vector2 world, float scale)
    {
        UiFeedback.Play(UiSound.GameCollect);
        particles.Emit(GemSparkle, world, 12);
        var screen = camera.ToScreen(world);
        fx.Shockwave(screen, camera.Px(0.9f), DelveRenderer.GemCore, 0.35f, 2.2f);
        fx.AddText(GameNumber.Signed(1), screen - new Vector2(0f, camera.Px(0.4f)), DelveRenderer.GemLight, 1f,
            40f * scale);
    }

    private void OnCrushed(DelveTile victim, Vector2 world, float scale)
    {
        if (victim == DelveTile.Player)
        {
            hazard = DelveEventKind.Crushed;
            return;
        }

        crushedFoes++;
        var screen = camera.ToScreen(world) - new Vector2(0f, camera.Px(0.8f));
        var text = Loc.T(victim == DelveTile.Slime ? L.Delve.GemShower : L.Delve.Squashed);
        fx.AddText(text, screen, victim == DelveTile.Slime ? DelveRenderer.GemLight : DelveRenderer.Flame, 1.15f,
            34f * scale);
    }

    private void OnExploded(Vector2 world, bool gems, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        particles.Emit(Embers, world, 22);
        particles.Emit(Shards, world, 14);
        if (gems)
        {
            particles.Emit(GemSparkle, world, 24);
        }

        fx.Shockwave(camera.ToScreen(world), camera.Px(2.4f), gems ? DelveRenderer.GemCore : DelveRenderer.Ember, 0.5f,
            3.4f);
        camera.Shake(0.55f);
        context.Fx.Flash(gems ? DelveRenderer.GemLight : DelveRenderer.Flame, 0.3f);
        context.Fx.Punch(0.06f);
    }

    private void OnExitOpened(Vector2 world, in GameContext context)
    {
        exitFlash = 1f;
        UiFeedback.Play(UiSound.GamePowerUp);
        context.Fx.Flash(new Vector4(1f, 1f, 1f, 1f), 0.35f);
        context.Fx.Sweep();
        particles.Emit(GemSparkle, world, 20);
        fx.Shockwave(camera.ToScreen(world), camera.Px(3f), Accent, 0.6f, 3f);
        ShowBanner(Loc.T(L.Delve.ExitOpen), Accent);
    }

    private void OnEscaped(Vector2 world, in GameContext context)
    {
        ending = true;
        endTimer = 0f;
        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
        particles.Confetti(world, 70, ClearPalette, 7f, 0.16f, 1.4f, 9f);
        particles.Emit(GemSparkle, world, 24);
        fx.Shockwave(camera.ToScreen(world), camera.Px(4f), Accent, 0.7f, 3.4f);
        ShowBanner(Loc.T(L.Delve.Escaped), DelveRenderer.GemLight);
    }

    private void OnDied(in GameContext context)
    {
        ending = true;
        endTimer = 0f;
        UiFeedback.Play(UiSound.GameBreak);
        context.Fx.SlowMo(0.5f, 0.4f);
        context.Fx.Vignette(Danger, 0.5f, 0.9f);
        var message = hazard switch
        {
            DelveEventKind.TimedOut => L.Delve.OutOfTime,
            DelveEventKind.Touched => L.Delve.Caught,
            _ => L.Delve.Crushed,
        };
        ShowBanner(Loc.T(message), Danger);
    }

    private void WarnTime(in GameContext context)
    {
        if (board.State != DelveState.Playing || context.Session.State != StageFlow.Playing)
        {
            return;
        }

        var second = (int)MathF.Ceiling(board.TimeLeft);
        if (second == lastSecond)
        {
            return;
        }

        lastSecond = second;
        if (second > TickingSeconds || second <= 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameTick);
        context.Fx.Vignette(Danger, 0.18f + 0.02f * (TickingSeconds - second), 0.5f);
    }

    private void AdvanceEnd(in GameContext context)
    {
        endTimer += context.DeltaSeconds;
        if (endTimer < EndDelay)
        {
            return;
        }

        finished = true;
        var won = board.State == DelveState.Won;
        var outcome = new GameOutcome(board.Gems, ScoreKind.Level, GameId, won)
            .WithStars(DelveBoard.Stars(won, board.Gems, board.BonusGems, board.TimeLeft, board.TimeLimit))
            .WithStat(L.Delve.Gems, GameNumber.Label(board.Gems))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.Elapsed));
        if (won)
        {
            outcome = outcome.WithStat(L.Delve.TimeLeft, TimeText.MinutesSeconds((int)board.TimeLeft))
                .WithStat(L.Delve.Foes, GameNumber.Label(crushedFoes));
        }

        context.Session.Finish(outcome);
    }

    private void ShowBanner(string text, Vector4 tint)
    {
        bannerText = text;
        bannerTint = tint;
        banner = 0f;
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context)
    {
        var full = context.Full;
        var topLeft = camera.ToWorld(full.Min);
        var bottomRight = camera.ToWorld(full.Max);
        var window = new DelveWindow(Math.Max(0, (int)MathF.Floor(topLeft.X) - 1),
            Math.Min(board.Columns - 1, (int)MathF.Ceiling(bottomRight.X) + 1),
            Math.Max(0, (int)MathF.Floor(topLeft.Y) - 1), Math.Min(board.Rows - 1, (int)MathF.Ceiling(bottomRight.Y) + 1));
        drawList.PushClipRect(full.Min, full.Max, true);
        DelveRenderer.DrawCave(drawList, board, in camera, window, clock, Accent, exitFlash);
        DelveRenderer.DrawObjects(drawList, board, in camera, window, clock, squash, exitFlash);
        particles.Draw(drawList, in camera);
        drawList.PopClipRect();
    }

    private void DrawHud(in GameContext context, ImDrawListPtr drawList, float scale)
    {
        var hud = context.Hud;
        hud.Score(board.Gems, L.Delve.Gems);
        hud.Timer(board.TimeLeft, board.TimeLimit, board.TimeLeft <= UrgentSeconds);
        hud.Level(level);
        var quota = quotaLabel.Get(L.Stage.StarsOf, Math.Min(board.Gems, board.Quota), board.Quota);
        var bonus = GameNumber.Label(board.BonusGems);
        hud.Custom(StatCapsule.Width(quota, scale));
        hud.Custom(StatCapsule.Width(bonus, scale));
        if (hud.CustomPlaced(0))
        {
            StatCapsule.Draw(drawList, hud.CustomRect(0),
                board.ExitOpen ? FontAwesomeIcon.DoorOpen : FontAwesomeIcon.DoorClosed, quota,
                board.ExitOpen ? DelveRenderer.GemCore : Muted, scale);
        }

        if (hud.CustomPlaced(1))
        {
            StatCapsule.Draw(drawList, hud.CustomRect(1), FontAwesomeIcon.Gem, bonus,
                board.Gems >= board.BonusGems ? GamePalette.Star : Muted, scale);
        }

        context.Session.Report(board.Gems);
    }
}

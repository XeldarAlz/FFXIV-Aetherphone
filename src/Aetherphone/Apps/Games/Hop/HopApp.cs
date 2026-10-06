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

namespace Aetherphone.Apps.Games.Hop;

internal sealed class HopApp : IMiniGame
{
    private const string GameId = "hop";
    private const float LevelBannerSeconds = 1.6f;
    private const float PadInset = 4f;
    private const float PadKeyInset = 4f;
    private const float PadGap = 6f;
    private const float PadOpacity = 0.92f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float BarHeight = 3f;
    private const float BarInset = 6f;
    private const float SkyBase = 0.15f;
    private const float SkyPerLevel = 0.06f;
    private const float SkyDeepest = 0.85f;
    private const float LastLifeVignette = 0.10f;
    private const float LastLifeVignettePulse = 0.10f;
    private const float LastLifeVignetteSeconds = 0.3f;
    private const float BankPunch = 0.04f;
    private const float DeathShake = 0.65f;
    private const ulong IdleSeed = 0x484F5050455255UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Hop, GameGenre.Arcade, L.Hop.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly TextStyle TimerStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Droplet = new(0.72f, 0.88f, 1f, 0.9f);
    private static readonly Vector4 Wind = new(1f, 1f, 1f, 0.7f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.62f, 0.62f, 0.68f, 1f), new(1f, 0.85f, 0.30f, 1f), new(1f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.46f, 0.86f, 0.62f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
    };

    private static readonly ParticleSpec[] ConfettiSpecs = BuildConfetti();
    private static readonly ParticleSpec HopDust = new(White with { W = 0.5f }, White with { W = 0f }, 0.08f, 1.2f, 0.3f);
    private static readonly ParticleSpec Splash = new(Droplet, HopRenderer.WaterColor with { W = 0f }, 0.09f, 3f, 0.5f, 9f,
        1.2f, 0f, MathF.PI * 1.2f, -MathF.PI * 0.5f);
    private static readonly ParticleSpec SplashRing = new(Droplet, Droplet with { W = 0f }, 0.25f, 0f, 0.45f,
        shape: ParticleShape.Ring, additive: true);
    private static readonly ParticleSpec WindRight = new(Wind, Wind with { W = 0f }, 0.1f, 7f, 0.35f, 0f, 1.2f, 0f, 0.35f, 0f,
        ParticleShape.Streak);
    private static readonly ParticleSpec WindLeft = new(Wind, Wind with { W = 0f }, 0.1f, 7f, 0.35f, 0f, 1.2f, 0f, 0.35f,
        MathF.PI, ParticleShape.Streak);
    private static readonly ParticleSpec DeathBurst = new(HopRenderer.HopperColor, HopRenderer.HopperColor with { W = 0.2f },
        0.16f, 5f, 0.7f, 9f, 1.2f, 8f);
    private static readonly string LevelBonusLabel = string.Concat("+", GameNumber.Label(HopBoard.LevelClearBonus));

    private readonly HopBoard board = new();
    private readonly HopBoard idleBoard = new();
    private readonly HopRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Vector4 glow;
    private readonly ParticleSpec denSparkle;
    private Camera2D camera = Camera2D.Create();
    private LabelSlot levelLabel;
    private bool finished;
    private bool idleReady;
    private float bannerProgress = 1f;
    private string bannerText = string.Empty;

    public HopApp()
    {
        glow = GamePalette.Lighten(Accent, 0.4f);
        denSparkle = new ParticleSpec(glow, White, 0.12f, 3.5f, 0.7f, 2f, 2.4f, 6f, shape: ParticleShape.Star,
            additive: true);
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        finished = false;
        ShowLevelBanner();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.GameOver)
        {
            idleBoard.StartGame(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        PlaceCamera(context, scale);
        context.Backdrop.SetSky(SkyProgress(idleBoard.Level));
        idleBoard.Tick(context.RawDeltaSeconds);
        renderer.Draw(ImGui.GetWindowDrawList(), idleBoard, in camera, Accent, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        PlaceCamera(context, scale);
        context.Backdrop.SetSky(SkyProgress(board.Level));
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            board.Tick(simDelta);
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, LevelBannerSeconds);
        renderer.Draw(drawList, board, in camera, accent, scale);
        var pad = DrawPad(drawList, context, accent, scale);
        if (!finished)
        {
            if (context.Session.State == StageFlow.Playing)
            {
                HandleInput(pad);
            }

            ReactToEvents(context);
            if (board.Lives == 1 && !board.GameOver)
            {
                context.Fx.Vignette(Danger, LastLifeVignette + LastLifeVignettePulse * Pulse.Wave(Pulse.Breath),
                    LastLifeVignetteSeconds);
            }
        }

        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, camera.ToScreen(HopRenderer.CellCenterWorld(6f, HopBoard.MedianRow)), bannerText, accent,
            context.Theme, bannerProgress);
        DrawTimer(drawList, context, accent, scale);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, HopBoard.StartLives);
        context.Hud.Level(board.Level);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(TimerCapsuleWidth(scale));
        context.Session.Report(board.Score);
        if (board.GameOver && !finished)
        {
            finished = true;
            Finish(context);
        }
    }

    private static float SkyProgress(int level) => MathF.Min(SkyDeepest, SkyBase + level * SkyPerLevel);

    private void PlaceCamera(in GameContext context, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        var safe = context.Safe;
        var bottom = MathF.Max(safe.Min.Y, band.Min.Y - PadGap * scale);
        var view = new Rect(safe.Min, new Vector2(safe.Max.X, bottom));
        camera.Fit(view, HopBoard.Columns, HopBoard.Rows, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private static PadDirection DrawPad(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        var side = MathF.Max(0f, band.Height - PadInset * 2f * scale);
        var panel = new Rect(new Vector2(band.Center.X - side * 0.5f, band.Min.Y + PadInset * scale),
            new Vector2(band.Center.X + side * 0.5f, band.Min.Y + PadInset * scale + side));
        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PadOpacity);
        return GamePad.DPad(panel.Inset(PadKeyInset * scale), accent, context.Theme);
    }

    private void HandleInput(PadDirection pad)
    {
        if (pad == PadDirection.Up || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            board.Hop(0, 1);
        }
        else if (pad == PadDirection.Down || GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            board.Hop(0, -1);
        }
        else if (pad == PadDirection.Left || GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.Hop(-1, 0);
        }
        else if (pad == PadDirection.Right || GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.Hop(1, 0);
        }
    }

    private void ReactToEvents(in GameContext context)
    {
        var hopper = HopRenderer.CellCenterWorld(board.X, board.Row);
        if (board.HoppedThisFrame && !board.Dying)
        {
            UiFeedback.Play(UiSound.GameJump);
            particles.Emit(HopDust, hopper + new Vector2(0f, 0.4f), 3);
            if (board.OnLog)
            {
                UiFeedback.Play(UiSound.GamePop);
                var landing = hopper + new Vector2(0f, 0.3f);
                particles.Emit(Splash, landing, 10);
                particles.Emit(SplashRing, landing, 1);
            }
        }

        if (board.BankedBayThisFrame >= 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
            var den = HopRenderer.CellCenterWorld(HopBoard.BayColumns[board.BankedBayThisFrame], HopBoard.BankRow);
            var screen = camera.ToScreen(den);
            particles.Emit(denSparkle, den, 14);
            fx.Shockwave(screen, camera.Px(2.4f), glow with { W = 0.7f }, 0.4f, 2.5f);
            fx.AddText(GameNumber.Label(board.LastBankPoints), screen, glow, 1.1f);
            camera.Shake(0.12f);
            context.Fx.Punch(BankPunch);
        }

        if (board.BumpedThisFrame)
        {
            camera.Shake(0.08f);
            fx.HitStop(0.03f);
        }

        if (board.NearMissThisFrame)
        {
            particles.Emit(board.NearMissDirection > 0f ? WindRight : WindLeft, hopper, 6);
            camera.Shake(0.05f);
        }

        if (board.DiedThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            particles.Emit(DeathBurst, hopper, 16);
            camera.Shake(DeathShake);
            fx.HitStop(0.1f);
            context.Fx.Flash(Danger, 0.35f);
            if (board.Lives == 1)
            {
                context.Fx.Vignette(Danger, 0.5f, 1f);
            }
        }

        if (board.LevelClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            var top = new Vector2(HopBoard.Columns * 0.5f, 2f);
            EmitConfetti(top, 70);
            context.Fx.Flash(glow, 0.18f);
            fx.AddText(LevelBonusLabel, camera.ToScreen(top), glow, 1.3f);
        }

        if (board.LevelStartedThisFrame)
        {
            ShowLevelBanner();
        }
    }

    private void ShowLevelBanner()
    {
        bannerText = levelLabel.Get(L.Hop.LevelNumber, board.Level);
        bannerProgress = 0f;
    }

    private void EmitConfetti(Vector2 origin, int count)
    {
        var perColor = Math.Max(1, count / ConfettiSpecs.Length);
        for (var index = 0; index < ConfettiSpecs.Length; index++)
        {
            particles.Emit(in ConfettiSpecs[index], origin, perColor);
        }
    }

    private string TimerLabel() => TimeText.MinutesSeconds((int)MathF.Ceiling(board.TimerRemaining));

    private float TimerCapsuleWidth(float scale) =>
        CapsulePadX * 2f + IconSize + IconGap + Typography.Measure(TimerLabel(), TimerStyle).X / scale;

    private void DrawTimer(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var low = board.TimerRemaining < HopBoard.LowTimerSeconds && !board.Frozen && !board.GameOver;
        var pulse = low ? 0.5f + 0.5f * Pulse.Wave(Pulse.Fast) : 0f;
        var ink = low ? Vector4.Lerp(context.Theme.TextStrong, Danger, pulse) : context.Theme.TextStrong;
        var tint = low ? Danger : accent;
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Stopwatch, tint,
            iconSize);
        var origin = new Vector2(left + iconSize + IconGap * scale, rect.Center.Y - Typography.LineHeight(TimerStyle) * 0.5f);
        Typography.Draw(drawList, origin, TimerLabel(), ink, TimerStyle);
        var inset = BarInset * scale;
        var height = BarHeight * scale;
        var bottom = rect.Max.Y - height * 0.8f;
        var barLeft = rect.Min.X + inset;
        var barRight = rect.Max.X - inset;
        drawList.AddRectFilled(new Vector2(barLeft, bottom - height), new Vector2(barRight, bottom),
            ImGui.GetColorU32(tint with { W = 0.18f }), height * 0.5f);
        var fraction = Math.Clamp(board.TimerFraction, 0f, 1f);
        if (fraction <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(new Vector2(barLeft, bottom - height), new Vector2(barLeft + (barRight - barLeft) * fraction, bottom),
            ImGui.GetColorU32(tint with { W = 0.9f }), height * 0.5f);
    }

    private void Finish(in GameContext context)
    {
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Games.Dens, GameNumber.Label(board.BankedTotal)));
    }

    private static ParticleSpec[] BuildConfetti()
    {
        var specs = new ParticleSpec[CelebrationPalette.Length];
        for (var index = 0; index < specs.Length; index++)
        {
            specs[index] = new ParticleSpec(CelebrationPalette[index], CelebrationPalette[index], 0.14f, 9f, 1.4f, 18f,
                0.7f, 16f, 1.4f, -MathF.PI * 0.5f, ParticleShape.Square);
        }

        return specs;
    }
}

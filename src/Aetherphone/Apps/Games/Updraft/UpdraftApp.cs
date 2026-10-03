using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Updraft;

internal sealed class UpdraftApp : IMiniGame
{
    private const string GameId = "updraft";
    private const string HeightStatId = "updraft.height";
    private const float BannerSeconds = 1.5f;
    private const float TrailInterval = 0.03f;
    private const float PowerUpRadius = 15f;
    private static readonly Vector4 PuffColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Vector4 FragileColor = new(0.86f, 0.82f, 0.96f, 0.9f);
    private static readonly Vector4 ZapFlash = new(0.75f, 0.80f, 1f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.84f, 0.38f, 1f), new(0.50f, 0.93f, 1f, 1f), new(1f, 0.52f, 0.64f, 1f), new(0.62f, 0.90f, 0.46f, 1f),
        new(0.80f, 0.62f, 0.98f, 1f), new(1f, 1f, 1f, 1f),
    };

    private readonly UpdraftBoard board = new();
    private readonly UpdraftRenderer renderer = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private RollingValue heightRoll;
    private bool started;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private bool statsLoaded;
    private bool pointerMode;
    private int bestMetres;
    private float resultAppear;
    private float bannerProgress = 1f;
    private float trailTimer;
    private float crystalPulse;
    private float entrance;
    private string bannerText = string.Empty;
    private string resultLine = string.Empty;
    private Vector2 lastMouse;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Updraft.Title);
    public GameGenre Genre => GameGenre.Arcade;
    public bool RunsOnAClock => true;

    public void Open()
    {
        started = false;
        statsLoaded = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame()
    {
        board.StartGame(Environment.TickCount, bestMetres / UpdraftBoard.MetresPerUnit);
        renderer.Reset();
        particles.Clear();
        fx.Clear();
        heightRoll.Snap(0);
        finished = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        bannerProgress = 1f;
        trailTimer = 0f;
        crystalPulse = 0f;
        entrance = 0f;
        started = true;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            bestMetres = context.Stats.Get(HeightStatId).BestScore;
            statsLoaded = true;
        }

        if (!started)
        {
            StartNewGame();
        }

        if (pendingSubmit)
        {
            Submit(context.Stats);
        }

        var rowY = body.Min.Y + 30f * scale;
        var restartRadius = 16f * scale;
        var restartCenter = new Vector2(body.Max.X - 22f * scale, rowY);
        var input = UpdraftInput.Keys(0f);
        if (!finished)
        {
            var layout = UpdraftView.Fit(body, board.CameraBottom, Vector2.Zero);
            input = ReadInput(body, layout, restartCenter, restartRadius);
        }

        board.Tick(fx.ScaleDelta(deltaSeconds), input);

        var steadyView = UpdraftView.Fit(body, UpdraftRenderer.InterpolatedCamera(board), Vector2.Zero);
        ReactToEvents(steadyView, scale);
        EmitTrails(steadyView, deltaSeconds, scale);
        renderer.Animate(board, deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, BannerSeconds);
        crystalPulse = MathF.Max(0f, crystalPulse - deltaSeconds * 3f);
        entrance = GameJuice.Advance(entrance, deltaSeconds);
        if (board.GameOver && !finished)
        {
            finished = true;
            pendingSubmit = true;
            resultAppear = 0f;
            resultLine = Loc.T(L.Updraft.ResultLine, GameNumber.Label(board.HeightMetres), GameNumber.Label(board.Crystals));
        }

        var drawList = ImGui.GetWindowDrawList();
        var time = (float)ImGui.GetTime();
        var view = UpdraftView.Fit(body, steadyView.Camera, fx.ShakeOffset(scale));
        var sky = UpdraftSky.At(view.Metres);
        renderer.Draw(drawList, board, view, sky, Accent, time, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        GameBanner.Draw(drawList, new Vector2(body.Center.X, body.Min.Y + body.Height * 0.3f), bannerText, Accent, theme,
            bannerProgress);
        DrawHud(drawList, body, rowY, restartCenter, restartRadius, theme, deltaSeconds, time, scale);
        if (!board.Launched)
        {
            DrawReadyPrompt(body, GameJuice.PopIn(entrance), scale);
        }

        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void Submit(GameStatsStore stats)
    {
        newBest = stats.SubmitScore(GameId, board.Score);
        if (stats.SubmitScore(HeightStatId, board.HeightMetres))
        {
            bestMetres = board.HeightMetres;
        }

        pendingSubmit = false;
    }

    private UpdraftInput ReadInput(Rect body, in UpdraftView layout, Vector2 restartCenter, float restartRadius)
    {
        var left = GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var axis = (right ? 1f : 0f) - (left ? 1f : 0f);
        var mouse = ImGui.GetMousePos();
        var hovering = UiInteract.Hover(body.Min, body.Max);
        if (left || right)
        {
            pointerMode = false;
        }
        else if (hovering && Vector2.DistanceSquared(mouse, lastMouse) > 1f)
        {
            pointerMode = true;
        }

        lastMouse = mouse;
        if (!board.Launched)
        {
            var overRestart = Vector2.Distance(mouse, restartCenter) <= restartRadius;
            var tapped = hovering && !overRestart && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            if (tapped || left || right || GameInput.Pressed(ImGuiKey.Space))
            {
                board.Launch();
            }
        }

        return pointerMode && hovering ? UpdraftInput.Pointer(layout.WorldX(mouse.X)) : UpdraftInput.Keys(axis);
    }

    private void ReactToEvents(in UpdraftView view, float scale)
    {
        var events = board.Events;
        for (var index = 0; index < events.Length; index++)
        {
            ref readonly var item = ref events[index];
            var position = view.ToScreen(item.X, item.Y);
            switch (item.Kind)
            {
                case UpdraftEventKind.Bounce:
                    OnBounce(item.CloudKind, position, scale);
                    break;
                case UpdraftEventKind.Break:
                    UiFeedback.Play(UiSound.GameBreak);
                    particles.Burst(position + new Vector2(0f, 0.4f * view.Unit), 14, FragileColor, 120f * scale, 3.2f, 0.7f,
                        380f, MathF.PI, MathF.PI * 0.5f);
                    break;
                case UpdraftEventKind.Zap:
                    OnZap(position, scale);
                    break;
                case UpdraftEventKind.ShieldBlock:
                    UiFeedback.Play(UiSound.GamePop);
                    fx.Shockwave(position, 70f * scale, UpdraftArt.BubbleColor, 0.5f, 3f);
                    particles.Burst(position, 16, UpdraftArt.BubbleColor with { W = 0.8f }, 200f * scale, 2.6f, 0.6f, 60f,
                        MathF.Tau, 0f, ParticleShape.GlowCircle);
                    fx.AddText(Loc.T(L.Updraft.Blocked), position - new Vector2(0f, 24f * scale), UpdraftArt.BubbleColor, 1.1f);
                    fx.AddTrauma(0.2f);
                    break;
                case UpdraftEventKind.Crystal:
                    OnCrystal(item, position, scale);
                    break;
                case UpdraftEventKind.Feather:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    particles.Sparkle(position, 16, UpdraftArt.GoldColor, 160f * scale, 2.6f, 0.8f);
                    fx.AddText(Loc.T(L.Updraft.Feather), position - new Vector2(0f, 24f * scale), UpdraftArt.GoldColor, 1.1f);
                    break;
                case UpdraftEventKind.Shield:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    fx.Shockwave(position, 54f * scale, UpdraftArt.BubbleColor, 0.45f, 2.6f);
                    fx.AddText(Loc.T(L.Updraft.Shield), position - new Vector2(0f, 24f * scale), UpdraftArt.BubbleColor, 1.1f);
                    break;
                case UpdraftEventKind.Milestone:
                    UiFeedback.Play(UiSound.GameClear);
                    bannerText = Loc.T(L.Updraft.Metres, GameNumber.Label(item.Value));
                    bannerProgress = 0f;
                    particles.Sparkle(position, 12, GamePalette.Lighten(Accent, 0.4f), 150f * scale, 2.6f, 0.7f);
                    break;
                case UpdraftEventKind.PassedBest:
                    UiFeedback.Play(UiSound.GameClear);
                    bannerText = Loc.T(L.Updraft.PassedBest);
                    bannerProgress = 0f;
                    particles.Confetti(new Vector2(view.Body.Center.X, view.Body.Min.Y + view.Body.Height * 0.2f), 90,
                        CelebrationPalette, 300f * scale, 4f, 1.4f);
                    fx.Flash(GamePalette.Lighten(Accent, 0.3f), 0.16f);
                    break;
                case UpdraftEventKind.Fell:
                    UiFeedback.Play(UiSound.GameWrong);
                    fx.AddTrauma(0.3f);
                    break;
            }
        }
    }

    private void OnBounce(UpdraftCloudKind kind, Vector2 position, float scale)
    {
        renderer.OnBounce(kind);
        particles.Burst(position, 9, PuffColor, 110f * scale, 3.4f, 0.45f, -40f, MathF.PI, MathF.PI * 0.5f);
        switch (kind)
        {
            case UpdraftCloudKind.Golden:
                UiFeedback.Play(UiSound.GamePowerUp);
                fx.Flash(UpdraftArt.GoldColor, 0.22f);
                fx.Shockwave(position, 90f * scale, UpdraftArt.GoldColor, 0.5f, 3.4f);
                particles.Sparkle(position, 18, UpdraftArt.GoldColor, 220f * scale, 3f, 0.8f);
                fx.AddText(Loc.T(L.Updraft.SuperBounce), position - new Vector2(0f, 30f * scale), UpdraftArt.GoldColor, 1.2f);
                fx.AddTrauma(0.12f);
                return;
            case UpdraftCloudKind.Spring:
                UiFeedback.Play(UiSound.GamePop);
                fx.Shockwave(position, 60f * scale, UpdraftArt.CoilColor, 0.4f, 2.6f);
                fx.AddTrauma(0.08f);
                return;
            default:
                UiFeedback.Play(UiSound.GameJump);
                return;
        }
    }

    private void OnZap(Vector2 position, float scale)
    {
        renderer.OnZap();
        UiFeedback.Play(UiSound.GameHitSoft);
        fx.AddTrauma(0.55f);
        fx.HitStop(0.06f);
        fx.Flash(ZapFlash, 0.35f);
        particles.Streaks(position, 14, UpdraftArt.BoltColor, 380f * scale, 2.4f, 0.4f);
        fx.AddText(Loc.T(L.Updraft.Zap), position - new Vector2(0f, 24f * scale), ZapFlash, 1.15f);
    }

    private void OnCrystal(in UpdraftEvent item, Vector2 position, float scale)
    {
        UiFeedback.Play(UiSound.GameCollect);
        crystalPulse = 1f;
        var climb = Math.Min(item.Detail, UpdraftBoard.MaxChain) / (float)UpdraftBoard.MaxChain;
        var color = Vector4.Lerp(UpdraftArt.CrystalColor, UpdraftArt.GoldColor, climb);
        particles.Sparkle(position, 6 + item.Detail * 2, color, 120f * scale, 2.4f, 0.6f);
        fx.AddText("+" + GameNumber.Label(item.Value), position - new Vector2(0f, 14f * scale), color, 0.9f + 0.12f * item.Detail,
            46f + 10f * item.Detail);
    }

    private void EmitTrails(in UpdraftView view, float deltaSeconds, float scale)
    {
        if (!board.Launched || board.GameOver)
        {
            return;
        }

        trailTimer -= deltaSeconds;
        if (trailTimer > 0f)
        {
            return;
        }

        trailTimer = TrailInterval;
        var bird = UpdraftRenderer.InterpolatedBird(board);
        var position = view.ToScreen(bird.X, bird.Y);
        var radius = UpdraftBoard.BirdRadius * view.Unit;
        if (board.Boosting)
        {
            var color = board.LastBounceKind == UpdraftCloudKind.Golden ? UpdraftArt.GoldColor : PuffColor;
            particles.Streaks(position + new Vector2(0f, radius), 2, color with { W = 0.8f }, 160f * scale, 2.2f, 0.35f, 0.5f,
                MathF.PI * 0.5f);
            particles.Burst(position, 1, color with { W = 0.5f }, 20f * scale, radius * 0.3f / scale, 0.35f, 0f, MathF.Tau, 0f,
                ParticleShape.GlowCircle);
            return;
        }

        if (board.Gliding)
        {
            particles.Sparkle(position + new Vector2(0f, radius * 0.6f), 1, UpdraftArt.GoldColor, 40f * scale, 2f, 0.5f);
        }
    }

    private void DrawHud(ImDrawListPtr drawList, Rect body, float rowY, Vector2 restartCenter, float restartRadius,
        PhoneTheme theme, float deltaSeconds, float time, float scale)
    {
        var heightLabel = Loc.T(L.Updraft.Height);
        var crystalsLabel = Loc.T(L.Updraft.Crystals);
        var heightText = GameNumber.Label(board.HeightMetres);
        var crystalsText = GameNumber.Label(board.Crystals);
        var gap = 10f * scale;
        var start = body.Min.X + 12f * scale;
        var available = restartCenter.X - restartRadius - gap - start;
        var natural = GameHud.PillWidth(heightLabel, heightText) + GameHud.PillWidth(crystalsLabel, crystalsText) + gap;
        var sizeScale = MathF.Min(1f, available / natural);
        var heightWidth = GameHud.PillWidth(heightLabel, heightText, sizeScale);
        var crystalsWidth = GameHud.PillWidth(crystalsLabel, crystalsText, sizeScale);
        var left = start + (available - heightWidth - crystalsWidth - gap) * 0.5f;
        var beatingBest = bestMetres > 0 && board.HeightMetres > bestMetres;
        GameHud.ScorePill(new Vector2(left + heightWidth * 0.5f, rowY), heightLabel, ref heightRoll, board.HeightMetres,
            Accent, theme, deltaSeconds, beatingBest, sizeScale);
        left += heightWidth + gap;
        GameHud.Pill(new Vector2(left + crystalsWidth * 0.5f, rowY), crystalsLabel, crystalsText, Accent, theme,
            crystalPulse > 0f, sizeScale);
        if (!finished && GameHud.RestartButton(restartCenter, restartRadius, theme))
        {
            StartNewGame();
        }

        DrawPowerUps(drawList, body, rowY + (GameHud.PillHeight * 0.5f + 10f) * scale, time, scale);
    }

    private void DrawPowerUps(ImDrawListPtr drawList, Rect body, float top, float time, float scale)
    {
        var radius = PowerUpRadius * scale;
        var center = new Vector2(body.Min.X + 12f * scale + radius, top + radius);
        if (board.Gliding)
        {
            var corner = new Vector2(radius, radius);
            Material.Frosted(drawList, center - corner, center + corner, radius, scale);
            var ringRadius = radius - 2.5f * scale;
            ProgressRing.Track(drawList, center, ringRadius, 3f * scale, new Vector4(1f, 1f, 1f, 0.15f));
            ProgressRing.Fill(drawList, center, ringRadius, 3f * scale, board.FeatherFraction, UpdraftArt.GoldColor);
            UpdraftArt.DrawFeather(drawList, center, radius * 0.4f, 0f);
            center.X += radius * 2f + 8f * scale;
        }

        if (!board.HasShield)
        {
            return;
        }

        var shieldCorner = new Vector2(radius, radius);
        Material.Frosted(drawList, center - shieldCorner, center + shieldCorner, radius, scale);
        UpdraftArt.DrawBubble(drawList, center, radius * 0.62f, time, 1f);
    }

    private static void DrawReadyPrompt(Rect body, float pop, float scale)
    {
        if (pop <= 0.01f)
        {
            return;
        }

        var pulse = pop * (1f + 0.05f * Pulse.Wave(Pulse.Calm));
        var center = new Vector2(body.Center.X, body.Min.Y + body.Height * 0.36f);
        var prompt = Loc.T(L.Games.TapToStart);
        Typography.DrawCentered(center + new Vector2(1.5f * scale, 1.5f * scale), prompt, new Vector4(0f, 0f, 0f, 0.35f),
            TextStyles.Title2.Scale * pulse, TextStyles.Title2.Weight);
        Typography.DrawCentered(center, prompt, new Vector4(1f, 1f, 1f, 1f), TextStyles.Title2.Scale * pulse,
            TextStyles.Title2.Weight);
        var hint = Loc.T(L.Updraft.Hint);
        var hintWidth = body.Width * 0.82f;
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, hintWidth).Y;
        var hintCenter = center + new Vector2(0f, 24f * scale + hintHeight * 0.5f);
        var fade = MathF.Min(1f, pop);
        var drawList = ImGui.GetWindowDrawList();
        Typography.DrawWrappedCentered(drawList, hintCenter + new Vector2(1f * scale, 1f * scale), hint,
            new Vector4(0f, 0f, 0f, 0.3f * fade), TextStyles.Footnote, hintWidth);
        Typography.DrawWrappedCentered(drawList, hintCenter, hint, new Vector4(1f, 1f, 1f, 0.92f * fade), TextStyles.Footnote,
            hintWidth);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), resultLine, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}

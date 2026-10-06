using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Coil;

internal sealed class CoilApp : ILegacyMiniGame
{
    private const string GameId = "coil";
    private const float StageBannerSeconds = 2.4f;
    private const float ComboBannerSeconds = 1.2f;
    private const float AimFollowRate = 22f;
    private const float RecoilDecay = 6f;
    private const float SwapDecay = 7f;
    private const float DangerShake = 0.9f;
    private const int BigChain = 3;

    private static readonly LocString[] StageNames =
    {
        L.Coil.StageWhirlpool, L.Coil.StageMeander, L.Coil.StageTwinCoil, L.Coil.StageHeart, L.Coil.StageZigzag,
        L.Coil.StageStarburst, L.Coil.StageHourglass, L.Coil.StageClover, L.Coil.StageKeystone, L.Coil.StagePaperclip,
        L.Coil.StageFigureEight, L.Coil.StageRipple,
    };

    private static readonly CoilPower[] TimedPowers = { CoilPower.Freeze, CoilPower.Slow, CoilPower.Reverse, CoilPower.Guide };

    private readonly CoilBoard board = new(Environment.TickCount);
    private readonly CoilRenderer renderer = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private bool started;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private int loadedBest = -1;
    private float resultAppear;
    private float readyAppear;
    private float recoil;
    private float swapPulse;
    private float turretAngle = -MathF.PI * 0.5f;
    private float stageBanner = 1f;
    private float comboBanner = 1f;
    private string comboText = string.Empty;
    private string resultLine = string.Empty;
    private string stageLabel = string.Empty;
    private string? stageLabelPrefix;
    private int stageLabelNumber = -1;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Coil.Title);
    public GameGenre Genre => GameGenre.Puzzle;
    public bool RunsOnAClock => true;

    public void Open()
    {
        loadedBest = -1;
        started = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void NewRun(bool playImmediately)
    {
        board.Reset(Environment.TickCount);
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        finished = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        readyAppear = 0f;
        recoil = 0f;
        swapPulse = 0f;
        comboBanner = 1f;
        stageBanner = 1f;
        started = true;
        if (playImmediately)
        {
            BeginPlay();
        }
    }

    private void BeginPlay()
    {
        board.Begin();
        stageBanner = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        var time = (float)ImGui.GetTime();
        if (loadedBest < 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
        }

        if (!started)
        {
            NewRun(false);
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, board.Score);
            if (newBest)
            {
                loadedBest = board.Score;
            }

            pendingSubmit = false;
        }

        var rowY = body.Min.Y + 30f * scale;
        var pad = 8f * scale;
        var area = new Rect(new Vector2(body.Min.X + pad * 2f, rowY + 34f * scale), body.Max - new Vector2(pad * 2f, pad * 2f));
        var field = CoilRenderer.FitField(area);
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        board.BeginFrame();
        var aim = AimAt(field);
        if (!finished)
        {
            HandleInput(field, aim);
        }

        board.Tick(fx.ScaleDelta(deltaSeconds));
        ReactToEvents(field, scale, theme);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        Animate(aim, deltaSeconds);
        var shake = fx.ShakeOffset(scale);
        var shaken = new Rect(field.Min + shake, field.Max + shake);
        var turret = CoilShapes.Polar(turretAngle);
        DrawHud(body, rowY, theme, deltaSeconds, scale);
        GameScene.Arena(drawList, new Rect(shaken.Min - new Vector2(pad, pad), shaken.Max + new Vector2(pad, pad)), 18f * scale,
            scale, Accent);
        renderer.DrawTrack(drawList, board, shaken, Accent, scale, time);
        renderer.DrawVortex(drawList, board, shaken, Accent, time);
        renderer.DrawMarbles(drawList, board, shaken, time);
        renderer.DrawShots(drawList, board, shaken, time);
        if (board.State == CoilState.Playing)
        {
            renderer.DrawAim(drawList, board, shaken, turret, scale);
        }

        renderer.DrawLauncher(drawList, board, shaken, turret, Accent, recoil, swapPulse, time);
        CoilRenderer.DrawDangerVignette(drawList, shaken, board.State == CoilState.Draining ? 1f : board.Danger, time);
        fx.DrawFlash(drawList, shaken, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawPowerChips(drawList, shaken, theme, scale);
        DrawBanners(drawList, shaken, theme, scale);
        if (board.State == CoilState.Ready)
        {
            DrawReady(drawList, field, theme, deltaSeconds, scale);
        }

        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private Vector2 AimAt(Rect field)
    {
        var launcher = CoilRenderer.ToScreen(field, board.Launcher);
        var direction = ImGui.GetMousePos() - launcher;
        return direction.LengthSquared() < 1f ? CoilShapes.Polar(turretAngle) : Vector2.Normalize(direction);
    }

    private void HandleInput(Rect field, Vector2 aim)
    {
        var hovered = UiInteract.Hover(field.Min, field.Max);
        var swapKey = GameInput.Pressed(ImGuiKey.Space);
        if (board.State == CoilState.Ready)
        {
            if ((hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) || swapKey)
            {
                BeginPlay();
            }

            return;
        }

        if (board.State != CoilState.Playing)
        {
            return;
        }

        if ((hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) || swapKey)
        {
            if (board.Swap())
            {
                swapPulse = 1f;
                UiFeedback.Play(UiSound.GameTick);
            }

            return;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && board.Fire(aim))
        {
            recoil = 1f;
            UiFeedback.Play(UiSound.GameShoot);
        }
    }

    private void Animate(Vector2 aim, float deltaSeconds)
    {
        var target = MathF.Atan2(aim.Y, aim.X);
        var difference = MathF.IEEERemainder(target - turretAngle, MathF.Tau);
        turretAngle += difference * MathF.Min(1f, deltaSeconds * AimFollowRate);
        recoil = MathF.Max(0f, recoil - deltaSeconds * RecoilDecay);
        swapPulse = MathF.Max(0f, swapPulse - deltaSeconds * SwapDecay);
        stageBanner = GameBanner.Advance(stageBanner, deltaSeconds, StageBannerSeconds);
        comboBanner = GameBanner.Advance(comboBanner, deltaSeconds, ComboBannerSeconds);
        if (board.State == CoilState.Playing && board.Danger > 0f)
        {
            fx.AddTrauma(deltaSeconds * DangerShake * board.Danger);
        }
    }

    private void ReactToEvents(Rect field, float scale, PhoneTheme theme)
    {
        var unit = field.Width / CoilBoard.FieldWidth;
        var radius = CoilBoard.MarbleRadius * unit;
        ReactToClears(field, radius, scale);
        ReactToGaps(field, radius, scale);
        ReactToPowers(field, radius, scale);
        var swallowed = board.Swallowed;
        for (var index = 0; index < swallowed.Length; index++)
        {
            var vortex = CoilRenderer.ToScreen(field, swallowed[index].Position);
            particles.Burst(vortex, 4, CoilArt.ColourOf(swallowed[index].Colour), 120f * scale, 2.2f, 0.5f, 0f);
        }

        if (board.LandedThisFrame && board.Clears.Length == 0)
        {
            var land = CoilRenderer.ToScreen(field, board.LandPosition);
            fx.Shockwave(land, radius * 2.2f, new Vector4(1f, 1f, 1f, 0.5f), 0.22f, 1.6f, radius);
            UiFeedback.Play(UiSound.GamePiece);
        }

        if (board.DrainStartedThisFrame)
        {
            UiFeedback.Play(UiSound.GameWrong);
            fx.Flash(theme.Danger, 0.35f);
            fx.AddTrauma(0.55f);
        }

        if (board.LifeLostThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            fx.AddTrauma(0.3f);
        }

        if (board.StageClearedThisFrame)
        {
            UiFeedback.Play(UiSound.GameClear);
            var top = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.12f);
            particles.Confetti(top, 90, CoilArt.MarbleColours, 300f * scale, 4f, 1.6f);
            particles.Sparkle(CoilRenderer.ToScreen(field, board.Track.End), 24, GamePalette.Lighten(Accent, 0.4f),
                200f * scale, 3f, 0.9f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.22f);
        }

        if (board.StageStartedThisFrame)
        {
            stageBanner = 0f;
        }

        if (board.GameOverThisFrame && !finished)
        {
            finished = true;
            pendingSubmit = true;
            resultAppear = 0f;
            resultLine = Loc.T(L.Coil.ReachedStage, board.Stage);
        }
    }

    private void ReactToClears(Rect field, float radius, float scale)
    {
        var bursts = board.Bursts;
        for (var index = 0; index < bursts.Length; index++)
        {
            var center = CoilRenderer.ToScreen(field, bursts[index].Position);
            var colour = CoilArt.ColourOf(bursts[index].Colour);
            particles.Sparkle(center, 5, GamePalette.Lighten(colour, 0.35f), 130f * scale, 2.4f, 0.6f);
            particles.Burst(center, 6, colour, 150f * scale, 2.6f, 0.45f, 160f, MathF.Tau, 0f, ParticleShape.GlowCircle);
        }

        var clears = board.Clears;
        var deepest = 0;
        for (var index = 0; index < clears.Length; index++)
        {
            ref readonly var clear = ref clears[index];
            var center = CoilRenderer.ToScreen(field, clear.Center);
            var colour = CoilArt.ColourOf(clear.Colour);
            fx.Shockwave(center, radius * (3.2f + clear.Count * 0.5f), GamePalette.Lighten(colour, 0.3f), 0.45f, 3f, radius);
            fx.AddText("+" + GameNumber.Label(clear.Points), center, GamePalette.Lighten(colour, 0.4f), 1.1f);
            fx.AddTrauma(MathF.Min(0.3f, 0.04f * clear.Count * clear.Multiplier));
            deepest = Math.Max(deepest, clear.Multiplier);
        }

        if (clears.Length == 0)
        {
            return;
        }

        UiFeedback.Play(deepest >= 2 ? UiSound.GameMatch : UiSound.GamePop);
        if (deepest < 2)
        {
            return;
        }

        comboText = Loc.T(L.Coil.Chain, deepest);
        comboBanner = 0f;
        if (deepest < BigChain)
        {
            return;
        }

        fx.HitStop(MathF.Min(0.1f, 0.04f + 0.015f * (deepest - BigChain)));
        fx.Flash(GamePalette.Lighten(Accent, 0.5f), 0.18f);
        particles.Sparkle(field.Center, 18, new Vector4(1f, 0.95f, 0.7f, 1f), 220f * scale, 3f, 0.8f);
    }

    private void ReactToGaps(Rect field, float radius, float scale)
    {
        var gaps = board.Gaps;
        for (var index = 0; index < gaps.Length; index++)
        {
            var center = CoilRenderer.ToScreen(field, gaps[index].Position);
            var gold = new Vector4(1f, 0.88f, 0.45f, 1f);
            particles.Streaks(center, 12, gold, 320f * scale, 2.2f, 0.5f);
            particles.Sparkle(center, 10, new Vector4(1f, 1f, 0.9f, 1f), 160f * scale, 2.6f, 0.7f);
            fx.Shockwave(center, radius * 4.5f, gold, 0.5f, 3.2f, radius);
            fx.AddText(Loc.T(L.Coil.GapShot) + " +" + GameNumber.Label(gaps[index].Points),
                center - new Vector2(0f, radius * 2f), gold, 1.05f);
            UiFeedback.Play(UiSound.GameCollect);
        }
    }

    private void ReactToPowers(Rect field, float radius, float scale)
    {
        var triggers = board.Triggers;
        for (var index = 0; index < triggers.Length; index++)
        {
            var center = CoilRenderer.ToScreen(field, triggers[index].Position);
            var color = CoilArt.PowerColour(triggers[index].Power);
            fx.Shockwave(center, radius * 7f, color, 0.6f, 3.6f, radius);
            fx.Flash(color, 0.16f);
            fx.AddText(Loc.T(PowerName(triggers[index].Power)), center - new Vector2(0f, radius * 2.6f), color, 1.2f);
            particles.Sparkle(center, 16, color, 200f * scale, 3f, 0.8f);
            UiFeedback.Play(UiSound.GamePowerUp);
        }
    }

    private static LocString PowerName(CoilPower power) => power switch
    {
        CoilPower.Freeze => L.Coil.PowerFreeze,
        CoilPower.Slow => L.Coil.PowerSlow,
        CoilPower.Reverse => L.Coil.PowerReverse,
        CoilPower.Blast => L.Coil.PowerBlast,
        CoilPower.Prism => L.Coil.PowerPrism,
        _ => L.Coil.PowerGuide,
    };

    private string StageLabel()
    {
        var prefix = Loc.T(L.Games.Stage);
        if (ReferenceEquals(prefix, stageLabelPrefix) && stageLabelNumber == board.Stage)
        {
            return stageLabel;
        }

        stageLabelPrefix = prefix;
        stageLabelNumber = board.Stage;
        stageLabel = prefix + " " + GameNumber.Label(board.Stage);
        return stageLabel;
    }

    private void DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        var scoreLabel = Loc.T(L.Games.Score);
        var stageCaption = Loc.T(L.Games.Stage);
        var stageText = GameNumber.Label(board.Stage);
        var restartRadius = 16f * scale;
        var restartCenter = new Vector2(body.Max.X - 22f * scale, rowY);
        var gap = 10f * scale;
        var left = body.Min.X + 40f * scale;
        var available = restartCenter.X - restartRadius - gap - left;
        var natural = GameHud.PillWidth(scoreLabel, GameNumber.Label(board.Score)) + GameHud.PillWidth(stageCaption, stageText) + gap;
        var sizeScale = MathF.Min(1f, available / natural);
        var scoreWidth = GameHud.PillWidth(scoreLabel, GameNumber.Label(board.Score), sizeScale);
        var stageWidth = GameHud.PillWidth(stageCaption, stageText, sizeScale);
        var start = left + (available - scoreWidth - stageWidth - gap) * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(start + scoreWidth * 0.5f, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme,
            deltaSeconds, beatingBest, sizeScale);
        GameHud.Pill(new Vector2(start + scoreWidth + gap + stageWidth * 0.5f, rowY), stageCaption, stageText, Accent, theme,
            false, sizeScale);
        if (GameHud.RestartButton(restartCenter, restartRadius, theme))
        {
            NewRun(true);
        }

        DrawLives(body, rowY, scale);
    }

    private void DrawLives(Rect body, float rowY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 5.5f * scale;
        var x = body.Min.X + 20f * scale;
        var lastLife = board.Lives == 1;
        for (var life = 0; life < CoilBoard.StartLives; life++)
        {
            var center = new Vector2(x, rowY + (life - 1) * radius * 2.7f);
            if (life >= board.Lives)
            {
                drawList.AddCircle(center, radius * 0.8f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 12,
                    MathF.Max(1f, scale));
                continue;
            }

            var alpha = lastLife ? 0.55f + 0.45f * Pulse.Wave(Pulse.Fast) : 1f;
            CoilArt.Marble(drawList, center, radius, (byte)life, CoilPower.None, 0f, new Vector2(1f, 0f), alpha,
                (float)ImGui.GetTime());
        }
    }

    private void DrawPowerChips(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale)
    {
        var chipSize = new Vector2(50f * scale, 22f * scale);
        var cursor = field.Min + new Vector2(8f * scale, 8f * scale);
        for (var index = 0; index < TimedPowers.Length; index++)
        {
            var power = TimedPowers[index];
            var left = TimeLeft(power);
            if (left <= 0f)
            {
                continue;
            }

            DrawChip(drawList, cursor, chipSize, power, left / Duration(power), false, scale);
            cursor.X += chipSize.X + 6f * scale;
        }

        if (board.ArmedPower != CoilPower.None)
        {
            DrawChip(drawList, cursor, chipSize, board.ArmedPower, 1f, true, scale);
        }
    }

    private void DrawChip(ImDrawListPtr drawList, Vector2 min, Vector2 size, CoilPower power, float fraction, bool armed,
        float scale)
    {
        var max = min + size;
        var color = CoilArt.PowerColour(power);
        var radius = size.Y * 0.5f;
        Material.Frosted(drawList, min, max, radius, scale, 0.9f);
        var pulse = armed ? 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 6f) : 0f;
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(color with { W = 0.45f + pulse * 0.45f }), 1.2f * scale);
        var glyphCenter = new Vector2(min.X + radius, min.Y + radius);
        drawList.AddCircleFilled(glyphCenter, radius * 0.72f, ImGui.GetColorU32(GamePalette.Darken(color, 0.55f)));
        CoilArt.PowerGlyph(drawList, glyphCenter, radius * 0.48f, power, ImGui.GetColorU32(color), 1.4f * scale);
        var barMin = new Vector2(min.X + radius * 2f + 2f * scale, min.Y + radius - 2f * scale);
        var barMax = new Vector2(max.X - 8f * scale, barMin.Y + 4f * scale);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 2f * scale);
        var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * Math.Clamp(fraction, 0f, 1f), barMax.Y);
        drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(color with { W = armed ? 0.5f + pulse * 0.5f : 1f }),
            2f * scale);
    }

    private float TimeLeft(CoilPower power) => power switch
    {
        CoilPower.Freeze => board.FreezeLeft,
        CoilPower.Slow => board.SlowLeft,
        CoilPower.Reverse => board.ReverseLeft,
        _ => board.GuideLeft,
    };

    private static float Duration(CoilPower power) => power switch
    {
        CoilPower.Freeze => CoilBoard.FreezeSeconds,
        CoilPower.Slow => CoilBoard.SlowSeconds,
        CoilPower.Reverse => CoilBoard.ReverseSeconds,
        _ => CoilBoard.GuideSeconds,
    };

    private void DrawBanners(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale)
    {
        var upper = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
        if (board.State != CoilState.Ready)
        {
            GameBanner.Draw(drawList, upper, StageLabel(), Accent, theme, stageBanner);
            GameBanner.Draw(drawList, upper + new Vector2(0f, 44f * scale), Loc.T(StageNames[CoilBoard.ShapeFor(board.Stage)]),
                Accent, theme, stageBanner, TextStyles.Headline);
        }

        GameBanner.Draw(drawList, new Vector2(field.Center.X, field.Min.Y + field.Height * 0.36f), comboText,
            new Vector4(1f, 0.86f, 0.42f, 1f), theme, comboBanner, TextStyles.Title1);
        if (board.State != CoilState.StageClear)
        {
            return;
        }

        var progress = board.StageClearProgress;
        var center = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.42f);
        GameBanner.Draw(drawList, center, Loc.T(L.Coil.StageClear), Accent, theme, progress, TextStyles.Title1);
        var alpha = progress < 0.15f ? progress / 0.15f : progress > 0.8f ? (1f - progress) / 0.2f : 1f;
        var counted = (int)(board.ClearBonus * Math.Clamp((progress - 0.12f) * 2.4f, 0f, 1f));
        var bonusCenter = center + new Vector2(0f, 52f * scale);
        Typography.DrawCentered(drawList, bonusCenter, Loc.Upper(Loc.T(L.Coil.Bonus)), theme.TextMuted with { W = alpha },
            TextStyles.Caption1.Scale, TextStyles.Caption1.Weight);
        Typography.DrawCentered(drawList, bonusCenter + new Vector2(0f, 22f * scale), GameNumber.Label(counted),
            GamePalette.Lighten(Accent, 0.45f) with { W = alpha }, TextStyles.Title2.Scale, TextStyles.Title2.Weight);
    }

    private void DrawReady(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float deltaSeconds, float scale)
    {
        readyAppear = GameJuice.Advance(readyAppear, deltaSeconds, 2.2f);
        var pop = GameJuice.PopIn(readyAppear);
        var width = field.Width * 0.86f;
        var center = new Vector2(field.Center.X, field.Max.Y - field.Height * 0.2f);
        var hintWidth = width - 28f * scale;
        var howTo = Loc.T(L.Coil.HowTo);
        var goal = Loc.T(L.Coil.Goal);
        var howToHeight = Typography.MeasureWrappedBlock(howTo, TextStyles.Footnote, hintWidth).Y;
        var goalHeight = Typography.MeasureWrappedBlock(goal, TextStyles.Footnote, hintWidth).Y;
        var height = 44f * scale + howToHeight + goalHeight + 30f * scale;
        var half = new Vector2(width, height) * 0.5f * (0.9f + 0.1f * pop);
        var alpha = MathF.Min(1f, readyAppear * 2f);
        Material.Frosted(drawList, center - half, center + half, 20f * scale, scale, alpha);
        Squircle.Stroke(drawList, center - half, center + half, 20f * scale, ImGui.GetColorU32(Accent with { W = 0.45f * alpha }),
            1.2f * scale);
        var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
        var top = center.Y - half.Y + 24f * scale;
        Typography.DrawCentered(drawList, new Vector2(center.X, top), Loc.T(L.Games.TapToStart),
            theme.TextStrong with { W = alpha }, TextStyles.Title2.Scale * pulse, TextStyles.Title2.Weight);
        var cursor = top + 26f * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, cursor + howToHeight * 0.5f), howTo,
            theme.TextMuted with { W = alpha }, TextStyles.Footnote, hintWidth);
        cursor += howToHeight + 8f * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, cursor + goalHeight * 0.5f), goal,
            theme.TextMuted with { W = alpha }, TextStyles.Footnote, hintWidth);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), resultLine, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            NewRun(true);
        }
    }
}

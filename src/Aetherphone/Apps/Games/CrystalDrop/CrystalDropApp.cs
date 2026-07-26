using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.CrystalDrop;

internal sealed class CrystalDropApp : IMiniGame
{
    private const string GameId = "crystaldrop";
    private readonly CrystalDropBoard board = new();
    private readonly CrystalDropRenderer renderer = new();
    private readonly ParticleSystem particles = new(320);
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private float pointerX = 0.5f;
    private float resultAppear;
    private bool statsLoaded;
    private bool wasOver;
    private bool pendingSubmit;
    private bool newBest;
    private int bestScore;
    private int finalScore;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.CrystalDrop);
    public string Genre => Loc.T(L.Games.GenrePuzzle);

    public void Open()
    {
        statsLoaded = false;
        StartGame();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartGame()
    {
        board.Reset();
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        pointerX = 0.5f;
        resultAppear = 0f;
        wasOver = false;
        pendingSubmit = false;
        newBest = false;
    }

    public void Draw(in GameContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        var deltaSeconds = fx.ScaleDelta(context.DeltaSeconds);
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, finalScore);
            if (newBest)
            {
                bestScore = finalScore;
            }

            pendingSubmit = false;
        }

        var area = new Rect(new Vector2(body.Min.X + 6f * scale, body.Min.Y + 62f * scale),
            new Vector2(body.Max.X - 6f * scale, body.Max.Y - 4f * scale));
        var jar = CrystalDropRenderer.JarOf(area, scale);
        board.Step(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        ConsumeMerges(jar, scale);
        if (board.GameOver && !wasOver)
        {
            OnGameOver(jar, scale);
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        HandleInput(body, jar, theme, scale);
        renderer.Draw(board, jar, pointerX, Accent, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        DrawNextPreview(drawList, body, theme, scale);
        DrawHud(body, theme, scale, deltaSeconds);
        if (board.OverflowSeconds > 0f && !board.GameOver)
        {
            DrawUrgency(drawList, body, board.OverflowFraction, scale);
        }

        if (board.GameOver)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleInput(Rect body, Rect jar, PhoneTheme theme, float scale)
    {
        if (GameHud.RestartButton(new Vector2(body.Max.X - 22f * scale, body.Min.Y + 30f * scale), 16f * scale, theme))
        {
            StartGame();
            return;
        }

        if (board.GameOver)
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        if (body.Contains(mouse))
        {
            pointerX = (mouse.X - jar.Min.X) / jar.Width;
            if (board.CanDrop)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !body.Contains(mouse))
        {
            return;
        }

        var tier = board.HeldTier;
        if (!board.Drop(pointerX))
        {
            return;
        }

        var radius = CrystalDropBoard.RadiusOf(tier) * jar.Width;
        var origin = new Vector2(jar.Min.X + board.ClampDropX(pointerX) * jar.Width, jar.Min.Y - radius);
        particles.Burst(origin, 5, CrystalDropRenderer.TierColor(tier), 60f * scale, 1.8f, 0.35f, 40f);
    }

    private void ConsumeMerges(Rect jar, float scale)
    {
        for (var index = 0; index < board.MergeCount; index++)
        {
            var merge = board.Merge(index);
            var center = CrystalDropRenderer.ToScreen(jar, merge.Position);
            var color = CrystalDropRenderer.TierColor(merge.Tier);
            var weight = 0.4f + merge.Tier * 0.09f;
            fx.Shockwave(center, (40f + merge.Tier * 16f) * scale, GamePalette.Lighten(color, 0.4f), 0.45f, 2.8f);
            particles.Burst(center, 8 + merge.Tier, color, (110f + merge.Tier * 22f) * scale, 2.4f, 0.5f, 260f);
            particles.Sparkle(center, 5 + merge.Tier, GamePalette.Lighten(color, 0.5f), 120f * scale, 2.2f, 0.6f);
            fx.AddTrauma(MathF.Min(0.5f, 0.05f + weight * 0.16f));
            fx.AddText("+" + GameNumber.Label(merge.Points), center, GamePalette.Lighten(color, 0.45f),
                1f + merge.Tier * 0.045f);
            if (merge.Chain > 1)
            {
                fx.AddText("x" + GameNumber.Label(merge.Chain), center + new Vector2(0f, -22f * scale),
                    new Vector4(1f, 0.92f, 0.6f, 1f), 1.05f);
            }

            if (merge.Cleared)
            {
                fx.Flash(GamePalette.Lighten(color, 0.4f), 0.4f);
                fx.HitStop(0.06f);
                particles.Streaks(center, 16, GamePalette.Lighten(color, 0.5f), 420f * scale, 2.8f, 0.6f);
            }
        }

        board.ClearMerges();
    }

    private void OnGameOver(Rect jar, float scale)
    {
        wasOver = true;
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.45f);
        fx.AddTrauma(0.55f);
        particles.Burst(new Vector2(jar.Center.X, jar.Min.Y + jar.Width * CrystalDropBoard.DangerLine), 24,
            new Vector4(0.96f, 0.42f, 0.42f, 1f), 260f * scale, 3.2f, 0.7f, 300f);
    }

    private static void DrawUrgency(ImDrawListPtr drawList, Rect body, float fraction, float scale)
    {
        var pulse = (0.06f + 0.16f * fraction) * (0.6f + 0.4f * Pulse.Wave(Pulse.Fast));
        drawList.AddRect(body.Min + new Vector2(2f * scale, 2f * scale), body.Max - new Vector2(2f * scale, 2f * scale),
            ImGui.GetColorU32(new Vector4(0.95f, 0.3f, 0.3f, pulse)), 14f * scale, ImDrawFlags.RoundCornersAll,
            5f * scale);
    }

    private void DrawNextPreview(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale)
    {
        var label = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Next));
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var height = 46f * scale;
        var width = MathF.Max(labelSize.X + 22f * scale, 54f * scale);
        var center = new Vector2(body.Min.X + 30f * scale + width * 0.5f, body.Min.Y + 30f * scale);
        var half = new Vector2(width * 0.5f, height * 0.5f);
        Material.Frosted(drawList, center - half, center + half, height * 0.5f, scale);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 12f * scale), label, theme.TextMuted,
            TextStyles.Caption2);
        CrystalDropRenderer.DrawCrystal(drawList, new Vector2(center.X, center.Y - 7f * scale), 11f * scale,
            board.NextTier, scale);
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale, float deltaSeconds)
    {
        var rowY = body.Min.Y + 30f * scale;
        var beatingBest = board.Score > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X + 4f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        string? secondary = null;
        if (bestScore > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestScore)}";
        }

        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(finalScore), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}

using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleShooterApp : IMiniGame
{
    private const string GameId = "bubbles";
    private const int ComboChipThreshold = 2;
    private readonly BubbleBoard board = new();
    private readonly BubbleRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private bool started;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private int loadedBest;
    private int displayBest;
    private float resultAppear;
    private float lastFieldHeight = 1.7f;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Bubbles);
    public string Genre => Loc.T(L.Games.GenreArcade);

    public void Open()
    {
        loadedBest = 0;
        started = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame(float fieldHeight)
    {
        board.Reset(fieldHeight);
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        finished = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        displayBest = loadedBest;
        started = true;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBest == 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
            displayBest = loadedBest;
        }

        var rowY = body.Min.Y + 30f * scale;
        var pad = 6f * scale;
        var field = new Rect(new Vector2(body.Min.X + pad, rowY + 26f * scale),
            new Vector2(body.Max.X - pad, body.Max.Y - pad));
        var factor = field.Width;
        var fieldHeight = field.Height / factor;
        lastFieldHeight = fieldHeight;
        if (!started)
        {
            StartNewGame(fieldHeight);
        }

        board.SetFieldHeight(fieldHeight);
        if (pendingSubmit)
        {
            context.Stats.SubmitScore(GameId, board.Score);
            if (board.Score > loadedBest)
            {
                loadedBest = board.Score;
            }

            pendingSubmit = false;
        }

        var aim = ComputeAim(field, factor);
        if (!finished)
        {
            if (board.GameOver)
            {
                finished = true;
                resultAppear = 0f;
                newBest = board.Score > loadedBest;
                pendingSubmit = true;
            }
            else
            {
                HandleInput(field, factor, scale, aim);
                board.Update(fx.ScaleDelta(deltaSeconds));
                ReactToEvents(field, factor, scale);
            }
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (board.Score > displayBest)
        {
            displayBest = board.Score;
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(body.Center.X - 62f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        GameHud.Pill(new Vector2(body.Center.X + 26f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(displayBest),
            Accent, theme, displayBest > loadedBest);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame(fieldHeight);
            return;
        }

        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);
        GameScene.Arena(drawList, shakenField, BubbleRenderer.ArenaRounding * scale, scale, Accent);
        renderer.Draw(board, shakenField, scale, finished ? Vector2.Zero : aim, theme);
        DrawDangerRim(drawList, shakenField, scale);
        DrawComboChip(drawList, board, shakenField, factor, scale, theme);
        fx.DrawFlash(drawList, field, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (finished)
        {
            DrawResult(theme, body);
        }
    }

    private Vector2 ComputeAim(Rect field, float factor)
    {
        var mouseNorm = (ImGui.GetMousePos() - field.Min) / factor;
        var direction = mouseNorm - board.LauncherPosition;
        if (direction.LengthSquared() < 0.0001f)
        {
            return new Vector2(0f, -1f);
        }

        if (direction.Y > -0.2f)
        {
            direction.Y = -0.2f;
        }

        return Vector2.Normalize(direction);
    }

    private void HandleInput(Rect field, float factor, float scale, Vector2 aim)
    {
        if (!UiInteract.Hover(field.Min, field.Max))
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            if (board.Swap())
            {
                var launcher = field.Min + board.LauncherPosition * factor;
                fx.Shockwave(launcher, board.Radius * factor * 1.8f, new Vector4(1f, 1f, 1f, 0.7f), 0.24f, 2f);
            }

            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            board.Fire(aim);
        }
    }

    private void ReactToEvents(Rect field, float factor, float scale)
    {
        if (board.BlastedThisShot)
        {
            var blast = field.Min + board.BlastPosition * factor;
            fx.Shockwave(blast, board.Radius * factor * 6f, new Vector4(1f, 0.70f, 0.32f, 1f), 0.45f, 4f);
            fx.Flash(new Vector4(1f, 0.72f, 0.36f, 0.42f), 0.55f);
            fx.AddTrauma(0.55f);
            fx.HitStop(0.07f);
            particles.Sparkle(blast, 20, new Vector4(1f, 0.86f, 0.52f, 1f), 240f * scale, 3.2f, 0.65f);
        }

        if (board.PopCount == 0)
        {
            return;
        }

        var sum = Vector2.Zero;
        for (var index = 0; index < board.PopCount; index++)
        {
            var center = field.Min + board.PopPosition(index) * factor;
            sum += center;
            var color = BubbleRenderer.ColorOf(board.PopColor(index));
            particles.Burst(center, 9, color, 150f * scale, 2.8f, 0.5f, 280f);
            fx.Shockwave(center, 26f * scale, GamePalette.Lighten(color, 0.3f), 0.3f, 2f);
        }

        var burstCenter = sum / board.PopCount;
        fx.AddTrauma(MathF.Min(0.35f, 0.04f * board.PopCount));
        if (board.LastShotScore > 0)
        {
            fx.AddText($"+{GameNumber.Label(board.LastShotScore)}", burstCenter, Accent, 1.25f);
        }

        if (board.Combo >= ComboChipThreshold)
        {
            fx.AddText($"x{GameNumber.Label(board.Combo)}", burstCenter - new Vector2(0f, 22f * scale),
                new Vector4(1f, 0.86f, 0.42f, 1f), 1.15f);
        }

        if (board.PopCount >= 5)
        {
            fx.HitStop(0.05f);
            particles.Sparkle(burstCenter, 10, new Vector4(1f, 0.95f, 0.7f, 1f), 160f * scale, 2.4f, 0.7f);
        }
    }

    private void DrawDangerRim(ImDrawListPtr drawList, Rect field, float scale)
    {
        if (board.DangerLevel <= 0.35f)
        {
            return;
        }

        var intensity = (board.DangerLevel - 0.35f) / 0.65f;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 4.5f);
        var alpha = intensity * (0.25f + pulse * 0.35f);
        Squircle.Stroke(drawList, field.Min, field.Max, BubbleRenderer.ArenaRounding * scale,
            ImGui.GetColorU32(new Vector4(0.97f, 0.30f, 0.32f, alpha)), 2.2f * scale);
    }

    private void DrawComboChip(ImDrawListPtr drawList, BubbleBoard target, Rect field, float factor, float scale,
        PhoneTheme theme)
    {
        if (target.Combo < ComboChipThreshold || finished)
        {
            return;
        }

        var launcher = field.Min + target.LauncherPosition * factor;
        var center = new Vector2(launcher.X, launcher.Y - target.Radius * factor * 2.4f);
        var label = $"x{GameNumber.Label(target.Combo)}";
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var half = new Vector2(textSize.X * 0.5f + 9f * scale, 10f * scale);
        Material.Frosted(drawList, center - half, center + half, half.Y, scale);
        Squircle.Stroke(drawList, center - half, center + half, half.Y,
            ImGui.GetColorU32(new Vector4(1f, 0.86f, 0.42f, 0.75f)), 1.2f * scale);
        Typography.DrawCentered(center, label, new Vector4(1f, 0.90f, 0.55f, 1f), TextStyles.Caption1);
    }

    private void DrawResult(PhoneTheme theme, Rect body)
    {
        resultAppear = MathF.Min(1f, resultAppear + ImGui.GetIO().DeltaTime * 3.4f);
        var bestValue = board.Score > displayBest ? board.Score : displayBest;
        var secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestValue)}";
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.TextStrong, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(lastFieldHeight);
        }
    }
}

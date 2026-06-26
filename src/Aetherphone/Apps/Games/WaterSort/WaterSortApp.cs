using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.WaterSort;

internal sealed class WaterSortApp : IMiniGame
{
    private const string GameId = "watersort";

    private const float PourDuration = 0.26f;

    private readonly WaterSortBoard board = new();

    private readonly WaterSortRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private Spring liftSpring = new(0f);

    private bool statsLoaded;

    private int bestCleared;

    private int currentLevel = 1;

    private bool pouring;

    private float pourTimer;

    private Rect pourFrom;

    private Rect pourTo;

    private Vector4 pourColor;

    private bool finished;

    private bool newBest;

    private bool pendingSubmit;

    private int clearedLevel;

    private float resultAppear;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.WaterSort);

    public string Genre => Loc.T(L.Games.GenrePuzzle);

    public Vector4 Accent => new(0.40f, 0.68f, 0.98f, 1f);

    public void Open()
    {
        statsLoaded = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartLevel(int level)
    {
        currentLevel = level;
        board.Reset(level);
        particles.Clear();
        fx.Clear();
        liftSpring.SnapTo(0f);
        pouring = false;
        pourTimer = 0f;
        finished = false;
        newBest = false;
        pendingSubmit = false;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;

        if (!statsLoaded)
        {
            bestCleared = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
            StartLevel(bestCleared + 1);
        }

        if (pendingSubmit)
        {
            context.Stats.SubmitScore(GameId, clearedLevel);
            pendingSubmit = false;
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        var lift = liftSpring.Step(board.Selected >= 0 ? 10f * scale : 0f, 0.06f, deltaSeconds);

        if (pouring)
        {
            pourTimer += deltaSeconds / PourDuration;
            if (pourTimer >= 1f)
            {
                pouring = false;
            }
        }

        var rowY = body.Min.Y + 30f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 48f * scale, rowY), Loc.T(L.Games.Level), GameNumber.Label(currentLevel), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 48f * scale, rowY), Loc.T(L.Games.Moves), GameNumber.Label(board.Moves), Accent, theme);

        if (GameHud.Button(new Vector2(body.Min.X + 36f * scale, rowY), new Vector2(58f * scale, 28f * scale), Loc.T(L.Games.Undo), theme.SurfaceMuted, theme))
        {
            board.Undo();
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartLevel(currentLevel);
            return;
        }

        var area = new Rect(new Vector2(body.Min.X + 6f * scale, rowY + 28f * scale), new Vector2(body.Max.X - 6f * scale, body.Max.Y - 8f * scale));

        if (!finished)
        {
            HandleClick(area, scale);
        }

        renderer.Draw(board, area, scale, theme, lift);

        if (pouring)
        {
            renderer.DrawPourStream(pourFrom, pourTo, pourColor, MathF.Min(1f, pourTimer), scale);
        }

        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
        fx.DrawText();

        if (finished)
        {
            DrawResult(theme, body, scale);
        }
    }

    private void HandleClick(Rect area, float scale)
    {
        var mouse = ImGui.GetMousePos();
        var hoveredTube = -1;
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            if (WaterSortRenderer.TubeRect(area, tube, board.TubeCount, scale).Contains(mouse))
            {
                hoveredTube = tube;
                break;
            }
        }

        if (hoveredTube < 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var action = board.ClickTube(hoveredTube);
        if (action == TubeAction.Poured)
        {
            OnPoured(area, scale);
        }
    }

    private void OnPoured(Rect area, float scale)
    {
        var pour = board.LastPour;
        pourFrom = WaterSortRenderer.TubeRect(area, pour.FromTube, board.TubeCount, scale);
        pourTo = WaterSortRenderer.TubeRect(area, pour.ToTube, board.TubeCount, scale);
        pourColor = WaterSortRenderer.ColorOf(pour.Color);
        pouring = true;
        pourTimer = 0f;

        var splash = new Vector2(pourTo.Center.X, pourTo.Min.Y + pourTo.Height * 0.2f);
        particles.Burst(splash, 9, pourColor, 130f * scale, 2.6f, 0.45f, 320f);

        if (board.IsSolved())
        {
            OnSolved(area, scale);
        }
    }

    private void OnSolved(Rect area, float scale)
    {
        finished = true;
        resultAppear = 0f;
        clearedLevel = currentLevel;
        newBest = clearedLevel > bestCleared;
        if (newBest)
        {
            bestCleared = clearedLevel;
        }

        pendingSubmit = true;
        fx.AddTrauma(0.25f);

        ReadOnlySpan<Vector4> palette = new[]
        {
            Accent,
            Styling.AccentMint,
            Styling.AccentAmber,
            Styling.AccentPink,
        };
        particles.Confetti(new Vector2(area.Center.X, area.Min.Y), 64, palette, 260f * scale, 4f, 1.3f);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float scale)
    {
        resultAppear = MathF.Min(1f, resultAppear + ImGui.GetIO().DeltaTime * 3.4f);

        var secondary = $"{GameNumber.Label(board.Moves)} {Loc.T(L.Games.Moves)}";
        var result = new GameResult(
            Loc.T(L.Games.YouWin),
            Accent,
            Loc.T(L.Games.Level),
            GameNumber.Label(clearedLevel),
            secondary,
            newBest,
            Loc.T(L.Games.NextLevel));

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartLevel(currentLevel + 1);
        }
    }
}

using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Solitaire;

internal sealed class SolitaireApp : IMiniGame
{
    private const string GameId = "solitaire";
    private const float DragThreshold = 5f;
    private readonly SolitaireBoard board = new();
    private readonly SolitaireRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly int[] grabbed = new int[13];
    private SolitaireHit grabSource = SolitaireHit.None;
    private int grabCount;
    private Vector2 grabOffset;
    private Vector2 pressPosition;
    private bool dragMoved;
    private float elapsed;
    private int lastSeconds = -1;
    private string timeText = "0:00";
    private bool finished;
    private bool pendingSubmit;
    private bool newBestTime;
    private int loadedBestTime;
    private float resultAppear;
    private string resultTimeText = "0:00";
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Solitaire);
    public string Genre => Loc.T(L.Games.GenreCards);
    public void Open()
    {
        StartNewGame();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame()
    {
        board.Deal();
        particles.Clear();
        fx.Clear();
        grabSource = SolitaireHit.None;
        grabCount = 0;
        dragMoved = false;
        elapsed = 0f;
        lastSeconds = -1;
        finished = false;
        pendingSubmit = false;
        newBestTime = false;
        loadedBestTime = -1;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBestTime < 0)
        {
            loadedBestTime = context.Stats.Get(GameId).BestTimeSeconds;
        }

        if (!finished)
        {
            elapsed += deltaSeconds;
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (pendingSubmit)
        {
            newBestTime = context.Stats.SubmitTime(GameId, (int)elapsed);
            pendingSubmit = false;
        }

        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        DrawHud(body, theme, scale);
        var area = new Rect(new Vector2(body.Min.X, body.Min.Y + 56f * scale),
            new Vector2(body.Max.X, body.Max.Y - 6f * scale));
        var layout = SolitaireLayout.Compute(area, board, scale);
        var dropTarget = SolitaireHit.None;
        if (!finished)
        {
            dropTarget = HandleInput(layout, scale);
        }

        renderer.Draw(board, layout, theme, Accent, scale, grabSource, dropTarget);
        if (grabCount > 0)
        {
            var topLeft = ImGui.GetMousePos() - grabOffset;
            renderer.DrawFloating(layout, new ReadOnlySpan<int>(grabbed, 0, grabCount), topLeft, scale);
        }

        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale)
    {
        var rowY = body.Min.Y + 26f * scale;
        var seconds = (int)elapsed;
        if (seconds != lastSeconds)
        {
            lastSeconds = seconds;
            timeText = $"{seconds / 60}:{seconds % 60:D2}";
        }

        GameHud.Pill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Time), timeText, Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Moves),
            GameNumber.Label(board.Moves), Accent, theme);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 15f * scale, theme))
        {
            StartNewGame();
        }
    }

    private SolitaireHit HandleInput(in SolitaireLayout layout, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (grabCount == 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var hit = layout.Hit(mouse);
            if (hit.Kind == SolitairePileKind.Stock)
            {
                if (board.DrawStock())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
            else
            {
                BeginGrab(hit, mouse, layout);
            }
        }

        if (grabCount == 0)
        {
            return SolitaireHit.None;
        }

        if (Vector2.Distance(mouse, pressPosition) > DragThreshold * scale)
        {
            dragMoved = true;
        }

        var dropTarget = ComputeDropTarget(layout, mouse);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ReleaseGrab(layout, mouse, dropTarget);
            return SolitaireHit.None;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return dropTarget;
    }

    private void BeginGrab(in SolitaireHit hit, Vector2 mouse, in SolitaireLayout layout)
    {
        grabCount = 0;
        if (hit.Kind == SolitairePileKind.Waste)
        {
            var card = board.WasteTop();
            if (card < 0)
            {
                return;
            }

            grabbed[0] = card;
            grabCount = 1;
            grabOffset = mouse - layout.WasteRect.Min;
        }
        else if (hit.Kind == SolitairePileKind.Foundation)
        {
            var card = board.FoundationTop(hit.Pile);
            if (card < 0)
            {
                return;
            }

            grabbed[0] = card;
            grabCount = 1;
            grabOffset = mouse - layout.FoundationRect(hit.Pile).Min;
        }
        else if (hit.Kind == SolitairePileKind.Tableau)
        {
            if (hit.CardIndex < 0 || !board.IsTableauFaceUp(hit.Pile, hit.CardIndex) ||
                !board.IsRunStart(hit.Pile, hit.CardIndex))
            {
                return;
            }

            var count = board.TableauCount(hit.Pile);
            for (var index = hit.CardIndex; index < count; index++)
            {
                grabbed[grabCount++] = board.TableauCardAt(hit.Pile, index);
            }

            grabOffset = mouse - layout.TableauCardRect(hit.Pile, hit.CardIndex).Min;
        }

        if (grabCount > 0)
        {
            grabSource = hit;
            pressPosition = mouse;
            dragMoved = false;
        }
    }

    private SolitaireHit ComputeDropTarget(in SolitaireLayout layout, Vector2 mouse)
    {
        if (grabCount == 0)
        {
            return SolitaireHit.None;
        }

        var hit = layout.Hit(mouse);
        var first = grabbed[0];
        if (hit.Kind == SolitairePileKind.Foundation && grabCount == 1 &&
            grabSource.Kind != SolitairePileKind.Foundation && board.CanFoundation(first))
        {
            return new SolitaireHit(SolitairePileKind.Foundation, SolitaireBoard.Suit(first), -1);
        }

        if (hit.Kind == SolitairePileKind.Tableau)
        {
            if (grabSource.Kind == SolitairePileKind.Tableau && grabSource.Pile == hit.Pile)
            {
                return SolitaireHit.None;
            }

            if (board.CanTableau(first, hit.Pile))
            {
                return new SolitaireHit(SolitairePileKind.Tableau, hit.Pile, board.TableauCount(hit.Pile) - 1);
            }
        }

        return SolitaireHit.None;
    }

    private void ReleaseGrab(in SolitaireLayout layout, Vector2 mouse, in SolitaireHit dropTarget)
    {
        var acted = false;
        if (!dragMoved)
        {
            acted = TapToFoundation();
        }
        else if (dropTarget.Kind == SolitairePileKind.Foundation)
        {
            acted = DropToFoundation();
        }
        else if (dropTarget.Kind == SolitairePileKind.Tableau)
        {
            acted = DropToTableau(dropTarget.Pile);
        }

        if (acted)
        {
            AfterMove(layout);
        }

        grabSource = SolitaireHit.None;
        grabCount = 0;
        dragMoved = false;
    }

    private bool TapToFoundation()
    {
        if (grabSource.Kind == SolitairePileKind.Waste)
        {
            return board.SendWasteToFoundation();
        }

        if (grabSource.Kind == SolitairePileKind.Tableau && grabCount == 1)
        {
            return board.SendTableauToFoundation(grabSource.Pile);
        }

        return false;
    }

    private bool DropToFoundation()
    {
        if (grabSource.Kind == SolitairePileKind.Waste)
        {
            return board.SendWasteToFoundation();
        }

        if (grabSource.Kind == SolitairePileKind.Tableau)
        {
            return board.SendTableauToFoundation(grabSource.Pile);
        }

        return false;
    }

    private bool DropToTableau(int destPile)
    {
        if (grabSource.Kind == SolitairePileKind.Waste)
        {
            return board.MoveWasteToTableau(destPile);
        }

        if (grabSource.Kind == SolitairePileKind.Foundation)
        {
            return board.MoveFoundationToTableau(grabSource.Pile, destPile);
        }

        if (grabSource.Kind == SolitairePileKind.Tableau)
        {
            return board.MoveTableauToTableau(grabSource.Pile, grabSource.CardIndex, destPile);
        }

        return false;
    }

    private void AfterMove(in SolitaireLayout layout)
    {
        var card = grabbed[0];
        var suit = SolitaireBoard.Suit(card);
        if (board.FoundationTop(suit) == card)
        {
            var scale = ImGuiHelpers.GlobalScale;
            var center = layout.FoundationRect(suit).Center;
            particles.Burst(center, 12, Accent, 150f * scale, 3f, 0.5f, 220f);
            particles.Sparkle(center, 5, new Vector4(1f, 0.95f, 0.65f, 1f), 110f * scale, 2f, 0.6f);
            fx.Shockwave(center, 40f * scale, GamePalette.Lighten(Accent, 0.3f), 0.4f, 2.4f);
            fx.AddTrauma(0.08f);
        }

        if (board.LastFlippedPile >= 0)
        {
            var pile = board.LastFlippedPile;
            var top = board.TableauCount(pile) - 1;
            if (top >= 0)
            {
                var center = layout.TableauCardRect(pile, top).Center;
                particles.Burst(center, 8, Styling.AccentAmber, 120f * ImGuiHelpers.GlobalScale, 2.6f, 0.45f, 220f);
            }
        }

        if (board.IsWon)
        {
            OnWon(layout);
        }
    }

    private void OnWon(in SolitaireLayout layout)
    {
        finished = true;
        resultAppear = 0f;
        pendingSubmit = true;
        var seconds = (int)elapsed;
        resultTimeText = $"{seconds / 60}:{seconds % 60:D2}";
        fx.AddTrauma(0.4f);
        fx.Flash(Accent, 0.4f);
        ReadOnlySpan<Vector4> palette = new[] { Accent, Styling.AccentAmber, Styling.AccentRose, Styling.AccentBlue, };
        var top = new Vector2(layout.OriginX + layout.ColumnPitch * 3f, layout.TopRowY);
        particles.Confetti(top, 90, palette, 280f * ImGuiHelpers.GlobalScale, 4.4f, 1.5f);
        particles.Sparkle(top + new Vector2(0f, 60f * ImGuiHelpers.GlobalScale), 18, new Vector4(1f, 0.95f, 0.7f, 1f),
            210f * ImGuiHelpers.GlobalScale, 2.8f, 1f);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        string? secondary = null;
        if (loadedBestTime > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {loadedBestTime / 60}:{loadedBestTime % 60:D2}";
        }

        var result = new GameResult(Loc.T(L.Games.YouWin), Accent, Loc.T(L.Games.Time), resultTimeText, secondary,
            newBestTime);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}

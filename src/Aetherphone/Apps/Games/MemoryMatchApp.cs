using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal enum CardLogical
{
    FaceDown,
    FaceUp,
    Matched,
}

internal enum AnimPhase
{
    Selecting,
    Revealing,
    Celebrating,
    Shaking,
    FlippingBack,
    Won,
}

internal sealed class MemoryMatchApp : IPhoneApp
{
    private const int Columns = 4;
    private const int Rows = 4;
    private const int TotalCards = Columns * Rows;
    private const float FlipDuration = 0.22f;
    private const float RevealDuration = 0.55f;
    private const float CelebrateDuration = 0.45f;
    private const float ShakeDuration = 0.35f;

    public string Id => "memory";
    public string DisplayName => "Pairs";
    public string Glyph => "P";
    public Vector4 Accent => Styling.AccentAmber;
    public int BadgeCount => 0;

    private readonly int[] symbols = new int[TotalCards];
    private readonly CardLogical[] logical = new CardLogical[TotalCards];
    private readonly float[] flipProgress = new float[TotalCards];
    private readonly float[] flipTarget = new float[TotalCards];
    private readonly float[] matchGlow = new float[TotalCards];
    private readonly float[] shakePhase = new float[TotalCards];
    private readonly Random rng = new();

    private int firstCard = -1;
    private int secondCard = -1;
    private AnimPhase phase;
    private int attempts;
    private float phaseTimer;
    private DateTime startTime;

    private PhoneTheme frameTheme = PhoneTheme.Default;

    public void OnOpened() => ResetGame();

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        var body = GameCommon.LayoutBelowHeader(context.Content);

        AppHeader.Draw(context, DisplayName);

        using (AppSurface.Begin(body))
        {
            var dt = ImGui.GetIO().DeltaTime;
            var contentMax = new Vector2(body.Min.X, body.Min.Y) + ImGui.GetContentRegionAvail();
            var surface = new Rect(new Vector2(body.Min.X, body.Min.Y), contentMax);

            UpdateAnimations(dt);

            var scale = ImGuiHelpers.GlobalScale;
            var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;

            var statsY = body.Min.Y + 30f * scale;
            var statsSpacing = 90f * scale;
            GameCommon.DrawScorePill(new Vector2(surface.Center.X - statsSpacing * 0.5f, statsY), "Attempts", attempts, frameTheme);
            GameCommon.DrawScorePill(new Vector2(surface.Center.X + statsSpacing * 0.5f, statsY), "Time", elapsed, frameTheme);

            var gridTop = statsY + 36f * scale;
            var gridArea = new Rect(new Vector2(surface.Min.X, gridTop), surface.Max);
            var grid = GameCommon.LayoutGameGrid(gridArea, Columns, Rows, 0.10f);

            DrawCardGrid(grid);

            if (phase == AnimPhase.Won)
            {
                if (GameCommon.DrawWinOverlay(surface, frameTheme, attempts, elapsed))
                {
                    ResetGame();
                }
            }
        }
    }

    private void ResetGame()
    {
        for (var index = 0; index < TotalCards; index++)
        {
            symbols[index] = index / 2;
            logical[index] = CardLogical.FaceDown;
            flipProgress[index] = 0f;
            flipTarget[index] = 0f;
            matchGlow[index] = 0f;
            shakePhase[index] = 0f;
        }

        Shuffle();
        firstCard = -1;
        secondCard = -1;
        phase = AnimPhase.Selecting;
        attempts = 0;
        phaseTimer = 0f;
        startTime = DateTime.Now;
    }

    private void Shuffle()
    {
        for (var index = TotalCards - 1; index > 0; index--)
        {
            var swap = rng.Next(index + 1);
            (symbols[index], symbols[swap]) = (symbols[swap], symbols[index]);
        }
    }

    private void UpdateAnimations(float dt)
    {
        for (var index = 0; index < TotalCards; index++)
        {
            var target = flipTarget[index];
            if (MathF.Abs(flipProgress[index] - target) < 0.001f)
            {
                flipProgress[index] = target;
                continue;
            }

            var step = dt / FlipDuration;
            flipProgress[index] = Math.Max(0f, Math.Min(1f,
                flipProgress[index] + (target > flipProgress[index] ? step : -step)));
        }

        for (var index = 0; index < TotalCards; index++)
        {
            if (matchGlow[index] > 0f)
            {
                matchGlow[index] = MathF.Max(0f, matchGlow[index] - dt / CelebrateDuration);
            }
        }

        for (var index = 0; index < TotalCards; index++)
        {
            if (shakePhase[index] > 0f)
            {
                shakePhase[index] = MathF.Max(0f, shakePhase[index] - dt / ShakeDuration);
            }
        }

        switch (phase)
        {
            case AnimPhase.Revealing:
                phaseTimer += dt;
                if (phaseTimer >= RevealDuration)
                {
                    ResolveMatch();
                }

                break;

            case AnimPhase.Celebrating:
                phaseTimer += dt;
                if (phaseTimer >= CelebrateDuration)
                {
                    logical[firstCard] = CardLogical.Matched;
                    logical[secondCard] = CardLogical.Matched;
                    firstCard = -1;
                    secondCard = -1;
                    phase = AllMatched() ? AnimPhase.Won : AnimPhase.Selecting;
                }

                break;

            case AnimPhase.Shaking:
                phaseTimer += dt;
                if (phaseTimer >= ShakeDuration)
                {
                    flipTarget[firstCard] = 0f;
                    flipTarget[secondCard] = 0f;
                    phase = AnimPhase.FlippingBack;
                    phaseTimer = 0f;
                }

                break;

            case AnimPhase.FlippingBack:
            {
                var bothDone = flipProgress[firstCard] <= 0.001f && flipProgress[secondCard] <= 0.001f;
                if (bothDone)
                {
                    logical[firstCard] = CardLogical.FaceDown;
                    logical[secondCard] = CardLogical.FaceDown;
                    flipProgress[firstCard] = 0f;
                    flipProgress[secondCard] = 0f;
                    firstCard = -1;
                    secondCard = -1;
                    phase = AnimPhase.Selecting;
                }

                break;
            }
        }
    }

    private void DrawCardGrid(Rect grid)
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var index = row * Columns + column;
                var (cellMin, cellMax) = GameCommon.CellBounds(grid, column, row, Columns, Rows, 0.10f);
                DrawCard(cellMin, cellMax, index);
            }
        }
    }

    private void DrawCard(Vector2 min, Vector2 max, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = 8f * scale;
        var center = (min + max) * 0.5f;
        var fullWidth = max.X - min.X;

        var shake = shakePhase[index];
        var shakeX = 0f;
        if (shake > 0f)
        {
            var decay = shake;
            shakeX = MathF.Sin(shake * MathF.PI * 8f) * 5f * scale * decay;
        }

        var glow = matchGlow[index];

        var fProg = flipProgress[index];
        var flipScale = MathF.Abs(MathF.Cos(fProg * MathF.PI));
        var showBack = fProg < 0.5f;

        var showFaceDown = showBack && logical[index] != CardLogical.Matched;
        var showFaceUp = !showBack && logical[index] != CardLogical.FaceDown;

        if (!showFaceDown && !showFaceUp && logical[index] == CardLogical.Matched)
        {
            showFaceUp = true;
            flipScale = 1f;
        }

        var halfScaledWidth = fullWidth * 0.5f * flipScale;
        var drawMin = new Vector2(center.X - halfScaledWidth + shakeX, min.Y);
        var drawMax = new Vector2(center.X + halfScaledWidth + shakeX, max.Y);

        var hovered = flipScale > 0.3f && GameCommon.HitTest(min, max);

        if (showFaceDown)
        {
            DrawFaceDownCard(drawMin, drawMax, rounding, hovered);
        }
        else if (showFaceUp)
        {
            DrawFaceUpCard(drawMin, drawMax, index, rounding, glow);
        }

        if (hovered && phase == AnimPhase.Selecting && logical[index] == CardLogical.FaceDown
            && flipProgress[index] < 0.01f && GameCommon.WasClicked(min, max))
        {
            OnCardClicked(index);
        }
    }

    private void DrawFaceDownCard(Vector2 min, Vector2 max, float rounding, bool hovered)
    {
        var bg = hovered ? GameCommon.CardFaceDownHover : GameCommon.CardFaceDown;
        GameCommon.FillRect(min, max, bg, rounding);
        GameCommon.DrawRect(min, max, Styling.BorderDim, rounding, 1f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var accentGlow = Styling.WithAlpha(frameTheme.Accent, 0.12f);
            GameCommon.FillRect(min, max, accentGlow, rounding);
        }

        Typography.DrawCentered((min + max) * 0.5f, "?", Styling.TextMuted, 1.2f);
    }

    private void DrawFaceUpCard(Vector2 min, Vector2 max, int index, float rounding, float glow)
    {
        var symbolIndex = symbols[index];
        var accent = GameCommon.MatchColors[symbolIndex];
        var center = (min + max) * 0.5f;
        var scale = ImGuiHelpers.GlobalScale;

        GameCommon.FillRect(min, max, GameCommon.CardFaceUp, rounding);
        GameCommon.DrawRect(min, max, Styling.BorderDim, rounding, 1f);

        if (glow > 0.01f)
        {
            var glowWidth = 2f * scale + glow * 4f * scale;
            var glowColor = Styling.WithAlpha(accent, glow * 0.8f);
            GameCommon.DrawRect(min, max, glowColor, rounding, glowWidth);

            var innerGlow = Styling.WithAlpha(accent, glow * 0.18f);
            GameCommon.FillRect(min, max, innerGlow, rounding);

            var pulseScale = 1f + glow * 0.06f * MathF.Sin(glow * MathF.PI * 4f);
            var pulseHalfW = (max.X - min.X) * 0.5f * pulseScale;
            var pulseHalfH = (max.Y - min.Y) * 0.5f * pulseScale;
            var pulseMin = new Vector2(center.X - pulseHalfW, center.Y - pulseHalfH);
            var pulseMax = new Vector2(center.X + pulseHalfW, center.Y + pulseHalfH);
            GameCommon.FillRect(pulseMin, pulseMax, Styling.WithAlpha(accent, glow * 0.10f), rounding);
        }

        var symbol = GameCommon.MatchSymbols[symbolIndex];
        var symbolColor = glow > 0.01f
            ? Vector4.Lerp(accent, new Vector4(1f, 1f, 1f, 1f), glow * 0.6f)
            : accent;

        var symbolSize = Typography.Measure(symbol, 1.6f);
        Typography.DrawCentered(new Vector2(center.X, center.Y - symbolSize.Y * 0.1f), symbol, symbolColor, 1.6f, FontWeight.SemiBold);
    }

    private void OnCardClicked(int index)
    {
        if (logical[index] != CardLogical.FaceDown)
        {
            return;
        }

        if (firstCard < 0)
        {
            firstCard = index;
            logical[index] = CardLogical.FaceUp;
            flipTarget[index] = 1f;
        }
        else
        {
            secondCard = index;
            logical[index] = CardLogical.FaceUp;
            flipTarget[index] = 1f;
            attempts++;
            phase = AnimPhase.Revealing;
            phaseTimer = 0f;
        }
    }

    private void ResolveMatch()
    {
        if (firstCard < 0 || secondCard < 0)
        {
            phase = AnimPhase.Selecting;
            return;
        }

        if (symbols[firstCard] == symbols[secondCard])
        {
            matchGlow[firstCard] = 1f;
            matchGlow[secondCard] = 1f;
            phase = AnimPhase.Celebrating;
            phaseTimer = 0f;
        }
        else
        {
            shakePhase[firstCard] = 1f;
            shakePhase[secondCard] = 1f;
            phase = AnimPhase.Shaking;
            phaseTimer = 0f;
        }
    }

    private bool AllMatched()
    {
        for (var index = 0; index < TotalCards; index++)
        {
            if (logical[index] != CardLogical.Matched)
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
    }
}

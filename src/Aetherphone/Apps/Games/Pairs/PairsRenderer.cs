using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pairs;

internal static class PairsRenderer
{
    public const float GapFraction = 0.10f;
    public const float TrayHeight = 44f;
    public const float TrayGap = 10f;
    private const float CardRadius = 10f;
    private const float SlotRadius = 7f;
    private const float SlotInset = 6f;
    private const float FaceTint = 0.22f;
    private const float GlyphScale = 1.7f;
    private const float TrayGlyphScale = 1.05f;
    private const float MinSquash = 0.05f;
    private static readonly string[] Symbols = { "♥", "★", "◆", "●", "▲", "■", "✦", "♪" };
    private static readonly string BackGlyph = "?";

    private static readonly Vector4[] Colors =
    {
        new(0.95f, 0.45f, 0.78f, 1f), new(0.92f, 0.74f, 0.34f, 1f), new(0.46f, 0.86f, 0.66f, 1f),
        new(0.40f, 0.68f, 0.98f, 1f), new(0.75f, 0.50f, 0.95f, 1f), new(0.93f, 0.42f, 0.50f, 1f),
        new(0.45f, 0.80f, 0.70f, 1f), new(0.90f, 0.55f, 0.35f, 1f),
    };

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 BackInk = GamePalette.InkLight with { W = 0.42f };
    private static readonly Vector4 BackRim = new(1f, 1f, 1f, 0.16f);

    public static Vector4 ColorFor(int symbol) => Colors[symbol % Colors.Length];

    public static Vector4 BackFill(Vector4 accent) => GamePalette.Darken(accent, 0.55f);

    public static Vector4 SlotFill(Vector4 accent) => GamePalette.Darken(accent, 0.74f);

    public static Rect TraySlot(Rect tray, int slot, float scale)
    {
        var pitch = tray.Width / PairsBoard.PairCount;
        var size = MathF.Min(pitch, tray.Height) - SlotInset * scale;
        var center = new Vector2(tray.Min.X + pitch * (slot + 0.5f), tray.Center.Y);
        var half = new Vector2(size * 0.5f, size * 0.5f);
        return new Rect(center - half, center + half);
    }

    public static void DrawTray(ImDrawListPtr drawList, PairsBoard board, Rect tray, float[] flight, Vector4 accent,
        float scale)
    {
        var radius = SlotRadius * scale;
        var fill = SlotFill(accent);
        for (var slot = 0; slot < PairsBoard.PairCount; slot++)
        {
            var rect = TraySlot(tray, slot, scale);
            var symbol = LandedSymbol(board, flight, slot);
            if (symbol < 0)
            {
                StageCell.Draw(drawList, rect, fill, CellDepth.Sunken, radius, scale);
                continue;
            }

            var color = ColorFor(symbol);
            StageCell.Draw(drawList, rect, Vector4.Lerp(fill, color, FaceTint), CellDepth.Raised, radius, scale);
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(color with { W = 0.6f }),
                1f * scale);
            Typography.DrawCentered(drawList, rect.Center, Symbols[symbol % Symbols.Length], color, TrayGlyphScale,
                FontWeight.SemiBold);
        }
    }

    public static void DrawEmptyCell(ImDrawListPtr drawList, Rect cell, Vector4 accent, float scale)
    {
        StageCell.Draw(drawList, cell, SlotFill(accent), CellDepth.Sunken, CardRadius * scale, scale);
    }

    public static void DrawCard(ImDrawListPtr drawList, Rect cell, int symbol, float flip, float glow, float shakeX,
        bool hovered, Vector4 accent, float scale)
    {
        var squash = MathF.Abs(MathF.Cos(flip * MathF.PI));
        if (squash < MinSquash)
        {
            return;
        }

        var halfWidth = cell.Width * 0.5f * squash;
        var min = new Vector2(cell.Center.X - halfWidth + shakeX, cell.Min.Y);
        var max = new Vector2(cell.Center.X + halfWidth + shakeX, cell.Max.Y);
        var rect = new Rect(min, max);
        var radius = CardRadius * scale;
        if (flip < 0.5f)
        {
            DrawBack(drawList, rect, radius, hovered, accent, scale);
            return;
        }

        DrawFace(drawList, rect, radius, symbol, glow, scale);
    }

    public static void DrawFlyer(ImDrawListPtr drawList, Vector2 center, Vector2 size, int symbol, float progress,
        float scale)
    {
        var half = size * 0.5f;
        var rect = new Rect(center - half, center + half);
        var radius = MathF.Min(CardRadius * scale, size.Y * 0.25f);
        var color = ColorFor(symbol);
        StageCell.Draw(drawList, rect, Vector4.Lerp(GamePalette.CellSunken, color, FaceTint + 0.2f), CellDepth.Raised,
            radius, scale);
        Typography.DrawCentered(drawList, center, Symbols[symbol % Symbols.Length], color,
            Easing.Lerp(GlyphScale, TrayGlyphScale, progress), FontWeight.SemiBold);
    }

    private static void DrawBack(ImDrawListPtr drawList, Rect rect, float radius, bool hovered, Vector4 accent,
        float scale)
    {
        var fill = BackFill(accent);
        if (hovered)
        {
            fill = GamePalette.Lighten(fill, 0.12f);
        }

        StageCell.Draw(drawList, rect, fill, CellDepth.Raised, radius, scale);
        var inset = MathF.Min(rect.Width, rect.Height) * 0.12f;
        var innerMin = rect.Min + new Vector2(inset, inset);
        var innerMax = rect.Max - new Vector2(inset, inset);
        var rim = hovered ? accent with { W = 0.75f } : BackRim;
        Squircle.Stroke(drawList, innerMin, innerMax, MathF.Max(2f * scale, radius - inset), ImGui.GetColorU32(rim),
            1.2f * scale);
        Typography.DrawCentered(drawList, rect.Center, BackGlyph, hovered ? accent : BackInk, 1.3f, FontWeight.Bold);
    }

    private static void DrawFace(ImDrawListPtr drawList, Rect rect, float radius, int symbol, float glow, float scale)
    {
        var color = ColorFor(symbol);
        if (glow > 0.01f)
        {
            ProgressRing.Glow(rect.Center, rect.Height * 0.45f, color, glow * 1.1f);
        }

        var tint = Vector4.Lerp(GamePalette.CellSunken, color, FaceTint + glow * 0.25f);
        StageCell.Draw(drawList, rect, tint, CellDepth.Raised, radius, scale);
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(color with { W = 0.7f + glow * 0.3f }),
            (1.2f + glow * 2.4f) * scale);
        var ink = glow > 0.01f ? Vector4.Lerp(color, White, glow * 0.6f) : color;
        Typography.DrawCentered(drawList, rect.Center, Symbols[symbol % Symbols.Length], ink,
            GlyphScale + glow * 0.12f, FontWeight.SemiBold);
    }

    private static int LandedSymbol(PairsBoard board, float[] flight, int slot)
    {
        for (var symbol = 0; symbol < PairsBoard.PairCount; symbol++)
        {
            if (board.TraySlot(symbol) != slot)
            {
                continue;
            }

            return Landed(board, flight, symbol) ? symbol : -1;
        }

        return -1;
    }

    private static bool Landed(PairsBoard board, float[] flight, int symbol)
    {
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            if (board.Symbol(index) == symbol && flight[index] < 1f)
            {
                return false;
            }
        }

        return true;
    }
}

using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pairs;

internal sealed class PairsRenderer
{
    private static readonly string[] Symbols =
    {
        "♥", "★", "◆", "●", "▲", "■", "✦", "♪",
    };

    private static readonly Vector4[] Colors =
    {
        new(0.95f, 0.45f, 0.78f, 1f),
        new(0.92f, 0.74f, 0.34f, 1f),
        new(0.46f, 0.86f, 0.66f, 1f),
        new(0.40f, 0.68f, 0.98f, 1f),
        new(0.75f, 0.50f, 0.95f, 1f),
        new(0.93f, 0.42f, 0.50f, 1f),
        new(0.45f, 0.80f, 0.70f, 1f),
        new(0.90f, 0.55f, 0.35f, 1f),
    };

    public static Vector4 ColorFor(int symbolIndex) => Colors[symbolIndex % Colors.Length];

    public void DrawCard(Rect cell, int symbolIndex, CardState state, float flipProgress, float glow, float shakeX, bool hovered, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 9f * scale;
        var center = cell.Center;
        var fullWidth = cell.Width;

        var flipSquash = MathF.Abs(MathF.Cos(flipProgress * MathF.PI));
        var showBack = flipProgress < 0.5f && state != CardState.Matched;
        if (state == CardState.Matched)
        {
            flipSquash = 1f;
        }

        var halfWidth = fullWidth * 0.5f * flipSquash;
        var min = new Vector2(center.X - halfWidth + shakeX, cell.Min.Y);
        var max = new Vector2(center.X + halfWidth + shakeX, cell.Max.Y);

        if (flipSquash < 0.05f)
        {
            return;
        }

        if (showBack)
        {
            DrawBack(drawList, min, max, rounding, hovered, theme, scale);
        }
        else
        {
            DrawFace(drawList, min, max, center, rounding, symbolIndex, glow, scale);
        }
    }

    private void DrawBack(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered, PhoneTheme theme, float scale)
    {
        Elevation.Card(drawList, min, max, rounding, scale, 0.5f);
        var fill = hovered ? GamePalette.CellHover : GamePalette.Cell;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
        Squircle.Fill(drawList, min, new Vector2(max.X, min.Y + (max.Y - min.Y) * 0.5f), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.04f)));

        if (hovered)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.Accent with { W = 0.14f }));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(theme.Accent with { W = 0.6f }), 1.5f * scale);
        }
        else
        {
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Styling.BorderDim), 1f * scale);
        }

        Typography.DrawCentered((min + max) * 0.5f, "?", theme.TextMuted, 1.3f, FontWeight.Bold);
    }

    private void DrawFace(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector2 center, float rounding, int symbolIndex, float glow, float scale)
    {
        var color = ColorFor(symbolIndex);

        if (glow > 0.01f)
        {
            ProgressRing.Glow(center, (max.Y - min.Y) * 0.45f, color, glow * 1.1f);
        }

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(GamePalette.CellSunken));
        var tint = Vector4.Lerp(GamePalette.CellSunken, color, 0.18f + glow * 0.25f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(tint with { W = 0.5f + glow * 0.4f }));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(color with { W = 0.7f + glow * 0.3f }), (1.2f + glow * 2.4f) * scale);

        var symbolColor = glow > 0.01f ? Vector4.Lerp(color, new Vector4(1f, 1f, 1f, 1f), glow * 0.6f) : color;
        var symbolScale = 1.7f + glow * 0.12f;
        Typography.DrawCentered(center, Symbols[symbolIndex % Symbols.Length], symbolColor, symbolScale, FontWeight.SemiBold);
    }
}

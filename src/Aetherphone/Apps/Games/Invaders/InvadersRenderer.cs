using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Invaders;

internal sealed class InvadersRenderer
{
    public static readonly Vector4 BombColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 BulletColor = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 SaucerColor = new(0.98f, 0.45f, 0.62f, 1f);
    private static readonly Vector4 TopRowColor = new(0.98f, 0.95f, 0.90f, 1f);
    private const float HaloIntensity = 0.32f;
    private const float ShieldShadowAlpha = 0.35f;
    private const float ShieldHighlightAlpha = 0.22f;
    private const float ShieldScorch = 0.35f;
    private const float SaucerBob = 1.2f;
    private static readonly PixelSprite[][] InvaderSprites =
    {
        new[]
        {
            new PixelSprite("...##...", "..####..", ".######.", "##.##.##", "########", "..#..#.."),
            new PixelSprite("...##...", "..####..", ".######.", "##.##.##", "########", ".#.##.#."),
        },
        new[]
        {
            new PixelSprite("..#..#..", "#.####.#", "########", "##.##.##", ".######.", "#......#"),
            new PixelSprite("..#..#..", ".######.", "########", "##.##.##", ".######.", "..#..#.."),
        },
        new[]
        {
            new PixelSprite(".######.", "########", "##.##.##", "########", ".#.##.#.", "#.#..#.#"),
            new PixelSprite(".######.", "########", "##.##.##", "########", "..#..#..", ".##..##."),
        },
    };

    public static readonly PixelSprite Cannon = new("...#...", "..###..", ".#####.", "#######", "#######");
    private static readonly PixelSprite Saucer = new("...######...", ".##########.", "############", "..#..##..#..");

    public void Draw(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera, Rect full, Vector4 accent,
        float scale)
    {
        drawList.PushClipRect(full.Min, full.Max, true);
        DrawGround(drawList, in camera, full, accent);
        DrawShields(drawList, board, in camera, accent, scale);
        DrawInvaders(drawList, board, in camera, accent);
        DrawSaucer(drawList, board, in camera);
        DrawPlayer(drawList, board, in camera, accent);
        DrawBullet(drawList, board, in camera);
        DrawBombs(drawList, board, in camera);
        drawList.PopClipRect();
    }

    public static Vector4 KindColor(int kind, Vector4 accent)
    {
        switch (kind)
        {
            case 0:
                return TopRowColor;
            case 1:
                return GamePalette.Lighten(accent, 0.3f);
            default:
                return accent;
        }
    }

    private static void DrawGround(ImDrawListPtr drawList, in Camera2D camera, Rect full, Vector4 accent)
    {
        var groundY = camera.ToScreen(new Vector2(0f, InvadersBoard.PlayerY + InvadersBoard.PlayerHeight * 0.5f + 2f)).Y;
        drawList.AddLine(new Vector2(full.Min.X, groundY), new Vector2(full.Max.X, groundY),
            ImGui.GetColorU32(accent with { W = 0.5f }), MathF.Max(1f, camera.Px(0.6f)));
    }

    private static void DrawShields(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        var healthy = GamePalette.Lighten(accent, 0.12f) with { W = 0.85f };
        var scorched = GamePalette.Darken(healthy, ShieldScorch) with { W = 0.85f };
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ShieldShadowAlpha));
        var highlight = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, ShieldHighlightAlpha));
        var cell = camera.Px(InvadersBoard.ShieldCell);
        var inset = MathF.Max(0.5f, cell * 0.08f);
        var lift = MathF.Max(1f, cell * 0.12f);
        for (var shield = 0; shield < InvadersBoard.ShieldCount; shield++)
        {
            for (var column = 0; column < InvadersBoard.ShieldColumns; column++)
            {
                for (var row = 0; row < InvadersBoard.ShieldRows; row++)
                {
                    if (!board.ShieldCellAlive(shield, column, row))
                    {
                        continue;
                    }

                    var min = camera.ToScreen(InvadersBoard.ShieldCellPosition(shield, column, row)) +
                              new Vector2(inset, inset);
                    var max = min + new Vector2(cell - inset * 2f, cell - inset * 2f);
                    var fill = ImGui.GetColorU32(Exposed(board, shield, column, row) ? scorched : healthy);
                    drawList.AddRectFilled(min + new Vector2(0f, lift), max + new Vector2(0f, lift), shadow);
                    drawList.AddRectFilled(min, max, fill);
                    drawList.AddLine(min, new Vector2(max.X, min.Y), highlight, MathF.Max(1f, scale));
                }
            }
        }
    }

    private static bool Exposed(InvadersBoard board, int shield, int column, int row)
    {
        if (column > 0 && !board.ShieldCellAlive(shield, column - 1, row))
        {
            return true;
        }

        if (column < InvadersBoard.ShieldColumns - 1 && !board.ShieldCellAlive(shield, column + 1, row))
        {
            return true;
        }

        if (row > 0 && !board.ShieldCellAlive(shield, column, row - 1))
        {
            return true;
        }

        return row < InvadersBoard.ShieldRows - 1 && !board.ShieldCellAlive(shield, column, row + 1);
    }

    private static void DrawInvaders(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera, Vector4 accent)
    {
        var unit = camera.Px(InvadersBoard.InvaderWidth / 8f);
        var frame = board.AnimFrame ? 1 : 0;
        var halfSize = new Vector2(camera.Px(InvadersBoard.InvaderWidth * 0.5f), camera.Px(InvadersBoard.InvaderHeight * 0.5f));
        var haloRadius = camera.Px(InvadersBoard.InvaderWidth * 0.55f);
        for (var row = 0; row < InvadersBoard.Rows; row++)
        {
            var kind = InvadersBoard.RowKinds[row];
            var tint = KindColor(kind, accent);
            var color = ImGui.GetColorU32(tint);
            var sprite = InvaderSprites[kind][frame];
            for (var column = 0; column < InvadersBoard.Columns; column++)
            {
                if (!board.InvaderAlive(column, row))
                {
                    continue;
                }

                var topLeft = camera.ToScreen(board.InvaderPosition(column, row));
                ProgressRing.Glow(topLeft + halfSize, haloRadius, tint, HaloIntensity);
                sprite.Draw(drawList, topLeft, unit, color);
            }
        }
    }

    private static void DrawSaucer(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera)
    {
        if (!board.SaucerActive)
        {
            return;
        }

        var bob = (Pulse.Wave(Pulse.Medium) - 0.5f) * SaucerBob;
        var center = camera.ToScreen(new Vector2(board.SaucerX, InvadersBoard.SaucerY + bob));
        var unit = camera.Px(InvadersBoard.SaucerHalfWidth * 2f / Saucer.Width);
        ProgressRing.Glow(center, camera.Px(InvadersBoard.SaucerHalfWidth * 1.4f), SaucerColor, 0.7f);
        Saucer.DrawCentered(drawList, center, unit, ImGui.GetColorU32(SaucerColor));
    }

    private static void DrawPlayer(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera, Vector4 accent)
    {
        if (board.Respawning && Pulse.Wave(Pulse.Fast) > 0.5f)
        {
            return;
        }

        var center = camera.ToScreen(new Vector2(board.PlayerX, InvadersBoard.PlayerY - InvadersBoard.PlayerHeight * 0.5f));
        var unit = camera.Px(InvadersBoard.PlayerWidth / Cannon.Width);
        ProgressRing.Glow(center, camera.Px(InvadersBoard.PlayerWidth * 0.7f), accent, 0.35f);
        Cannon.DrawCentered(drawList, center, unit, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f)));
    }

    private static void DrawBullet(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera)
    {
        if (!board.HasBullet)
        {
            return;
        }

        var position = camera.ToScreen(board.Bullet);
        var halfWidth = MathF.Max(1f, camera.Px(0.5f));
        ProgressRing.Glow(position, camera.Px(2.5f), BulletColor, 0.5f);
        drawList.AddRectFilled(position - new Vector2(halfWidth, camera.Px(3f)), position + new Vector2(halfWidth, 0f),
            ImGui.GetColorU32(BulletColor));
    }

    private static void DrawBombs(ImDrawListPtr drawList, InvadersBoard board, in Camera2D camera)
    {
        var color = ImGui.GetColorU32(BombColor);
        var halfWidth = MathF.Max(1f, camera.Px(0.6f));
        var length = camera.Px(3f);
        for (var index = 0; index < board.BombCount; index++)
        {
            var position = camera.ToScreen(board.GetBomb(index));
            ProgressRing.Glow(position, camera.Px(2.5f), BombColor, 0.45f);
            drawList.AddRectFilled(position - new Vector2(halfWidth, 0f), position + new Vector2(halfWidth, length), color);
        }
    }
}

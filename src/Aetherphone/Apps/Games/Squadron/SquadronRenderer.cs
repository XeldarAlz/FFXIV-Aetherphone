using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Squadron;

internal sealed class SquadronRenderer
{
    public static readonly Vector4 RaptorColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 WardenColor = new(0.98f, 0.45f, 0.62f, 1f);
    public static readonly Vector4 BulletColor = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 ShotColor = new(1f, 0.75f, 0.35f, 1f);
    private const float BeamFillAlpha = 0.2f;
    private const float BeamEdgeAlpha = 0.45f;
    private const float RespawnBlinkSeconds = 0.12f;
    private const float HaloIntensity = 0.3f;
    private const float CaptiveSizeFactor = 0.8f;
    private static readonly PixelSprite[][] Sprites =
    {
        new[]
        {
            new PixelSprite("#......#", "##.##.##", ".######.", ".######.", "..####..", "...##..."),
            new PixelSprite("........", ".#.##.#.", "########", ".######.", "..####..", "...##..."),
        },
        new[]
        {
            new PixelSprite("##....##", "###..###", ".######.", "..####..", ".##..##.", "..#..#.."),
            new PixelSprite("........", "##....##", "########", ".######.", ".##..##.", "..#..#.."),
        },
        new[]
        {
            new PixelSprite("#.####.#", "########", "##.##.##", "########", ".##..##.", "#......#"),
            new PixelSprite("#.####.#", ".######.", "##.##.##", "########", "..#..#..", ".#....#."),
        },
    };

    public static readonly PixelSprite Fighter = new("...#...", "...#...", "..###..", ".#####.", "###.###");

    public static Vector4 KindColor(ShipKind kind, Vector4 accent)
    {
        switch (kind)
        {
            case ShipKind.Drone:
                return GamePalette.Lighten(accent, 0.35f);
            case ShipKind.Raptor:
                return RaptorColor;
            default:
                return WardenColor;
        }
    }

    public void Draw(ImDrawListPtr drawList, SquadronBoard board, in Camera2D camera, Rect full, Vector4 accent,
        float scale)
    {
        drawList.PushClipRect(full.Min, full.Max, true);
        var frame = board.AnimFrame ? 1 : 0;
        var unit = camera.Px(SquadronBoard.ShipWidth / 8f);
        var haloRadius = camera.Px(SquadronBoard.ShipWidth * 0.55f);
        for (var index = 0; index < board.ShipCount; index++)
        {
            var ship = board.GetShip(index);
            if (ship.State is ShipState.Waiting or ShipState.Gone)
            {
                continue;
            }

            var center = camera.ToScreen(ship.Position);
            var extent = board.BeamExtent(in ship);
            if (extent > 0f)
            {
                DrawBeam(drawList, in camera, ship.Position, extent);
            }

            var color = KindColor(ship.Kind, accent);
            ProgressRing.Glow(center, haloRadius, color, HaloIntensity);
            Sprites[(int)ship.Kind][frame].DrawCentered(drawList, center, unit, ImGui.GetColorU32(color));
            if (ship.HoldsCaptive)
            {
                DrawFighter(drawList, center - new Vector2(0f, camera.Px(SquadronBoard.ShipHeight)), in camera, accent,
                    0.8f, CaptiveSizeFactor);
            }
        }

        DrawPlayer(drawList, board, in camera, accent);
        DrawBullets(drawList, board, in camera);
        DrawShots(drawList, board, in camera, scale);
        if (board.RescueActive)
        {
            DrawFighter(drawList, camera.ToScreen(board.RescuePosition), in camera, accent, 0.9f, 1f);
        }

        if (board.CaptureActive)
        {
            DrawFighter(drawList, camera.ToScreen(board.CapturePosition), in camera, accent, 0.9f, 1f);
        }

        drawList.PopClipRect();
    }

    private static void DrawBeam(ImDrawListPtr drawList, in Camera2D camera, Vector2 shipPosition, float extent)
    {
        var topY = shipPosition.Y + SquadronBoard.ShipHeight * 0.5f;
        var top = camera.ToScreen(new Vector2(shipPosition.X, topY));
        var reach = camera.Px((SquadronBoard.PlayerRowY - topY) * extent);
        var bottomHalf = camera.Px(SquadronBoard.BeamTopHalfWidth +
                                   (SquadronBoard.BeamBottomHalfWidth - SquadronBoard.BeamTopHalfWidth) * extent);
        var topHalf = camera.Px(SquadronBoard.BeamTopHalfWidth);
        var bottomY = top.Y + reach;
        var fill = ImGui.GetColorU32(WardenColor with { W = BeamFillAlpha });
        var edge = ImGui.GetColorU32(WardenColor with { W = BeamEdgeAlpha });
        var topLeft = new Vector2(top.X - topHalf, top.Y);
        var topRight = new Vector2(top.X + topHalf, top.Y);
        var bottomLeft = new Vector2(top.X - bottomHalf, bottomY);
        var bottomRight = new Vector2(top.X + bottomHalf, bottomY);
        drawList.AddTriangleFilled(topLeft, topRight, bottomRight, fill);
        drawList.AddTriangleFilled(topLeft, bottomRight, bottomLeft, fill);
        drawList.AddLine(topLeft, bottomLeft, edge, 1.5f);
        drawList.AddLine(topRight, bottomRight, edge, 1.5f);
    }

    private static void DrawPlayer(ImDrawListPtr drawList, SquadronBoard board, in Camera2D camera, Vector4 accent)
    {
        if (board.CaptureActive)
        {
            return;
        }

        if (board.Respawning && (int)(board.RespawnRemaining / RespawnBlinkSeconds) % 2 == 0)
        {
            return;
        }

        var center = camera.ToScreen(board.PlayerCenter);
        if (!board.Dual)
        {
            DrawFighter(drawList, center, in camera, accent, 1f, 1f);
            return;
        }

        var offset = new Vector2(camera.Px(SquadronBoard.PlayerWidth * 0.5f), 0f);
        DrawFighter(drawList, center - offset, in camera, accent, 1f, 1f);
        DrawFighter(drawList, center + offset, in camera, accent, 1f, 1f);
    }

    public static void DrawFighter(ImDrawListPtr drawList, Vector2 center, in Camera2D camera, Vector4 accent, float alpha,
        float sizeFactor)
    {
        var unit = camera.Px(SquadronBoard.PlayerWidth / Fighter.Width) * sizeFactor;
        ProgressRing.Glow(center, camera.Px(SquadronBoard.PlayerWidth * 0.7f) * sizeFactor, accent, 0.35f * alpha);
        Fighter.DrawCentered(drawList, center, unit, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f) with { W = alpha }));
    }

    private static void DrawBullets(ImDrawListPtr drawList, SquadronBoard board, in Camera2D camera)
    {
        var color = ImGui.GetColorU32(BulletColor);
        var halfWidth = MathF.Max(1f, camera.Px(0.5f));
        var length = camera.Px(3f);
        for (var index = 0; index < board.BulletCount; index++)
        {
            var position = camera.ToScreen(board.GetBullet(index));
            ProgressRing.Glow(position, camera.Px(2.5f), BulletColor, 0.5f);
            drawList.AddRectFilled(position - new Vector2(halfWidth, length), position + new Vector2(halfWidth, 0f), color);
        }
    }

    private static void DrawShots(ImDrawListPtr drawList, SquadronBoard board, in Camera2D camera, float scale)
    {
        var color = ImGui.GetColorU32(ShotColor);
        for (var index = 0; index < board.ShotCount; index++)
        {
            var shot = board.GetShot(index);
            var head = camera.ToScreen(shot.Position);
            var tail = camera.ToScreen(shot.Position - Vector2.Normalize(shot.Velocity) * 2.4f);
            ProgressRing.Glow(head, camera.Px(2.2f), ShotColor, 0.45f);
            drawList.AddLine(tail, head, color, MathF.Max(1.5f, 2f * scale));
        }
    }
}

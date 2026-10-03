using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

internal static class OnlineGameArt
{
    private const int UnoMaxPlayers = 6;
    private const int DuelMaxPlayers = 2;
    private const float UnoFanAngle = 0.30f;

    public static readonly string[] Kinds =
    {
        GameRoomWire.UnoKind, GameRoomWire.ChessKind, GameRoomWire.PoolKind,
    };

    private static readonly Vector4 BallInk = new(0.09f, 0.09f, 0.11f, 1f);
    private static readonly Vector4 White = new(0.97f, 0.97f, 0.99f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.30f);

    public static string AccentId(string kind)
    {
        if (string.Equals(kind, GameRoomWire.ChessKind, StringComparison.Ordinal))
        {
            return "chess";
        }

        return string.Equals(kind, GameRoomWire.PoolKind, StringComparison.Ordinal) ? "pool" : "uno";
    }

    public static Vector4 Accent(string kind) => AppAccents.For(AccentId(kind));

    public static int MaxPlayers(string kind) =>
        string.Equals(kind, GameRoomWire.UnoKind, StringComparison.Ordinal) ? UnoMaxPlayers : DuelMaxPlayers;

    public static void Draw(ImDrawListPtr drawList, string kind, Vector2 center, float size, float scale)
    {
        if (string.Equals(kind, GameRoomWire.ChessKind, StringComparison.Ordinal))
        {
            if (!AppIconTile.TryDrawGlyph(drawList, "chess", center, size * AppIconTextures.GlyphFraction, White))
            {
                AppIconArt.TryDraw(drawList, "chess", center, size, White, Palette.Darken(Accent(kind), 0.16f));
            }

            return;
        }

        if (string.Equals(kind, GameRoomWire.PoolKind, StringComparison.Ordinal))
        {
            DrawEightBall(drawList, center, size);
            return;
        }

        DrawUnoFan(drawList, center, size, scale);
    }

    private static void DrawUnoFan(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        var cardWidth = size * 0.44f;
        for (var index = 0; index < 3; index++)
        {
            var spread = index - 1;
            var offset = new Vector2(spread * cardWidth * 0.55f, MathF.Abs(spread) * cardWidth * 0.14f);
            var rect = UnoCardArt.RectAround(center + offset, cardWidth);
            UnoCardArt.DrawBack(drawList, rect, scale, 1f, spread * UnoFanAngle);
        }
    }

    private static void DrawEightBall(ImDrawListPtr drawList, Vector2 center, float size)
    {
        var radius = size * 0.42f;
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.14f), radius, ImGui.GetColorU32(Shadow), 48);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BallInk), 48);
        drawList.AddCircleFilled(center, radius * 0.50f, ImGui.GetColorU32(White), 32);
        Typography.DrawCentered(drawList, center, "8", BallInk, MathF.Max(0.55f, size / 46f), FontWeight.Bold);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.42f, -radius * 0.46f), radius * 0.16f,
            ImGui.GetColorU32(White with { W = 0.45f }), 16);
    }
}

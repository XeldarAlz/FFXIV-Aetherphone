using Aetherphone.Apps.Games.Broadside;
using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Apps.Games.LuckyDraw;
using Aetherphone.Apps.Games.MiniGolf;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Online;

internal static class OnlineGameArt
{
    private const int UnoMaxPlayers = 6;
    private const int LuckyDrawMaxPlayers = 6;
    private const int MiniGolfMaxPlayers = 4;
    private const int DuelMaxPlayers = 2;
    private const float UnoFanAngle = 0.30f;
    private const float LuckyFanAngle = 0.26f;

    public static readonly string[] Kinds =
    {
        GameRoomWire.UnoKind, GameRoomWire.ChessKind, GameRoomWire.PoolKind, GameRoomWire.ConnectFourKind,
        GameRoomWire.BroadsideKind,
        GameRoomWire.LuckyDrawKind,
        GameRoomWire.CraterKind,
        GameRoomWire.MiniGolfKind,
    };

    private static readonly Vector4 BallInk = new(0.09f, 0.09f, 0.11f, 1f);
    private static readonly Vector4 White = new(0.97f, 0.97f, 0.99f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.30f);

    private static readonly CardDesign[] LuckyFan =
    {
        CardDesign.Action(FontAwesomeIcon.Snowflake, LuckyDrawRenderer.FreezeTint), CardDesign.Numbered(7),
        CardDesign.Action(FontAwesomeIcon.ShieldAlt, LuckyDrawRenderer.ChanceTint),
    };

    public static string AccentId(string kind)
    {
        if (string.Equals(kind, GameRoomWire.LuckyDrawKind, StringComparison.Ordinal))
        {
            return OnlineLuckyDrawTable.AccentId;
        }

        if (string.Equals(kind, GameRoomWire.ChessKind, StringComparison.Ordinal))
        {
            return "chess";
        }

        if (string.Equals(kind, GameRoomWire.PoolKind, StringComparison.Ordinal))
        {
            return "pool";
        }

        if (string.Equals(kind, GameRoomWire.BroadsideKind, StringComparison.Ordinal))
        {
            return "broadside";
        }

        if (string.Equals(kind, GameRoomWire.CraterKind, StringComparison.Ordinal))
        {
            return "crater";
        }

        if (string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal))
        {
            return OnlineMiniGolfTable.AccentId;
        }

        return string.Equals(kind, GameRoomWire.ConnectFourKind, StringComparison.Ordinal) ? "connectfour" : "uno";
    }

    public static Vector4 Accent(string kind) => AppAccents.For(AccentId(kind));

    public static int MaxPlayers(string kind)
    {
        if (string.Equals(kind, GameRoomWire.LuckyDrawKind, StringComparison.Ordinal))
        {
            return LuckyDrawMaxPlayers;
        }

        if (string.Equals(kind, GameRoomWire.CraterKind, StringComparison.Ordinal))
        {
            return GameRoomWire.CraterMaxPlayers;
        }

        if (string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal))
        {
            return MiniGolfMaxPlayers;
        }

        return string.Equals(kind, GameRoomWire.UnoKind, StringComparison.Ordinal) ? UnoMaxPlayers : DuelMaxPlayers;
    }

    public static void Draw(ImDrawListPtr drawList, string kind, Vector2 center, float size, float scale)
    {
        if (string.Equals(kind, GameRoomWire.LuckyDrawKind, StringComparison.Ordinal))
        {
            DrawLuckyFan(drawList, center, size, scale);
            return;
        }

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

        if (string.Equals(kind, GameRoomWire.ConnectFourKind, StringComparison.Ordinal))
        {
            DrawConnectFourMedallion(drawList, center, size);
            return;
        }

        if (string.Equals(kind, GameRoomWire.BroadsideKind, StringComparison.Ordinal))
        {
            DrawBroadsideMedallion(drawList, center, size, scale);
            return;
        }

        if (string.Equals(kind, GameRoomWire.CraterKind, StringComparison.Ordinal))
        {
            DrawCraterMedallion(drawList, center, size);
            return;
        }

        if (string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal))
        {
            DrawGreenMedallion(drawList, center, size);
            return;
        }

        DrawUnoFan(drawList, center, size, scale);
    }

    private static void DrawGreenMedallion(ImDrawListPtr drawList, Vector2 center, float size)
    {
        var half = size * 0.46f;
        drawList.AddCircleFilled(center + new Vector2(0f, half * 0.14f), half, ImGui.GetColorU32(Shadow), 48);
        Squircle.FillVerticalGradient(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
            size * 0.16f, ImGui.GetColorU32(MiniGolfRenderer.Fairway), ImGui.GetColorU32(MiniGolfRenderer.Grass));
        var cup = center + new Vector2(size * 0.14f, -size * 0.04f);
        var cupRadius = size * 0.1f;
        drawList.AddCircleFilled(cup, cupRadius * 1.2f, ImGui.GetColorU32(White with { W = 0.5f }), 20);
        drawList.AddCircleFilled(cup, cupRadius, ImGui.GetColorU32(BallInk), 20);
        var top = cup - new Vector2(0f, size * 0.32f);
        drawList.AddLine(cup, top, ImGui.GetColorU32(White), MathF.Max(1.5f, size * 0.03f));
        drawList.AddTriangleFilled(top, top + new Vector2(size * 0.2f, size * 0.07f), top + new Vector2(0f, size * 0.14f),
            ImGui.GetColorU32(Accent(GameRoomWire.MiniGolfKind)));
        MiniGolfRenderer.DrawBall(drawList, center + new Vector2(-size * 0.18f, size * 0.2f), size * 0.1f,
            Accent(GameRoomWire.MiniGolfKind) with { W = 0f }, 1f);
    }

    private static void DrawBroadsideMedallion(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        var half = size * 0.46f;
        var clock = (float)ImGui.GetTime();
        drawList.AddCircleFilled(center + new Vector2(0f, half * 0.14f), half, ImGui.GetColorU32(Shadow), 48);
        Squircle.FillVerticalGradient(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
            size * 0.16f, ImGui.GetColorU32(BroadsideArt.SkyTop), ImGui.GetColorU32(BroadsideArt.SkyBottom));
        var pitch = size * 0.2f;
        BroadsideArt.DrawPuff(drawList, center + new Vector2(-size * 0.2f, size * 0.24f), pitch, clock, 3, -1f);
        var burst = center + new Vector2(size * 0.22f, size * 0.22f);
        drawList.AddCircleFilled(burst, pitch * 0.34f, ImGui.GetColorU32(BroadsideArt.Ember), 16);
        drawList.AddCircleFilled(burst, pitch * 0.18f, ImGui.GetColorU32(BroadsideArt.Flame), 12);
        var hull = new Rect(center + new Vector2(-size * 0.36f, -size * 0.22f), center + new Vector2(size * 0.36f, 0f));
        BroadsideArt.DrawAirship(drawList, hull, true, BroadsideArt.Hull, scale, clock, -1f, 1f);
    }

    private static void DrawCraterMedallion(ImDrawListPtr drawList, Vector2 center, float size)
    {
        var radius = size * 0.3f;
        var body = center + new Vector2(-size * 0.04f, size * 0.06f);
        drawList.AddCircleFilled(body + new Vector2(0f, radius * 1.05f), radius * 1.1f, ImGui.GetColorU32(Shadow), 32);
        CraterArt.Launcher(drawList, body, radius, CraterRules.AimDirection(CraterRules.DefaultElevation, 1),
            GameSeats.Color(0));
        CraterArt.Moogle(drawList, body, radius, 1, GameSeats.Color(0), 0.2f, 0f, 0.3f);
    }

    private static void DrawConnectFourMedallion(ImDrawListPtr drawList, Vector2 center, float size)
    {
        var half = size * 0.46f;
        drawList.AddCircleFilled(center + new Vector2(0f, half * 0.14f), half, ImGui.GetColorU32(Shadow), 48);
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.16f,
            ImGui.GetColorU32(OnlineConnectFourTable.GridFrame));
        var discRadius = size * 0.15f;
        var offset = size * 0.19f;
        DrawSlot(drawList, center + new Vector2(-offset, -offset), discRadius, OnlineConnectFourTable.SeatColor(1));
        DrawSlot(drawList, center + new Vector2(offset, -offset), discRadius, OnlineConnectFourTable.SeatColor(0));
        DrawSlot(drawList, center + new Vector2(-offset, offset), discRadius, OnlineConnectFourTable.SeatColor(0));
        DrawSlot(drawList, center + new Vector2(offset, offset), discRadius, OnlineConnectFourTable.SeatColor(1));
    }

    private static void DrawSlot(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 20);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), 20);
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

    private static void DrawLuckyFan(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        var cardWidth = size * 0.4f;
        var accent = OnlineLuckyDrawTable.Accent;
        for (var index = 0; index < LuckyFan.Length; index++)
        {
            var spread = index - 1;
            var offset = new Vector2(spread * cardWidth * 0.62f, MathF.Abs(spread) * cardWidth * 0.12f);
            CardFace.Draw(drawList, new CardPose(center + offset, cardWidth, spread * LuckyFanAngle), LuckyFan[index],
                accent, scale);
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

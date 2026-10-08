using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal static class CasinoArt
{
    public const float GlyphExtentFactor = 0.26f;
    public const float LiveDotRadius = 3.5f;
    public const float SeatDotRadius = 3.2f;
    public const float SeatDotGap = 4f;
    public const float ChevronScale = 0.75f;

    private const float LiveHaloAlpha = 0.35f;
    private const float LiveHaloGrowth = 1.9f;
    private const float SheetLighten = 0.10f;
    private const float SheetStrokeAlpha = 0.08f;
    private const float SheetGrabberAlpha = 0.35f;

    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector4 TintOf(string gameId) => gameId switch
    {
        CasinoGames.Blackjack => AccentRing.Green,
        CasinoGames.Slots => AccentRing.Rose,
        CasinoGames.Scratch => AccentRing.Gold,
        CasinoGames.Barkeep => AccentRing.Orange,
        CasinoGames.Bingo => AccentRing.Azure,
        CasinoGames.Wheel => AccentRing.Violet,
        CasinoGames.DailySpin => AccentRing.Teal,
        CasinoGames.Mines => AccentRing.Emerald,
        CasinoGames.Dice => AccentRing.Indigo,
        CasinoGames.Limbo => AccentRing.Cyan,
        CasinoGames.Keno => AccentRing.Lime,
        CasinoGames.HiLo => AccentRing.Orchid,
        CasinoGames.Plinko => AccentRing.Cyan,
        _ => AccentRing.Emerald,
    };

    public static void GameTile(ImDrawListPtr drawList, string gameId, Vector2 center, float size, float alpha = 1f)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var surface = IconTile.Surface(TintOf(gameId));
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor, surface, alpha);
        CasinoGlyphs.Draw(drawList, gameId, center, size * GlyphExtentFactor,
            ImGui.GetColorU32(White with { W = alpha }), ImGui.GetColorU32(surface with { W = alpha }));
    }

    public static void IconTileAt(ImDrawListPtr drawList, Vector2 center, float size, Vector4 tint,
        FontAwesomeIcon icon)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink, size * 0.46f);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, Vector4 ink) =>
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.ChevronRight), ink, ChevronScale);

    public static void LiveDot(ImDrawListPtr drawList, Vector2 center, float scale, Vector4 ink, bool live)
    {
        var radius = LiveDotRadius * scale;
        if (live)
        {
            var wave = Pulse.Wave(Pulse.Breath);
            drawList.AddCircleFilled(center, radius * (1f + (LiveHaloGrowth - 1f) * wave),
                ImGui.GetColorU32(Palette.WithAlpha(ink, LiveHaloAlpha * (1f - wave))), 20);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink), 16);
    }

    public static float SeatDots(ImDrawListPtr drawList, Vector2 leftCenter, int seated, int seats, Vector4 filled,
        Vector4 empty, float scale)
    {
        var radius = SeatDotRadius * scale;
        var step = radius * 2f + SeatDotGap * scale;
        for (var seatIndex = 0; seatIndex < seats; seatIndex++)
        {
            var center = new Vector2(leftCenter.X + radius + seatIndex * step, leftCenter.Y);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(seatIndex < seated ? filled : empty), 16);
        }

        return seats <= 0 ? 0f : seats * step - SeatDotGap * scale;
    }

    public static SheetSkin Sheet(AppSkin ui) => new(
        Palette.Lighten(ui.Palette.BackdropTop, SheetLighten) with { W = 1f },
        Palette.WithAlpha(ui.TitleInk, SheetStrokeAlpha),
        Palette.WithAlpha(ui.MutedInk, SheetGrabberAlpha),
        ui.TitleInk);

    public static bool PressCard(uint id, Vector2 min, Vector2 max, out Vector2 pressedMin, out Vector2 pressedMax,
        bool interactive = true)
    {
        var hovered = interactive && UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(id, pressed, Motion.PressScaleCard);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * factor;
        pressedMin = center - half;
        pressedMax = center + half;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered;
    }
}

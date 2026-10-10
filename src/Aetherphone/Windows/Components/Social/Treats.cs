using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum TreatSpot : byte
{
    HomeTop,
    HomeLow,
    ChirperFeed,
    ChirperDeep,
    AethergramFeed,
    AethergramDeep,
    VelvetFeed,
    VelvetDeep,
    Skywatcher,
    EmptyState,
    Calendar,
    Clock,
    ChatWallpaper,
}

internal static class Treats
{
    public const int Total = 13;

    private const double CycleSeconds = 45.0;
    private const float VisibleWindow = 0.34f;
    private const float FadeWindow = 0.04f;
    private const float CandySize = 9f;
    private const float PickReach = 2.2f;
    private const float BobHeight = 3f;
    private const float HoverGrow = 1.15f;
    private const float GlowReach = 3.2f;
    private const float GlowAlpha = 0.35f;
    private const int GlowCells = 8;
    private const float AreaMargin = 0.12f;

    private static readonly Vector4[] Wrappers =
    {
        new(1f, 0.55f, 0.16f, 1f),
        new(0.62f, 0.36f, 0.92f, 1f),
        new(0.38f, 0.86f, 0.46f, 1f),
        new(1f, 0.44f, 0.62f, 1f),
    };

    private static readonly Vector4 Shine = new(1f, 1f, 1f, 0.55f);

    public static int Found
    {
        get
        {
            var configuration = Plugin.Cfg;
            return configuration.HalloweenTreatYear == DateTime.Today.Year
                ? BitOperations.PopCount((uint)configuration.HalloweenTreats)
                : 0;
        }
    }

    public static bool Rewarded => SeasonalTheme.Halloween && Found >= Total;

    public static void Offer(ImDrawListPtr drawList, TreatSpot spot, Rect area)
    {
        if (!Available(spot))
        {
            return;
        }

        var salt = (int)spot;
        var cycle = ImGui.GetTime() / CycleSeconds + Hash(salt, 0.37f);
        var phase = (float)(cycle - Math.Floor(cycle));
        if (phase > VisibleWindow)
        {
            return;
        }

        var round = (int)Math.Floor(cycle);
        var across = AreaMargin + Hash(round * 31 + salt, 1.7f) * (1f - AreaMargin * 2f);
        var down = AreaMargin + Hash(round * 17 + salt, 4.3f) * (1f - AreaMargin * 2f);
        var center = new Vector2(area.Min.X + area.Width * across, area.Min.Y + area.Height * down);
        var fade = MathF.Min(1f, MathF.Min(phase, VisibleWindow - phase) / FadeWindow);
        Present(drawList, spot, center, CandySize * UiScale.Current, fade);
    }

    public static void OfferAt(ImDrawListPtr drawList, TreatSpot spot, Vector2 center, float size)
    {
        if (Available(spot))
        {
            Present(drawList, spot, center, size, 1f);
        }
    }

    private static bool Available(TreatSpot spot)
    {
        if (!SeasonalTheme.Halloween)
        {
            return false;
        }

        var configuration = Plugin.Cfg;
        return configuration.HalloweenTreatYear != DateTime.Today.Year
            || (configuration.HalloweenTreats & (1 << (int)spot)) == 0;
    }

    private static void Present(ImDrawListPtr drawList, TreatSpot spot, Vector2 center, float size, float alpha)
    {
        var scale = UiScale.Current;
        var time = (float)ImGui.GetTime();
        center.Y += MathF.Sin(time * 2.2f + (int)spot) * BobHeight * scale;
        var reach = new Vector2(size * PickReach, size * PickReach);
        var hovered = UiInteract.Hover(center - reach, center + reach);
        var drawn = hovered ? size * HoverGrow : size;
        var wrapper = Wrappers[(int)spot % Wrappers.Length];
        NightScene.Glow(drawList, center, drawn * GlowReach, wrapper with { W = GlowAlpha * alpha }, GlowCells);
        DrawCandy(drawList, center, drawn, wrapper with { W = alpha }, time + (int)spot);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (alpha < 0.5f || !UiInteract.Click(center - reach, center + reach, hovered))
        {
            return;
        }

        Collect(spot);
        UiInteract.BlockThisFrame();
    }

    private static void DrawCandy(ImDrawListPtr drawList, Vector2 center, float size, Vector4 wrapper, float time)
    {
        var tilt = MathF.Sin(time * 1.3f) * 0.25f;
        var axis = new Vector2(MathF.Cos(tilt), MathF.Sin(tilt));
        var normal = new Vector2(-axis.Y, axis.X);
        var ink = ImGui.GetColorU32(wrapper);
        for (var side = -1; side <= 1; side += 2)
        {
            var root = center + axis * (side * size * 0.75f);
            var tip = center + axis * (side * size * 1.7f);
            drawList.AddTriangleFilled(root, tip + normal * size * 0.6f, tip - normal * size * 0.6f, ink);
        }

        drawList.AddCircleFilled(center, size, ink, 20);
        drawList.AddCircleFilled(center - normal * size * 0.35f - axis * size * 0.3f, size * 0.28f,
            ImGui.GetColorU32(Shine with { W = Shine.W * wrapper.W }), 12);
    }

    private static void Collect(TreatSpot spot)
    {
        var configuration = Plugin.Cfg;
        var year = DateTime.Today.Year;
        if (configuration.HalloweenTreatYear != year)
        {
            configuration.HalloweenTreatYear = year;
            configuration.HalloweenTreats = 0;
        }

        configuration.HalloweenTreats |= 1 << (int)spot;
        configuration.Save();
        var found = Found;
        UiFeedback.Play(found >= Total ? UiSound.HalloweenFlare : UiSound.HalloweenSparkle);
        ShellToast.Show(found >= Total
            ? Loc.T(L.Seasonal.TreatsComplete)
            : Loc.T(L.Seasonal.TreatFound, found, Total));
    }

    private static float Hash(int index, float salt)
    {
        var value = MathF.Sin(index * 127.1f + salt) * 43758.547f;
        return value - MathF.Floor(value);
    }
}

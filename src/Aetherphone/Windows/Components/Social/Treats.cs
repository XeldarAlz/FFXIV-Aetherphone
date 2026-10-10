using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Home;
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
    public const int NoSpot = -1;

    private const int SpotCount = 13;
    private const double MinimumWaitMilliseconds = 30 * 60 * 1000;
    private const double MaximumWaitMilliseconds = 60 * 60 * 1000;
    private const double CatchUpFactor = 0.3;
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

    private static AppInstaller? installer;
    private static AethernetSession? session;
    private static int activeSpot = NoSpot;
    private static int lastSpot = NoSpot;

    public static bool VelvetBarred { get; set; }

    public static int Found => Plugin.Cfg.HalloweenTreatYear == DateTime.Today.Year
        ? BitOperations.PopCount((uint)Plugin.Cfg.HalloweenTreats)
        : 0;

    public static int Unlocked => SeasonalTheme.HalloweenByDate
        ? Math.Clamp(SeasonalTheme.DayOfHalloween(DateTime.Today) + 1, 0, Total)
        : Total;

    public static bool Rewarded => SeasonalTheme.Halloween && Found >= Total;

    public static void Configure(AppInstaller appInstaller, AethernetSession aethernetSession)
    {
        installer = appInstaller;
        session = aethernetSession;
    }

    public static void Reset()
    {
        installer = null;
        session = null;
        activeSpot = NoSpot;
        lastSpot = NoSpot;
        VelvetBarred = false;
    }

    public static void SummonNow()
    {
        activeSpot = NoSpot;
        SetDue(Now);
    }

    public static void SetAllFound(bool found)
    {
        var configuration = Plugin.Cfg;
        configuration.HalloweenTreatYear = DateTime.Today.Year;
        configuration.HalloweenTreats = found ? (1 << Total) - 1 : 0;
        configuration.HalloweenTreatDue = 0;
        configuration.Save();
        activeSpot = NoSpot;
    }

    public static void Offer(ImDrawListPtr drawList, TreatSpot spot, Rect area)
    {
        if (ActiveSpot() != (int)spot)
        {
            return;
        }

        var salt = (int)spot * 31 + SeasonalTheme.DayOfHalloween(DateTime.Today) + Found * 7;
        var across = AreaMargin + Spooks.Hash(salt, 1.7f) * (1f - AreaMargin * 2f);
        var down = AreaMargin + Spooks.Hash(salt, 4.3f) * (1f - AreaMargin * 2f);
        var center = new Vector2(area.Min.X + area.Width * across, area.Min.Y + area.Height * down);
        Present(drawList, spot, center, CandySize * UiScale.Current);
    }

    public static void OfferAt(ImDrawListPtr drawList, TreatSpot spot, Vector2 center, float size)
    {
        if (ActiveSpot() == (int)spot)
        {
            Present(drawList, spot, center, size);
        }
    }

    public static void DrawStill(ImDrawListPtr drawList, Vector2 center, float size, int index) =>
        DrawCandy(drawList, center, size, Wrappers[index % Wrappers.Length], 0f);

    public static int ActiveSpot()
    {
        if (!SeasonalTheme.Halloween || Found >= Unlocked)
        {
            return NoSpot;
        }

        if (activeSpot != NoSpot && Reachable((TreatSpot)activeSpot))
        {
            return activeSpot;
        }

        var due = Plugin.Cfg.HalloweenTreatDue;
        if (due <= 0)
        {
            SetDue(Now + Wait());
            return NoSpot;
        }

        if (Now < due)
        {
            return NoSpot;
        }

        activeSpot = PickSpot();
        return activeSpot;
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static bool Reachable(TreatSpot spot) => spot switch
    {
        TreatSpot.HomeTop or TreatSpot.HomeLow or TreatSpot.EmptyState => true,
        TreatSpot.ChirperFeed or TreatSpot.ChirperDeep => Installed("chirper") && SignedIn,
        TreatSpot.AethergramFeed or TreatSpot.AethergramDeep => Installed("aethergram") && SignedIn,
        TreatSpot.VelvetFeed or TreatSpot.VelvetDeep => VelvetOpen,
        TreatSpot.Skywatcher => Installed("skywatcher"),
        TreatSpot.Calendar => Installed("calendar"),
        TreatSpot.Clock => Installed("clock"),
        TreatSpot.ChatWallpaper => Installed("message") || Installed("messages"),
        _ => false,
    };

    private static bool Installed(string appId) => installer is not null && installer.IsInstalled(appId);

    private static bool SignedIn => session is not null && session.IsSignedIn;

    private static bool VelvetOpen => !VelvetBarred && Installed("velvet") && SignedIn &&
        Plugin.Cfg.VelvetAcknowledgedGate && Plugin.Cfg.IsVelvetOnboarded();

    private static int PickSpot()
    {
        Span<int> candidates = stackalloc int[SpotCount];
        var reachable = 0;
        for (var spot = 0; spot < SpotCount; spot++)
        {
            if (spot != lastSpot && Reachable((TreatSpot)spot))
            {
                candidates[reachable++] = spot;
            }
        }

        return reachable == 0 ? NoSpot : candidates[Random.Shared.Next(reachable)];
    }

    private static long Wait()
    {
        var wait = MinimumWaitMilliseconds +
            Random.Shared.NextDouble() * (MaximumWaitMilliseconds - MinimumWaitMilliseconds);
        return (long)(Unlocked - Found > 1 ? wait * CatchUpFactor : wait);
    }

    private static void SetDue(long due)
    {
        var configuration = Plugin.Cfg;
        configuration.HalloweenTreatDue = due;
        configuration.Save();
    }

    private static void Present(ImDrawListPtr drawList, TreatSpot spot, Vector2 center, float size)
    {
        var reach = new Vector2(size * PickReach, size * PickReach);
        var hitMin = center - reach;
        var hitMax = center + reach;
        var hovered = UiInteract.HoverOverlay(new Rect(hitMin, hitMax));
        var time = (float)ImGui.GetTime();
        var drawn = hovered ? size * HoverGrow : size;
        var bobbed = center + new Vector2(0f, MathF.Sin(time * 2.2f + (int)spot) * BobHeight * UiScale.Current);
        var wrapper = Wrappers[(int)spot % Wrappers.Length];
        NightScene.Glow(drawList, bobbed, drawn * GlowReach, wrapper with { W = GlowAlpha }, GlowCells);
        DrawCandy(drawList, bobbed, drawn, wrapper, time + (int)spot);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered, false))
        {
            Collect(spot);
            UiInteract.BlockThisFrame();
        }
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
            ImGui.GetColorU32(Shine), 12);
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

        var treats = configuration.HalloweenTreats;
        configuration.HalloweenTreats = treats | (~treats & (treats + 1));
        activeSpot = NoSpot;
        lastSpot = (int)spot;
        configuration.HalloweenTreatDue = Now + Wait();
        configuration.Save();
        var found = Found;
        UiFeedback.Play(found >= Total ? UiSound.HalloweenFlare : UiSound.HalloweenSparkle);
        ShellToast.Show(found >= Total
            ? Loc.T(L.Seasonal.TreatsComplete, Total)
            : Loc.T(L.Seasonal.TreatFound, found, Total));
    }
}

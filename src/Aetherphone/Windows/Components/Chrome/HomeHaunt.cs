using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class HomeHaunt
{
    private const int PuffCount = 5;
    private const int PuffCells = 8;
    private const float PuffRadius = 110f;
    private const float PuffLift = 64f;
    private const float VeilReach = 260f;
    private const double FlightPeriodSeconds = 19.0;
    private const float FlightWindow = 0.32f;
    private const float BatSize = 1.05f;

    private static readonly Vector4 MistInk = new(0.82f, 0.78f, 0.96f, 0.2f);
    private static readonly Vector4 VeilInk = new(0.08f, 0.04f, 0.14f, 0.32f);
    private static readonly Vector4 FlockInk = Spooks.BatShadow with { W = 0.8f };

    public static void Draw(Rect screen)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var seconds = ImGui.GetTime();
        var reach = VeilReach * scale;
        drawList.AddRectFilledMultiColor(new Vector2(screen.Min.X, screen.Max.Y - reach), screen.Max,
            ImGui.GetColorU32(VeilInk with { W = 0f }), ImGui.GetColorU32(VeilInk with { W = 0f }),
            ImGui.GetColorU32(VeilInk), ImGui.GetColorU32(VeilInk));
        DrawMist(drawList, screen, (float)seconds, scale);
        Spooks.DrawFlight(drawList, screen, seconds, FlightPeriodSeconds, FlightWindow, 0.10f, 0.16f,
            BatSize * scale, FlockInk);
    }

    public static void DrawTreats(Rect screen)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        Treats.Offer(drawList, TreatSpot.HomeTop, Band(screen, 0.12f, 0.24f));
        Treats.Offer(drawList, TreatSpot.HomeLow, Band(screen, 0.82f, 0.85f));
    }

    private static Rect Band(Rect screen, float top, float bottom) =>
        new(new Vector2(screen.Min.X, screen.Min.Y + screen.Height * top),
            new Vector2(screen.Max.X, screen.Min.Y + screen.Height * bottom));

    private static void DrawMist(ImDrawListPtr drawList, Rect screen, float seconds, float scale)
    {
        var radius = PuffRadius * scale;
        var span = screen.Width + radius * 4f;
        var baseY = screen.Max.Y - PuffLift * scale;
        for (var puffIndex = 0; puffIndex < PuffCount; puffIndex++)
        {
            var speed = (5f + puffIndex * 1.7f) * scale;
            var offset = puffIndex * span / PuffCount;
            var travel = (seconds * speed + offset) % span;
            var lift = MathF.Sin(seconds * 0.3f + puffIndex * 1.9f) * 14f * scale - puffIndex % 2 * 30f * scale;
            var center = new Vector2(screen.Min.X - radius * 2f + travel, baseY + lift);
            NightScene.Glow(drawList, center, radius, MistInk, PuffCells);
        }
    }
}

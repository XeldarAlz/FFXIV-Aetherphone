using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal static class RaffleWheelArt
{
    private const float FullTurn = MathF.PI * 2f;
    private const float PointerAngle = -MathF.PI * 0.5f;
    private const float StepRadians = 0.07f;
    private const float NameRadiusShare = 0.66f;
    private const float MinNameArc = 44f;

    private static readonly Vector4[] Fills =
    {
        new(0.42f, 0.10f, 0.32f, 1f),
        new(0.08f, 0.30f, 0.40f, 1f),
        new(0.30f, 0.16f, 0.46f, 1f),
        new(0.12f, 0.34f, 0.24f, 1f),
    };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, RafflePlayback playback,
        CasinoRaffleEntrantDto[] entrants, string me, float phase, float scale)
    {
        var rim = new Rect(center - new Vector2(radius + 10f * scale), center + new Vector2(radius + 10f * scale));
        CasinoLights.BulbChase(drawList, rim, radius + 10f * scale, scale, phase, CasinoLights.BulbPitch,
            CasinoColors.Money, CasinoColors.LightA, playback.Stage == RaffleStage.Spinning ? 1f : 0.55f);
        var count = playback.SegmentCount;
        if (count == 0)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Fills[0] with { W = 0.6f }), 64);
            return;
        }

        var landed = playback.Stage is RaffleStage.Landed or RaffleStage.Done;
        var lastWinner = playback.WinnersShown > 0 ? playback.WinnerEntrant(playback.WinnersShown - 1) : -1;
        for (var segment = 0; segment < count; segment++)
        {
            var owner = playback.OwnerOf(segment);
            var start = PointerAngle + playback.StartOf(segment) * FullTurn + playback.Angle;
            var end = PointerAngle + playback.EndOf(segment) * FullTurn + playback.Angle;
            var mine = owner < entrants.Length && string.Equals(entrants[owner].UserId, me, StringComparison.Ordinal);
            var fill = Fills[segment % Fills.Length];
            if (mine)
            {
                fill = Palette.Mix(fill, CasinoColors.Money, 0.45f);
            }

            if (landed && owner == lastWinner)
            {
                fill = Palette.Mix(CasinoColors.Money, CasinoColors.MoneyHighlight, 0.5f + 0.5f * MathF.Sin(phase * 6f));
            }

            Wedge(drawList, center, radius, start, end, ImGui.GetColorU32(fill));
            var edge = new Vector2(MathF.Cos(start), MathF.Sin(start)) * radius;
            drawList.AddLine(center, center + edge, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f)), 1.2f * scale);
            DrawName(drawList, center, radius, start, end, owner < entrants.Length ? entrants[owner].DisplayName : string.Empty,
                scale);
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = 0.8f }), 96, 2f * scale);
        drawList.AddCircleFilled(center, radius * 0.16f, ImGui.GetColorU32(CasinoColors.FeltBottom), 32);
        drawList.AddCircle(center, radius * 0.16f, ImGui.GetColorU32(CasinoColors.Money), 32, 2f * scale);
        DrawPointer(drawList, center, radius, scale);
    }

    private static void Wedge(ImDrawListPtr drawList, Vector2 center, float radius, float start, float end,
        uint color)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling((end - start) / StepRadians));
        var previous = center + new Vector2(MathF.Cos(start), MathF.Sin(start)) * radius;
        for (var step = 1; step <= steps; step++)
        {
            var angle = start + (end - start) * step / steps;
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            drawList.AddTriangleFilled(center, previous, next, color);
            previous = next;
        }
    }

    private static void DrawName(ImDrawListPtr drawList, Vector2 center, float radius, float start, float end,
        string name, float scale)
    {
        var arc = (end - start) * radius * NameRadiusShare;
        if (name.Length == 0 || arc < MinNameArc * scale)
        {
            return;
        }

        var middle = (start + end) * 0.5f;
        var at = center + new Vector2(MathF.Cos(middle), MathF.Sin(middle)) * radius * NameRadiusShare;
        var width = MathF.Min(arc, radius * 0.6f);
        var fitted = Typography.FitText(name, width, TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, at, fitted, CasinoColors.InkTitle, TextStyles.FootnoteEmphasized);
    }

    private static void DrawPointer(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var tip = new Vector2(center.X, center.Y - radius + 10f * scale);
        var left = new Vector2(center.X - 11f * scale, center.Y - radius - 14f * scale);
        var right = new Vector2(center.X + 11f * scale, center.Y - radius - 14f * scale);
        drawList.AddTriangleFilled(left, right, tip, ImGui.GetColorU32(CasinoColors.MoneyHighlight));
        drawList.AddTriangle(left, right, tip, ImGui.GetColorU32(CasinoColors.FeltBottom), 1.5f * scale);
    }
}

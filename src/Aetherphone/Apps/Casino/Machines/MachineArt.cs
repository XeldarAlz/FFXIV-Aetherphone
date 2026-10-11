using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Machines;

internal static class MachineArt
{
    public const float ChassisRadius = 18f;
    public const float BulbInset = 7f;

    private static readonly Vector4 BirdTop = new(0.16f, 0.07f, 0.02f, 1f);
    private static readonly Vector4 BirdBottom = new(0.05f, 0.02f, 0.01f, 1f);
    private static readonly Vector4 CascadeTop = new(0.02f, 0.10f, 0.16f, 1f);
    private static readonly Vector4 CascadeBottom = new(0.01f, 0.03f, 0.07f, 1f);
    private static readonly Vector4 MoogleTop = new(0.14f, 0.04f, 0.10f, 1f);
    private static readonly Vector4 MoogleBottom = new(0.05f, 0.01f, 0.04f, 1f);
    private static readonly Vector4 BirdFeature = new(0.55f, 0.24f, 0.02f, 1f);
    private static readonly Vector4 CascadeFeature = new(0.05f, 0.30f, 0.42f, 1f);
    private static readonly Vector4 MoogleFeature = new(0.45f, 0.10f, 0.28f, 1f);
    private static readonly Vector4 Crystal = new(0.55f, 0.95f, 1f, 1f);

    public static Vector4 NeonOf(string machineId) => machineId switch
    {
        SlotsRules.CascadeId => CasinoColors.LightB,
        SlotsRules.MoogleId => CasinoColors.LightA,
        _ => CasinoColors.Money,
    };

    public static CasinoSign SignOf(string machineId) => machineId switch
    {
        SlotsRules.CascadeId => CasinoSign.Cascade,
        SlotsRules.MoogleId => CasinoSign.Moogle,
        _ => CasinoSign.GoldenBird,
    };

    public static void SceneTint(ImDrawListPtr drawList, Rect full, string machineId, float amount, float phase,
        float scale)
    {
        if (amount <= 0.01f)
        {
            return;
        }

        var tint = machineId switch
        {
            SlotsRules.CascadeId => CascadeFeature,
            SlotsRules.MoogleId => MoogleFeature,
            _ => BirdFeature,
        };
        drawList.AddRectFilledMultiColor(full.Min, full.Max, ImGui.GetColorU32(tint with { W = 0.55f * amount }),
            ImGui.GetColorU32(tint with { W = 0.55f * amount }), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.3f * amount)),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.3f * amount)));
        if (string.Equals(machineId, SlotsRules.CascadeId, StringComparison.Ordinal))
        {
            DrawShards(drawList, full, amount, phase, scale);
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(phase * 1.3f);
        for (var index = 0; index < 18; index++)
        {
            var x = full.Min.X + full.Width * Fraction(index * 0.618f);
            var drift = Fraction(index * 0.37f + phase * (0.03f + index * 0.002f));
            var y = full.Max.Y - full.Height * drift;
            var radius = (6f + index % 5 * 3f) * scale;
            drawList.AddCircleFilled(new Vector2(x, y), radius,
                ImGui.GetColorU32(NeonOf(machineId) with { W = 0.10f * amount * (0.6f + 0.4f * pulse) }), 16);
        }
    }

    public static void Chassis(ImDrawListPtr drawList, Rect rect, string machineId, float phase, float lit,
        bool featured, float scale)
    {
        var radius = ChassisRadius * scale;
        var top = machineId switch
        {
            SlotsRules.CascadeId => CascadeTop,
            SlotsRules.MoogleId => MoogleTop,
            _ => BirdTop,
        };
        var bottom = machineId switch
        {
            SlotsRules.CascadeId => CascadeBottom,
            SlotsRules.MoogleId => MoogleBottom,
            _ => BirdBottom,
        };
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        var neon = NeonOf(machineId);
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(neon with { W = 0.55f }),
            2f * scale);
        CasinoLights.BulbChase(drawList, rect.Inset(-BulbInset * 0.4f * scale), radius, scale,
            featured ? phase * 2f : phase, CasinoLights.BulbPitch, CasinoColors.Money, neon, lit);
    }

    public static void ReelWindow(ImDrawListPtr drawList, Rect rect, string machineId, float scale)
    {
        var radius = 12f * scale;
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(new Vector4(0.03f, 0.02f, 0.06f, 0.96f)),
            ImGui.GetColorU32(new Vector4(0.08f, 0.06f, 0.12f, 0.96f)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(NeonOf(machineId) with { W = 0.35f }), 1.5f * scale);
        drawList.AddRectFilledMultiColor(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + rect.Height * 0.12f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0f)), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0f)));
    }

    public static void Hero(ImDrawListPtr drawList, Vector2 center, float extent, string machineId, float phase)
    {
        var bob = MathF.Sin(phase * 2.2f) * extent * 0.06f;
        var at = center + new Vector2(0f, bob);
        switch (machineId)
        {
            case SlotsRules.CascadeId:
                drawList.AddCircleFilled(at, extent * 1.1f, ImGui.GetColorU32(Crystal with { W = 0.12f }), 32);
                MachineSymbols.Draw(drawList, machineId, CrystalCascadeRules.Scatter, at, extent, 1f,
                    MachineSymbols.ShimmerFrame(0, phase));
                return;
            case SlotsRules.MoogleId:
                MachineSymbols.Draw(drawList, machineId, MoogleMoneyRules.Moogle, at, extent, 1f, false);
                return;
            default:
                MachineSymbols.DrawBird(drawList, at, extent, 1f);
                return;
        }
    }

    public static void VolatilityBars(ImDrawListPtr drawList, Vector2 origin, float height, int lit, Vector4 ink,
        float scale)
    {
        var width = 6f * scale;
        var gap = 3f * scale;
        for (var bar = 0; bar < 5; bar++)
        {
            var barHeight = height * (0.4f + 0.15f * bar);
            var min = new Vector2(origin.X + bar * (width + gap), origin.Y + height - barHeight);
            var max = new Vector2(min.X + width, origin.Y + height);
            Squircle.Fill(drawList, min, max, 2f * scale,
                ImGui.GetColorU32(bar < lit ? ink : CasinoColors.InkMuted with { W = 0.35f }));
        }
    }

    private static void DrawShards(ImDrawListPtr drawList, Rect full, float amount, float phase, float scale)
    {
        Span<Vector2> shard = stackalloc Vector2[3];
        for (var index = 0; index < 14; index++)
        {
            var x = full.Min.X + full.Width * Fraction(index * 0.618f + 0.1f);
            var baseY = index % 2 == 0 ? full.Max.Y : full.Min.Y;
            var height = full.Height * (0.08f + 0.06f * (index % 4));
            var direction = index % 2 == 0 ? -1f : 1f;
            var shimmer = 0.5f + 0.5f * MathF.Sin(phase * 1.7f + index);
            shard[0] = new Vector2(x - 14f * scale, baseY);
            shard[1] = new Vector2(x + 14f * scale, baseY);
            shard[2] = new Vector2(x + 4f * scale, baseY + direction * height);
            drawList.PathClear();
            for (var point = 0; point < 3; point++)
            {
                drawList.PathLineTo(shard[point]);
            }

            drawList.PathFillConvex(ImGui.GetColorU32(Crystal with { W = (0.06f + 0.06f * shimmer) * amount }));
        }
    }

    private static float Fraction(float value) => value - MathF.Floor(value);
}

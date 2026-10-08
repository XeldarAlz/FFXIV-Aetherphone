using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal static class RaceBirdArt
{
    public const float NumberMinHeight = 26f;

    private const float LegWidth = 0.065f;
    private const float WingShade = 0.28f;
    private const float CrestShade = 0.2f;

    public static readonly Vector4[] Plumage =
    {
        new(0.98f, 0.76f, 0.24f, 1f),
        new(0.20f, 0.19f, 0.24f, 1f),
        new(0.80f, 0.45f, 0.24f, 1f),
        new(0.74f, 0.80f, 0.94f, 1f),
        new(1.00f, 0.62f, 0.16f, 1f),
        new(0.30f, 0.66f, 0.42f, 1f),
        new(0.24f, 0.42f, 0.90f, 1f),
        new(0.98f, 0.50f, 0.10f, 1f),
        new(0.52f, 0.30f, 0.72f, 1f),
        new(0.86f, 0.28f, 0.30f, 1f),
        new(0.30f, 0.50f, 0.58f, 1f),
        new(0.96f, 0.93f, 0.84f, 1f),
    };

    public static readonly Vector4[] Cloth =
    {
        new(0.88f, 0.20f, 0.24f, 1f),
        new(0.95f, 0.95f, 0.95f, 1f),
        new(0.18f, 0.40f, 0.88f, 1f),
        new(0.98f, 0.84f, 0.18f, 1f),
        new(0.16f, 0.62f, 0.32f, 1f),
        new(0.12f, 0.12f, 0.14f, 1f),
        new(1.00f, 0.52f, 0.12f, 1f),
        new(0.98f, 0.44f, 0.70f, 1f),
    };

    private static readonly Vector4 Beak = new(0.98f, 0.62f, 0.18f, 1f);
    private static readonly Vector4 Leg = new(0.90f, 0.55f, 0.16f, 1f);
    private static readonly Vector4 EyeInk = new(0.06f, 0.05f, 0.08f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.35f);

    public static Vector4 PlumageOf(int colour) => Plumage[Math.Clamp(colour, 0, Plumage.Length - 1)];

    public static Vector4 ClothOf(int slot) => Cloth[Math.Clamp(slot, 0, Cloth.Length - 1)];

    public static Vector4 InkOn(Vector4 fill) => Palette.Luminance(fill) > 0.55f
        ? new Vector4(0.08f, 0.07f, 0.10f, 1f)
        : White;

    public static void DrawSide(ImDrawListPtr drawList, Vector2 foot, float height, int colour, int silk, int slot,
        int frame, float alpha)
    {
        var body = PlumageOf(colour) with { W = alpha };
        var wing = Palette.Mix(body, new Vector4(0f, 0f, 0f, 1f), WingShade) with { W = alpha };
        var bodyColor = ImGui.GetColorU32(body);
        var legColor = ImGui.GetColorU32(Leg with { W = alpha });
        var bob = frame == 0 ? 0f : -0.035f * height;
        Shapes.FillEllipse(drawList, foot + new Vector2(0f, -0.01f * height), new Vector2(0.3f * height, 0.05f * height),
            ImGui.GetColorU32(Shadow with { W = Shadow.W * alpha }), 16);
        DrawLegs(drawList, foot, height, frame, legColor);
        var bodyCenter = foot + new Vector2(0f, -0.56f * height + bob);
        DrawTail(drawList, bodyCenter, height, ImGui.GetColorU32(wing));
        Shapes.FillEllipse(drawList, bodyCenter, new Vector2(0.32f * height, 0.21f * height), -0.12f, bodyColor, 20);
        var neckBase = bodyCenter + new Vector2(0.18f * height, -0.08f * height);
        var head = bodyCenter + new Vector2(0.36f * height, -0.42f * height);
        drawList.AddLine(neckBase, head, bodyColor, 0.15f * height);
        DrawCrest(drawList, head, height,
            ImGui.GetColorU32(Palette.Mix(body, new Vector4(1f, 1f, 1f, 1f), CrestShade) with { W = alpha }));
        drawList.AddCircleFilled(head, 0.11f * height, bodyColor, 16);
        var beakRoot = head + new Vector2(0.08f * height, -0.01f * height);
        drawList.AddTriangleFilled(beakRoot + new Vector2(0f, -0.045f * height),
            beakRoot + new Vector2(0.17f * height, 0.02f * height), beakRoot + new Vector2(0f, 0.05f * height),
            ImGui.GetColorU32(Beak with { W = alpha }));
        drawList.AddCircleFilled(head + new Vector2(0.03f * height, -0.025f * height), MathF.Max(1f, 0.022f * height),
            ImGui.GetColorU32(EyeInk with { W = alpha }), 8);
        Shapes.FillEllipse(drawList, bodyCenter + new Vector2(-0.04f * height, -0.01f * height),
            new Vector2(0.19f * height, 0.1f * height), -0.25f, ImGui.GetColorU32(wing), 16);
        var clothMin = bodyCenter + new Vector2(-0.1f * height, -0.17f * height);
        var clothMax = bodyCenter + new Vector2(0.13f * height, 0.06f * height);
        DrawSilk(drawList, clothMin, clothMax, slot, silk, alpha);
        if (height < NumberMinHeight * UiScale.Current)
        {
            return;
        }

        Typography.DrawCenteredExact(drawList, (clothMin + clothMax) * 0.5f, GameNumber.Label(slot + 1),
            InkOn(ClothOf(slot)) with { W = alpha }, TextStyles.Caption2.Scale * height / (46f * UiScale.Current),
            FontWeight.Bold);
    }

    public static void DrawTop(ImDrawListPtr drawList, Vector2 center, float length, int colour, int silk, int slot,
        int frame, float alpha)
    {
        var body = PlumageOf(colour) with { W = alpha };
        var wing = Palette.Mix(body, new Vector4(0f, 0f, 0f, 1f), WingShade) with { W = alpha };
        var bodyColor = ImGui.GetColorU32(body);
        var legColor = ImGui.GetColorU32(Leg with { W = alpha });
        var stride = frame == 0 ? 1f : -1f;
        drawList.AddCircleFilled(center + new Vector2(-0.12f * length, 0.1f * length * stride), 0.07f * length, legColor,
            10);
        drawList.AddCircleFilled(center + new Vector2(0.12f * length, -0.1f * length * stride), 0.07f * length, legColor,
            10);
        Shapes.FillEllipse(drawList, center + new Vector2(0f, 0.34f * length), new Vector2(0.1f * length, 0.16f * length),
            ImGui.GetColorU32(wing), 12);
        Shapes.FillEllipse(drawList, center, new Vector2(0.24f * length, 0.32f * length), bodyColor, 20);
        Shapes.FillEllipse(drawList, center + new Vector2(-0.2f * length, 0.02f * length),
            new Vector2(0.08f * length, 0.22f * length), ImGui.GetColorU32(wing), 12);
        Shapes.FillEllipse(drawList, center + new Vector2(0.2f * length, 0.02f * length),
            new Vector2(0.08f * length, 0.22f * length), ImGui.GetColorU32(wing), 12);
        var head = center + new Vector2(0f, -0.4f * length);
        drawList.AddCircleFilled(head, 0.13f * length, bodyColor, 16);
        drawList.AddTriangleFilled(head + new Vector2(-0.06f * length, -0.08f * length),
            head + new Vector2(0f, -0.26f * length), head + new Vector2(0.06f * length, -0.08f * length),
            ImGui.GetColorU32(Beak with { W = alpha }));
        var clothMin = center + new Vector2(-0.13f * length, -0.12f * length);
        var clothMax = center + new Vector2(0.13f * length, 0.16f * length);
        DrawSilk(drawList, clothMin, clothMax, slot, silk, alpha);
    }

    public static void DrawSilk(ImDrawListPtr drawList, Vector2 min, Vector2 max, int slot, int silk, float alpha)
    {
        var baseColor = ClothOf(slot) with { W = alpha };
        var trim = InkOn(ClothOf(slot)) with { W = alpha * 0.85f };
        var rounding = (max.Y - min.Y) * 0.2f;
        var trimColor = ImGui.GetColorU32(trim);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(baseColor), rounding);
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        var line = MathF.Max(1f, height * 0.14f);
        switch (Math.Clamp(silk, 0, RaceRules.SilkCount - 1))
        {
            case 1:
                drawList.AddLine(new Vector2(min.X, min.Y + height * 0.35f), new Vector2(max.X, min.Y + height * 0.35f),
                    trimColor, line);
                drawList.AddLine(new Vector2(min.X, min.Y + height * 0.7f), new Vector2(max.X, min.Y + height * 0.7f),
                    trimColor, line);
                break;
            case 2:
                drawList.AddLine(new Vector2(min.X + width * 0.33f, min.Y), new Vector2(min.X + width * 0.33f, max.Y),
                    trimColor, line);
                drawList.AddLine(new Vector2(min.X + width * 0.66f, min.Y), new Vector2(min.X + width * 0.66f, max.Y),
                    trimColor, line);
                break;
            case 3:
                drawList.AddLine(new Vector2(min.X, min.Y + height * 0.3f), new Vector2(min.X + width * 0.5f, max.Y - height * 0.2f),
                    trimColor, line);
                drawList.AddLine(new Vector2(min.X + width * 0.5f, max.Y - height * 0.2f), new Vector2(max.X, min.Y + height * 0.3f),
                    trimColor, line);
                break;
            case 4:
                drawList.AddCircleFilled(new Vector2(min.X + width * 0.3f, min.Y + height * 0.5f), line * 1.1f, trimColor, 8);
                drawList.AddCircleFilled(new Vector2(min.X + width * 0.7f, min.Y + height * 0.5f), line * 1.1f, trimColor, 8);
                break;
            case 5:
            {
                var center = (min + max) * 0.5f;
                drawList.AddQuadFilled(center + new Vector2(0f, -height * 0.32f), center + new Vector2(width * 0.28f, 0f),
                    center + new Vector2(0f, height * 0.32f), center + new Vector2(-width * 0.28f, 0f), trimColor);
                break;
            }
        }
    }

    private static void DrawLegs(ImDrawListPtr drawList, Vector2 foot, float height, int frame, uint color)
    {
        var hip = foot + new Vector2(0f, -0.4f * height);
        var thickness = MathF.Max(1f, LegWidth * height);
        if (frame == 0)
        {
            Leg2(drawList, hip, foot + new Vector2(0.06f * height, -0.2f * height), foot + new Vector2(0.24f * height, 0f),
                color, thickness);
            Leg2(drawList, hip, foot + new Vector2(-0.14f * height, -0.18f * height),
                foot + new Vector2(-0.22f * height, -0.04f * height), color, thickness);
            return;
        }

        Leg2(drawList, hip, foot + new Vector2(0.1f * height, -0.24f * height), foot + new Vector2(0.04f * height, -0.12f * height),
            color, thickness);
        Leg2(drawList, hip, foot + new Vector2(-0.04f * height, -0.2f * height), foot + new Vector2(-0.02f * height, 0f),
            color, thickness);
    }

    private static void Leg2(ImDrawListPtr drawList, Vector2 hip, Vector2 knee, Vector2 toe, uint color, float thickness)
    {
        drawList.AddLine(hip, knee, color, thickness * 1.4f);
        drawList.AddLine(knee, toe, color, thickness);
        drawList.AddLine(toe, toe + new Vector2(thickness * 1.8f, 0f), color, thickness);
    }

    private static void DrawTail(ImDrawListPtr drawList, Vector2 bodyCenter, float height, uint color)
    {
        var root = bodyCenter + new Vector2(-0.26f * height, -0.04f * height);
        drawList.AddTriangleFilled(root + new Vector2(0f, -0.08f * height), root + new Vector2(-0.24f * height, -0.24f * height),
            root + new Vector2(0.02f * height, 0.06f * height), color);
        drawList.AddTriangleFilled(root + new Vector2(0f, -0.02f * height), root + new Vector2(-0.26f * height, -0.08f * height),
            root + new Vector2(0.02f * height, 0.08f * height), color);
    }

    private static void DrawCrest(ImDrawListPtr drawList, Vector2 head, float height, uint color)
    {
        drawList.AddTriangleFilled(head + new Vector2(-0.04f * height, -0.08f * height),
            head + new Vector2(-0.16f * height, -0.22f * height), head + new Vector2(0.02f * height, -0.1f * height), color);
        drawList.AddTriangleFilled(head + new Vector2(-0.08f * height, -0.04f * height),
            head + new Vector2(-0.22f * height, -0.12f * height), head + new Vector2(-0.04f * height, -0.09f * height), color);
    }
}

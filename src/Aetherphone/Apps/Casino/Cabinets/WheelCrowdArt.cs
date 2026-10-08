using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class WheelCrowdArt
{
    public const int MaxFaces = 5;
    public const float FaceRadius = 6f;
    public const float FaceStep = 9f;

    private const float PlusGap = 3f;
    private const int DiscSegments = 16;

    private static readonly Vector4[] Tones =
    {
        new(0.36f, 0.42f, 0.70f, 1f),
        new(0.62f, 0.36f, 0.58f, 1f),
        new(0.30f, 0.56f, 0.52f, 1f),
        new(0.70f, 0.50f, 0.32f, 1f),
        new(0.44f, 0.40f, 0.52f, 1f),
        new(0.58f, 0.30f, 0.36f, 1f),
    };

    private static readonly Vector4 Silhouette = new(0.05f, 0.04f, 0.09f, 0.55f);

    public static int FacesFor(int bettors, bool mine)
    {
        var crowd = mine && bettors < 1 ? 1 : bettors;
        return Math.Clamp(crowd, 0, MaxFaces);
    }

    public static int OverflowFor(int bettors, bool mine)
    {
        var crowd = mine && bettors < 1 ? 1 : bettors;
        return Math.Max(0, crowd - MaxFaces);
    }

    public static float Height(float scale) => FaceRadius * 2f * scale;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float width, int spot, int bettors, bool mine,
        float scale)
    {
        var faces = FacesFor(bettors, mine);
        if (faces <= 0)
        {
            return;
        }

        var overflow = OverflowFor(bettors, mine);
        var plus = overflow > 0 ? NumberText.Signed(overflow) : string.Empty;
        var plusWidth = overflow > 0 ? Typography.Measure(plus, TextStyles.Caption2).X + PlusGap * scale : 0f;
        var radius = FaceRadius * scale;
        var step = FaceStep * scale;
        var available = width - plusWidth - radius * 2f;
        if (faces > 1 && step * (faces - 1) > available)
        {
            step = MathF.Max(radius * 0.6f, available / (faces - 1));
        }

        var total = radius * 2f + step * (faces - 1) + plusWidth;
        var x = center.X - total * 0.5f + radius;
        for (var face = faces - 1; face >= 0; face--)
        {
            var faceCenter = new Vector2(x + step * face, center.Y);
            var tone = Tones[(spot * 3 + face) % Tones.Length];
            var yours = mine && face == 0;
            AvatarView.Draw(drawList, faceCenter, radius, yours ? CasinoColors.Money : tone, string.Empty, 1f,
                AvatarHandle.Disabled, DiscSegments);
            DrawSilhouette(drawList, faceCenter, radius);
            drawList.AddCircle(faceCenter, radius, ImGui.GetColorU32(yours ? CasinoColors.MoneyHighlight
                : new Vector4(0.04f, 0.03f, 0.08f, 0.9f)), DiscSegments, MathF.Max(1f, scale));
        }

        if (overflow <= 0)
        {
            return;
        }

        var plusLeft = x + step * (faces - 1) + radius + PlusGap * scale;
        var plusSize = Typography.Measure(plus, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(plusLeft, center.Y - plusSize.Y * 0.5f), plus, CasinoColors.InkBody,
            TextStyles.Caption2);
    }

    private static void DrawSilhouette(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        var ink = ImGui.GetColorU32(Silhouette);
        drawList.AddCircleFilled(new Vector2(center.X, center.Y - radius * 0.28f), radius * 0.32f, ink, 12);
        drawList.PathClear();
        drawList.PathArcTo(new Vector2(center.X, center.Y + radius * 0.62f), radius * 0.52f, MathF.PI, MathF.PI * 2f, 10);
        drawList.PathFillConvex(ink);
    }
}

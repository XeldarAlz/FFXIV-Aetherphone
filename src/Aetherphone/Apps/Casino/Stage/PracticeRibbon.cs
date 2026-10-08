using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class PracticeRibbon
{
    private const float StripeWidth = 14f;
    private const float StripeAlpha = 0.10f;
    private const float FillAlpha = 0.20f;
    private const float EdgeAlpha = 0.45f;

    public static void Draw(ImDrawListPtr drawList, Rect rect, float scale)
    {
        if (rect.Height <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(rect.Min, rect.Max,
            ImGui.GetColorU32(CasinoColors.Practice with { W = FillAlpha }));
        drawList.PushClipRect(rect.Min, rect.Max, true);
        var stripe = ImGui.GetColorU32(CasinoColors.Practice with { W = StripeAlpha });
        var pitch = StripeWidth * 2f * scale;
        for (var x = rect.Min.X - rect.Height; x < rect.Max.X; x += pitch)
        {
            drawList.AddQuadFilled(new Vector2(x, rect.Max.Y), new Vector2(x + StripeWidth * scale, rect.Max.Y),
                new Vector2(x + StripeWidth * scale + rect.Height, rect.Min.Y), new Vector2(x + rect.Height, rect.Min.Y),
                stripe);
        }

        drawList.PopClipRect();
        var edge = ImGui.GetColorU32(CasinoColors.Practice with { W = EdgeAlpha });
        drawList.AddLine(new Vector2(rect.Min.X, rect.Max.Y), rect.Max, edge, MathF.Max(1f, scale));
        var label = Typography.FitText(Loc.T(L.Strip.PracticeRibbon), rect.Width - Metrics.Space.Lg * 2f * scale,
            TextStyles.Caption1);
        Typography.DrawCentered(drawList, rect.Center, label, CasinoColors.InkTitle, TextStyles.Caption1);
    }
}

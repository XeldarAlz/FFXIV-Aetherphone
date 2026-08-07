using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CopyToast
{
    private const float LifetimeSeconds = 1.15f;
    private const float RiseDistance = 26f;
    private const float MaxFrameSeconds = 0.1f;

    private static Vector2 origin;
    private static float elapsed = LifetimeSeconds;
    private static string label = string.Empty;
    private static int frame = -1;

    public static void Show() => Show(Loc.T(L.Common.Copied));

    public static void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        origin = ImGui.GetMousePos();
        elapsed = 0f;
        label = text;
    }

    public static void Flush()
    {
        var current = ImGui.GetFrameCount();
        if (elapsed >= LifetimeSeconds || frame == current)
        {
            return;
        }

        frame = current;
        elapsed += MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        var progress = Math.Clamp(elapsed / LifetimeSeconds, 0f, 1f);
        var alpha = 1f - Easing.EaseOutQuint(progress);
        if (alpha <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        var style = TextStyles.FootnoteEmphasized;
        var padX = 10f * scale;
        var padY = 6f * scale;
        var margin = 8f * scale;
        var textSize = Typography.Measure(label, style);
        var width = textSize.X + 2f * padX;
        var height = textSize.Y + 2f * padY;
        var viewport = ImGui.GetMainViewport();
        var left = viewport.Pos.X + margin;
        var right = viewport.Pos.X + viewport.Size.X - margin;
        var centerX = right - left > width
            ? Math.Clamp(origin.X, left + width * 0.5f, right - width * 0.5f)
            : (left + right) * 0.5f;
        var centerY = origin.Y - height - Easing.EaseOutQuint(progress) * RiseDistance * scale;
        var center = new Vector2(centerX, centerY);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rounding = height * 0.5f;
        var drawList = ImGui.GetForegroundDrawList();
        Elevation.Floating(drawList, min, max, rounding, scale, alpha);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 0.96f * alpha)));
        Material.EdgeSquircle(drawList, min, max, rounding, scale, alpha);
        Typography.DrawCentered(drawList, center, label, new Vector4(0.96f, 0.96f, 0.98f, alpha), style);
    }
}

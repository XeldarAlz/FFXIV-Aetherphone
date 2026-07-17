using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class InfiniteScroll
{
    private const float BottomTriggerDistance = 320f;
    private const float LoadingRowHeight = 26f;

    public static bool ReachedBottom()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = ImGui.GetScrollMaxY();
        return max > 0f && ImGui.GetScrollY() >= max - BottomTriggerDistance * scale;
    }

    public static void DrawLoadingRow(float centerX, Vector4 ink)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = LoadingRowHeight * scale;
        var centerY = origin.Y + rowHeight * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        var dotRadius = 2.8f * scale;
        var dotGap = 6f * scale;
        var baseX = centerX - (dotRadius * 2f + dotGap);
        var phase = (float)ImGui.GetTime();
        for (var dot = 0; dot < 3; dot++)
        {
            var wave = MathF.Max(0f, MathF.Sin(phase * 6f - dot * 0.9f));
            var alpha = 0.30f + 0.55f * wave;
            var center = new Vector2(baseX + dot * (dotRadius * 2f + dotGap), centerY);
            drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 16);
        }

        ImGui.Dummy(new Vector2(0f, rowHeight));
    }
}

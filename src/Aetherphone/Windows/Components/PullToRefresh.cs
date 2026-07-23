using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

/// <summary>Pull-to-refresh gesture for a list inside an <see cref="AppSurface"/>; one instance per list.</summary>
internal sealed class PullToRefresh
{
    private const float ArmThreshold = 64f;
    private const float MinSpinnerSeconds = 0.5f;
    private const float MaxSpinnerSeconds = 20f;

    private bool wasDragging;
    private bool armed;
    private bool refreshing;
    private float spinnerElapsed;

    public void Draw(Rect area, float pull, bool dragging, bool loading, Vector4 ink, Action onRefresh)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dt = ImGui.GetIO().DeltaTime;

        if (refreshing)
        {
            spinnerElapsed += dt;
            if ((!loading && spinnerElapsed >= MinSpinnerSeconds) || spinnerElapsed >= MaxSpinnerSeconds)
            {
                refreshing = false;
            }
        }

        if (dragging)
        {
            wasDragging = true;
            armed = pull >= ArmThreshold * scale;
        }
        else if (wasDragging)
        {
            wasDragging = false;
            if (armed && !refreshing && !loading)
            {
                refreshing = true;
                spinnerElapsed = 0f;
                onRefresh();
            }

            armed = false;
        }

        DrawIndicator(area, pull, scale, ink);
    }

    private void DrawIndicator(Rect area, float pull, float scale, Vector4 ink)
    {
        var progress = refreshing ? 1f : Math.Clamp(pull / (ArmThreshold * scale), 0f, 1f);
        if (progress <= 0f)
        {
            return;
        }

        var centerX = area.Center.X;
        var centerY = area.Min.Y + 20f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var dotRadius = 2.8f * scale;
        var dotGap = 6f * scale;
        var baseX = centerX - (dotRadius * 2f + dotGap);
        var phase = (float)ImGui.GetTime();
        for (var dot = 0; dot < 3; dot++)
        {
            float alpha;
            if (refreshing)
            {
                var wave = MathF.Max(0f, MathF.Sin(phase * 6f - dot * 0.9f));
                alpha = 0.30f + 0.55f * wave;
            }
            else
            {
                alpha = progress * (dot < progress * 3f ? 0.85f : 0.25f);
            }

            var center = new Vector2(baseX + dot * (dotRadius * 2f + dotGap), centerY);
            drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 16);
        }
    }
}

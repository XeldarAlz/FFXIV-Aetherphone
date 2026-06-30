using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperTabs
{
    private const float SmoothTime = 0.16f;

    private static readonly Dictionary<string, Spring> Indicators = new(StringComparer.Ordinal);

    public static int Draw(string id, Rect row, IReadOnlyList<string> tabs, int selected, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var tabWidth = row.Width / tabs.Count;

        var result = selected;
        for (var index = 0; index < tabs.Count; index++)
        {
            var tabMin = new Vector2(row.Min.X + index * tabWidth, row.Min.Y);
            var tabMax = new Vector2(row.Min.X + (index + 1) * tabWidth, row.Max.Y);
            var hovered = ImGui.IsMouseHoveringRect(tabMin, tabMax);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    result = index;
                }
            }

            var emphasis = index == selected ? theme.TextStrong : hovered ? Palette.Mix(theme.TextMuted, theme.TextStrong, 0.4f) : theme.TextMuted;
            var center = new Vector2((tabMin.X + tabMax.X) * 0.5f, row.Center.Y);
            Typography.DrawCentered(center, tabs[index], emphasis, 0.95f, FontWeight.SemiBold);
        }

        drawList.AddLine(new Vector2(row.Min.X, row.Max.Y), new Vector2(row.Max.X, row.Max.Y), ImGui.GetColorU32(theme.Separator), 1f);

        var position = Animate(id, selected);
        var labelHalf = Typography.Measure(tabs[Math.Clamp((int)MathF.Round(position), 0, tabs.Count - 1)], 0.95f, FontWeight.SemiBold).X * 0.5f;
        var indicatorCenterX = row.Min.X + (position + 0.5f) * tabWidth;
        var indicatorWidth = MathF.Max(labelHalf + 8f * scale, 18f * scale);
        var indicatorMin = new Vector2(indicatorCenterX - indicatorWidth, row.Max.Y - 3f * scale);
        var indicatorMax = new Vector2(indicatorCenterX + indicatorWidth, row.Max.Y);
        Squircle.Fill(drawList, indicatorMin, indicatorMax, 1.5f * scale, ImGui.GetColorU32(theme.Accent));

        return result;
    }

    private static float Animate(string id, int selected)
    {
        if (!Indicators.TryGetValue(id, out var spring))
        {
            spring = new Spring(selected);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var position = spring.Step(selected, SmoothTime, deltaSeconds);
        Indicators[id] = spring;
        return position;
    }
}

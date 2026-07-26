using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal readonly record struct NavTab(FontAwesomeIcon Icon, string Label, int Badge = 0, bool Raised = false);

internal sealed class BottomTabBar
{
    public const float Height = 52f;

    private const float HitRadius = 17f;
    private const float RaisedRadius = 19f;
    private const float PillWidth = 40f;
    private const float PillHeight = 34f;
    private const float PillAlpha = 0.10f;
    private const float HoverSmoothTime = 0.12f;
    private const float MaxFrameSeconds = 0.1f;
    private const float IconScale = 1.2f;
    private const float ActiveIconScale = 1.3f;

    private Spring[] hover = Array.Empty<Spring>();

    public int Draw(Rect bar, AppSkin ui, PhoneTheme theme, ReadOnlySpan<NavTab> tabs, int active)
    {
        if (tabs.Length == 0)
        {
            return -1;
        }

        if (hover.Length != tabs.Length)
        {
            hover = new Spring[tabs.Length];
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);
        var slot = bar.Width / tabs.Length;
        var tapped = -1;
        for (var index = 0; index < tabs.Length; index++)
        {
            var center = new Vector2(bar.Min.X + slot * (index + 0.5f), bar.Center.Y);
            var picked = tabs[index].Raised
                ? DrawRaised(drawList, ui, tabs[index], center, index, scale)
                : DrawTab(ui, theme, tabs[index], center, index, index == active, scale);
            if (picked)
            {
                tapped = index;
            }
        }

        return tapped;
    }

    private bool DrawTab(AppSkin ui, PhoneTheme theme, in NavTab tab, Vector2 center, int slot, bool active,
        float scale)
    {
        DrawHoverPill(ui, center, StepHover(slot, center, HitRadius * scale), scale);
        var ink = active ? ui.TitleInk : ui.MutedInk;
        var picked = ui.IconButton(center, HitRadius * scale, tab.Icon.ToIconString(), ink, AppSkin.Transparent,
            active ? ActiveIconScale : IconScale, tab.Label);
        ActivityBadge.Draw(center + new Vector2(11f * scale, -10f * scale), tab.Badge, theme, scale);
        return picked;
    }

    private bool DrawRaised(ImDrawListPtr drawList, AppSkin ui, in NavTab tab, Vector2 center, int slot, float scale)
    {
        var radius = RaisedRadius * scale;
        var half = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - half, center + half);
        var grow = 1f + 0.06f * StepHover(slot, center, radius);
        var fill = hovered ? Palette.Mix(ui.Accent, new Vector4(1f, 1f, 1f, 1f), 0.12f) : ui.Accent;
        Elevation.Floating(drawList, center - half, center + half, radius, scale, 0.55f);
        drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(fill), 40);
        AppSkin.Icon(drawList, center, tab.Icon.ToIconString(), new Vector4(0.11f, 0.08f, 0.02f, 1f), 1.05f);
        HoverTooltip.Show(new Rect(center - half, center + half), tab.Label, HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - half, center + half, hovered);
    }

    private float StepHover(int slot, Vector2 center, float radius)
    {
        var half = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - half, center + half);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        hover[slot].Step(hovered ? 1f : 0f, HoverSmoothTime, delta);
        return Math.Clamp(hover[slot].Value, 0f, 1f);
    }

    private static void DrawHoverPill(AppSkin ui, Vector2 center, float amount, float scale)
    {
        if (amount <= 0.001f)
        {
            return;
        }

        var grow = 0.86f + 0.14f * amount;
        var half = new Vector2(PillWidth * 0.5f * scale * grow, PillHeight * 0.5f * scale * grow);
        var tint = Palette.WithAlpha(ui.HoverTint, PillAlpha * amount);
        Squircle.Fill(ImGui.GetWindowDrawList(), center - half, center + half, half.Y, ImGui.GetColorU32(tint));
    }
}

using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.News;

internal sealed class NewsUi
{
    private static readonly Vector4 BackdropTop = new(0.07f, 0.07f, 0.10f, 1f);
    private static readonly Vector4 BackdropBottom = new(0.03f, 0.03f, 0.05f, 1f);
    private static readonly Vector4 BloomTop = new(0.28f, 0.28f, 0.34f, 0.14f);
    private static readonly Vector4 BloomBottom = new(0.10f, 0.10f, 0.14f, 0f);
    public PhoneTheme Theme { get; set; } = PhoneTheme.Default;

    public void Backdrop(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        PaintGradient(ImGui.GetWindowDrawList(), screen, screen, Theme.ScreenRounding * scale);
    }

    public void Body(Rect area)
    {
        var frame = SceneChrome.ScreenFrom(area, Theme, ImGuiHelpers.GlobalScale);
        PaintGradient(ImGui.GetWindowDrawList(), area, frame, 0f);
    }

    public static void PaintGradient(ImDrawListPtr drawList, Rect target, Rect frame, float rounding)
    {
        var topFraction = frame.Height <= 0f ? 0f : (target.Min.Y - frame.Min.Y) / frame.Height;
        var bottomFraction = frame.Height <= 0f ? 1f : (target.Max.Y - frame.Min.Y) / frame.Height;
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(BackdropTop, BackdropBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(BackdropTop, BackdropBottom, bottomFraction)));
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(BloomTop, BloomBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(BloomTop, BloomBottom, bottomFraction)));
    }
}

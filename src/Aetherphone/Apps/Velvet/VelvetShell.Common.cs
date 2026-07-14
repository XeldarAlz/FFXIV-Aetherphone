using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private static Rect Reserve(float heightUnscaled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + heightUnscaled * scale));
        ImGui.Dummy(new Vector2(width, heightUnscaled * scale));
        return rect;
    }

    private static void Gap(float pixels)
    {
        ImGui.Dummy(new Vector2(0f, pixels * ImGuiHelpers.GlobalScale));
    }

    private static Rect AnchorBox(Vector2 center, float half)
    {
        var offset = new Vector2(half, half);
        return new Rect(center - offset, center + offset);
    }

    private static void WrapText(string text, Vector4 color, in TextStyle style)
    {
        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            ImGui.TextWrapped(text);
        }

        ImGui.PopTextWrapPos();
    }

    private void OpenReport(string targetType, string targetId, string title)
    {
        Plugin.Report.Open(new ReportPrompt
        {
            Title = title,
            Submit = (reason, done) => store.Report(targetType, targetId, reason, done),
        });
    }
}

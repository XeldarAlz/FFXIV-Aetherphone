using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TableStatusPill
{
    private const float PadX = 8f;
    private const float PadY = 3f;
    private const float FillAlpha = 0.2f;
    private const float Rounding = 8f;

    public static LocString LabelOf(bool paused) => paused ? L.HostDesk.StatusPaused : L.HostDesk.StatusOpen;

    public static Vector4 InkOf(bool paused) => paused ? TableLedger.Amber : TableLedger.SettledInk;

    public static Vector2 Size(bool paused, float scale)
    {
        var text = Typography.Measure(Loc.T(LabelOf(paused)), TextStyles.FootnoteEmphasized);
        return new Vector2(text.X + PadX * 2f * scale, Typography.LineHeight(TextStyles.FootnoteEmphasized)
            + PadY * 2f * scale);
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 min, bool paused, float scale)
    {
        var size = Size(paused, scale);
        var max = min + size;
        var ink = InkOf(paused);
        Squircle.Fill(drawList, min, max, Rounding * scale, ImGui.GetColorU32(ink with { W = FillAlpha }));
        Typography.Draw(drawList, new Vector2(min.X + PadX * scale, min.Y + PadY * scale), Loc.T(LabelOf(paused)), ink,
            TextStyles.FootnoteEmphasized);
    }
}

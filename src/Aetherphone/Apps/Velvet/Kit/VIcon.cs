using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VIcon
{
    public const float Header = 22f;
    public const float TabActive = 24f;
    public const float TabIdle = 22f;
    public const float CardAction = 24f;
    public const float Overflow = 20f;
    public const float Row = 18f;
    public const float Field = 18f;
    public const float Chip = 14f;
    public const float Small = 13f;

    public static bool Button(Vector2 center, float radius, string glyph, float size, Vector4 ink,
        string tooltip = "", HoverLabelSide side = HoverLabelSide.Above, int badge = 0) =>
        SocialChrome.DrawHeaderIcon(ImGui.GetWindowDrawList(), center, radius, glyph, size, tooltip, VelvetInk.Shared,
            ink, false, badge, side);
}

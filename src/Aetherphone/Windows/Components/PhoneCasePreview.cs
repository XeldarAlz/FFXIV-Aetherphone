using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneCasePreview
{
    private const float BezelFraction = 0.055f;
    private const float BodyRoundingFraction = 0.155f;
    private const float IslandWidthFraction = 0.34f;
    private const float IslandHeightFraction = 0.030f;
    private const float IslandTopFraction = 0.028f;

    public static void Draw(ImDrawListPtr drawList, Rect body, Vector4 caseColor, PhoneTheme theme, float scale)
    {
        var finish = new CaseFinish(caseColor);
        var bezel = body.Width * BezelFraction;
        var screen = body.Inset(bezel);
        var bodyRounding = body.Width * BodyRoundingFraction;
        var screenRounding = MathF.Max(bodyRounding - bezel, 0f);
        DeviceChrome.DrawShell(drawList, body, screen, bodyRounding, screenRounding, scale, finish, theme.ScreenBase);
        DrawIsland(drawList, screen, theme);
    }

    private static void DrawIsland(ImDrawListPtr drawList, Rect screen, PhoneTheme theme)
    {
        var width = screen.Width * IslandWidthFraction;
        var height = MathF.Max(screen.Height * IslandHeightFraction, 2f);
        var top = screen.Min.Y + screen.Height * IslandTopFraction;
        var min = new Vector2(screen.Center.X - width * 0.5f, top);
        var max = new Vector2(screen.Center.X + width * 0.5f, top + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(theme.BezelOuter), height * 0.5f);
    }
}

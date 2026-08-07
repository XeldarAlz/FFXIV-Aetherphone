using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneCasePreview
{
    private const float IslandWidthFraction = 0.34f;
    private const float IslandHeightFraction = 0.030f;
    private const float IslandTopFraction = 0.028f;

    public static void Draw(ImDrawListPtr drawList, Rect body, PhoneCase option, PhoneTheme theme, float scale)
    {
        var finish = new CaseFinish(option.Tint);
        var inner = Shrink(body, 1f + 2f * CaseArt.MarginFraction);
        var chassis = ChassisGeometry.Preview(inner, option.Kind);
        if (option.Kind == PhoneCaseKind.Art && PhoneCaseTextures.Thumb(option.TextureId) is { } thumb)
        {
            Squircle.Fill(drawList, chassis.Body.Min, chassis.Body.Max, chassis.BodyRadius,
                ImGui.GetColorU32(finish.Frame));
            CaseArt.QuadClipped(drawList, thumb, CaseArt.RectFor(chassis.Body), false, CaseArt.Tint(1f));
            Squircle.Fill(drawList, chassis.Glass.Min, chassis.Glass.Max, chassis.GlassRadius,
                ImGui.GetColorU32(finish.Glass));
            Squircle.Fill(drawList, chassis.Screen.Min, chassis.Screen.Max, chassis.ScreenRadius,
                ImGui.GetColorU32(theme.ScreenBase));
        }
        else
        {
            DeviceChrome.DrawShell(drawList, chassis, scale, finish, theme.ScreenBase);
        }

        DrawIsland(drawList, chassis.Screen, finish);
    }

    private static Rect Shrink(Rect rect, float factor)
    {
        var size = rect.Size / factor;
        var center = rect.Center;
        return new Rect(center - size * 0.5f, center + size * 0.5f);
    }

    private static void DrawIsland(ImDrawListPtr drawList, Rect screen, in CaseFinish finish)
    {
        var width = screen.Width * IslandWidthFraction;
        var height = MathF.Max(screen.Height * IslandHeightFraction, 2f);
        var top = screen.Min.Y + screen.Height * IslandTopFraction;
        var min = new Vector2(screen.Center.X - width * 0.5f, top);
        var max = new Vector2(screen.Center.X + width * 0.5f, top + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(finish.Glass), height * 0.5f);
    }
}

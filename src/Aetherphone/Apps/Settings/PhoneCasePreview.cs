using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings;

internal static class PhoneCasePreview
{
    public const float DeviceAspect = 0.443f;

    private const float CanvasWidthFactor = 1f + 2f * CaseArt.MarginFraction;
    private const float CanvasHeightFactor = 1f / DeviceAspect + 2f * CaseArt.MarginFraction;

    public const float CanvasAspect = CanvasWidthFactor / CanvasHeightFactor;

    private const float IslandWidthFraction = 0.34f;
    private const float IslandHeightFraction = 0.030f;
    private const float IslandTopFraction = 0.028f;

    public static Rect BodyRect(Rect canvas)
    {
        var bodyWidth = MathF.Min(canvas.Width / CanvasWidthFactor, canvas.Height / CanvasHeightFactor);
        var bodySize = new Vector2(bodyWidth, bodyWidth / DeviceAspect);
        return new Rect(canvas.Center - bodySize * 0.5f, canvas.Center + bodySize * 0.5f);
    }

    public static void Draw(ImDrawListPtr drawList, Rect body, PhoneCase option, PhoneTheme theme, float scale,
        bool fullResolution = false)
    {
        var finish = new CaseFinish(option.Tint);
        var chassis = ChassisGeometry.Preview(BodyRect(body), option.Kind);
        if (option.Kind == PhoneCaseKind.Art && ResolveArt(option.TextureId, fullResolution) is { } thumb)
        {
            Squircle.Fill(drawList, chassis.Body.Min, chassis.Body.Max, chassis.BodyRadius,
                ImGui.GetColorU32(finish.Frame));
            CaseArt.QuadClipped(drawList, thumb, chassis.Body, false, CaseArt.Tint(1f));
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

    private static ImTextureID? ResolveArt(string textureId, bool fullResolution) =>
        fullResolution
            ? PhoneCaseTextures.Skin(textureId) ?? PhoneCaseTextures.Thumb(textureId)
            : PhoneCaseTextures.Thumb(textureId);

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

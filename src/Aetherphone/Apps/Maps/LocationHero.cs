using Aetherphone.Core;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal static class LocationHero
{
    private const float DiscRadius = 40f;
    private const float GlyphSize = 34f;
    private const float RingStep = 26f;
    private const int RingCount = 3;
    private const float TitleGap = 18f;
    private const float LineGap = 6f;
    private const float SideInset = 28f;
    private const float WashStrength = 0.42f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static string GlyphFor(MapLocationKind kind) => kind switch
    {
        MapLocationKind.House => PhoneIcons.HomeFilled,
        MapLocationKind.Ward => PhoneIcons.Home,
        MapLocationKind.Duty => PhoneIcons.Flag,
        MapLocationKind.Zone => PhoneIcons.MapPin,
        MapLocationKind.Offline => PhoneIcons.World,
        _ => PhoneIcons.Compass,
    };

    public static void Draw(ImDrawListPtr drawList, Rect screen, Rect visible, PhoneTheme theme, Vector4 accent,
        in MapLocation location, string note, float scale)
    {
        var rounding = theme.ScreenRounding * scale;
        var top = Palette.Mix(theme.AppBackground, accent, WashStrength);
        Squircle.FillVerticalGradient(drawList, screen.Min, screen.Max, rounding,
            ImGui.GetColorU32(top with { W = 1f }), ImGui.GetColorU32(theme.AppBackground with { W = 1f }));
        var maxWidth = MathF.Max(1f, visible.Width - SideInset * 2f * scale);
        var titleHeight = Typography.MeasureWrappedBlock(location.Title, TextStyles.Title1, maxWidth).Y;
        var subtitleHeight = location.Subtitle.Length > 0
            ? Typography.MeasureWrappedBlock(location.Subtitle, TextStyles.Subheadline, maxWidth).Y + LineGap * scale
            : 0f;
        var noteHeight = note.Length > 0
            ? Typography.MeasureWrappedBlock(note, TextStyles.Footnote, maxWidth).Y + LineGap * scale
            : 0f;
        var disc = DiscRadius * scale;
        var block = disc * 2f + TitleGap * scale + titleHeight + subtitleHeight + noteHeight;
        var blockTop = MathF.Max(visible.Min.Y, visible.Center.Y - block * 0.5f);
        var center = new Vector2(visible.Center.X, blockTop + disc);
        for (var ringIndex = RingCount; ringIndex >= 1; ringIndex--)
        {
            drawList.AddCircleFilled(center, disc + RingStep * ringIndex * scale,
                ImGui.GetColorU32(Palette.WithAlpha(accent, 0.05f * (RingCount + 1 - ringIndex))), 64);
        }

        Squircle.FillCircleVerticalGradient(drawList, center, disc,
            ImGui.GetColorU32(Palette.Lighten(accent, 0.12f) with { W = 1f }),
            ImGui.GetColorU32(Palette.Darken(accent, 0.16f) with { W = 1f }));
        PhoneIcon.Draw(drawList, center, GlyphFor(location.Kind), White, GlyphSize * scale);
        var textTop = center.Y + disc + TitleGap * scale;
        var bottom = Typography.DrawWrappedCentered(drawList, location.Title, TextStyles.Title1, theme.TextStrong,
            new Vector2(visible.Center.X, textTop), maxWidth);
        if (location.Subtitle.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(drawList, location.Subtitle, TextStyles.Subheadline,
                theme.TextMuted, new Vector2(visible.Center.X, bottom + LineGap * scale), maxWidth);
        }

        if (note.Length > 0)
        {
            Typography.DrawWrappedCentered(drawList, note, TextStyles.Footnote, theme.TextMuted,
                new Vector2(visible.Center.X, bottom + LineGap * scale), maxWidth);
        }
    }
}

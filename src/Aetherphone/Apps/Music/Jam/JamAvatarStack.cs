using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Jam;

internal static class JamAvatarStack
{
    private const float OverlapShare = 0.32f;
    private const float RingWidth = 2f;
    private const float MonogramScale = 0.62f;
    private const int Segments = 24;
    private const float OverflowFillAlpha = 0.9f;

    public static float Step(float radius) => radius * 2f * (1f - OverlapShare);

    public static float Width(int count, int maxShown, float radius)
    {
        if (count <= 0)
        {
            return 0f;
        }

        var shown = Math.Min(count, maxShown) + (count > maxShown ? 1 : 0);
        return radius * 2f + Step(radius) * (shown - 1);
    }

    public static float Draw(ImDrawListPtr drawList, Vector2 leftCenter, float radius, ReadOnlySpan<JamMember> members,
        ReadOnlySpan<string> names, int maxShown, string overflowLabel, Vector4 ring, PhoneTheme theme,
        RemoteImageCache images, LodestoneService lodestone, float scale)
    {
        var count = members.Length;
        if (count == 0)
        {
            return 0f;
        }

        var shown = Math.Min(count, maxShown);
        var overflow = count > shown;
        var step = Step(radius);
        var ringRadius = radius + RingWidth * scale;
        var ringColor = ImGui.GetColorU32(ring);
        if (overflow)
        {
            var center = new Vector2(leftCenter.X + radius + step * shown, leftCenter.Y);
            drawList.AddCircleFilled(center, ringRadius, ringColor, Segments);
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, OverflowFillAlpha)), Segments);
            Typography.DrawCentered(drawList, center, overflowLabel, theme.TextStrong, TextStyles.Caption2);
        }

        for (var index = shown - 1; index >= 0; index--)
        {
            var member = members[index];
            var center = new Vector2(leftCenter.X + radius + step * index, leftCenter.Y);
            drawList.AddCircleFilled(center, ringRadius, ringColor, Segments);
            AvatarView.DrawRemote(drawList, center, radius, theme, names[index], string.Empty, member.AvatarUrl,
                images, lodestone, MonogramScale, Segments);
        }

        return Width(count, maxShown, radius);
    }
}

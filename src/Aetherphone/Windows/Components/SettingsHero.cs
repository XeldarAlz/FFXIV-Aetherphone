using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SettingsHero
{
    private const float Height = 82f;
    private const float Padding = 14f;
    private const float AvatarRadius = 27f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static bool Draw(PhoneTheme theme, AethernetSession session)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var radius = Metrics.Radius.Card * scale;
        var fill = hovered ? Palette.Mix(theme.GroupedCard, theme.TextStrong, 0.05f) : theme.GroupedCard;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(drawList, min, max, radius, scale);

        var signedIn = session.IsSignedIn;
        var name = signedIn ? session.CurrentUser?.DisplayName ?? string.Empty : string.Empty;
        var avatarCenter = new Vector2(min.X + Padding * scale + AvatarRadius * scale, (min.Y + max.Y) * 0.5f);
        DrawAvatar(drawList, avatarCenter, AvatarRadius * scale, theme, signedIn, name);

        var textLeft = avatarCenter.X + AvatarRadius * scale + 14f * scale;
        var chevronTip = new Vector2(max.X - 16f * scale, avatarCenter.Y);
        var textWidth = MathF.Max(20f * scale, chevronTip.X - 12f * scale - textLeft);
        var title = signedIn ? name : Loc.T(L.Account.HeroSignInTitle);
        var subtitle = signedIn ? Loc.T(L.Account.HeroSubtitle) : Loc.T(L.Account.HeroSignInSubtitle);
        var titleSize = Typography.Measure(title, 1.25f, FontWeight.SemiBold);
        var subtitleSize = Typography.Measure(subtitle, 0.85f);
        var gap = 3f * scale;
        var blockTop = avatarCenter.Y - (titleSize.Y + gap + subtitleSize.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), Typography.FitText(title, textWidth, 1.25f,
            FontWeight.SemiBold), theme.TextStrong, 1.25f, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(textLeft, blockTop + titleSize.Y + gap),
            Typography.FitText(subtitle, textWidth, 0.85f, FontWeight.Regular), theme.TextMuted, 0.85f,
            FontWeight.Regular);

        DrawChevron(drawList, chevronTip, 6f * scale, 2.2f * scale, theme.TextMuted);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme,
        bool signedIn, string name)
    {
        if (!signedIn || name.Length == 0)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.Mix(theme.GroupedCard, theme.TextMuted,
                0.28f)), 48);
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.User, theme.TextMuted, radius * 0.9f);
            return;
        }

        var baseColor = IconTile.Surface(theme.Accent);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(baseColor), 48);
        drawList.AddCircleFilled(new Vector2(center.X, center.Y - radius * 0.35f), radius * 0.62f,
            ImGui.GetColorU32(Palette.WithAlpha(White, 0.10f)), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(White, 0.16f)), 48, 1f);
        Typography.DrawCentered(drawList, center, Initials.Of(name), White, 1.5f, FontWeight.SemiBold);
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 tip, float size, float thickness, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }
}

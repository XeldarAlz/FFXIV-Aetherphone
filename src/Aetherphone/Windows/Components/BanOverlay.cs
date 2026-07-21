using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class BanOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float PresenceSmoothTime = 0.16f;
    private readonly AethernetSession session;
    private Spring presence;

    public BanOverlay(AethernetSession session)
    {
        this.session = session;
    }

    public bool IsActive => session.IsBanned;

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var active = session.IsBanned;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        presence.Step(active ? 1f : 0f, PresenceSmoothTime, delta);
        if (presence.Value <= 0.01f)
        {
            if (!active)
            {
                presence.SnapTo(0f);
            }

            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##banOverlay", screen.Size, false, OverlayFlags))
        {
            DrawContent(screen, theme, Math.Clamp(presence.Value, 0f, 1f));
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, float reveal)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var alpha = Math.Clamp(reveal * 1.4f, 0f, 1f);
        dl.AddRectFilled(screen.Min, screen.Max,
            ImGui.GetColorU32(new Vector4(0.03f, 0.03f, 0.05f, 0.94f * alpha)));

        var centerX = screen.Center.X;
        var maxWidth = MathF.Min(screen.Size.X - 56f * scale, 320f * scale);

        var iconCenter = new Vector2(centerX, screen.Min.Y + screen.Size.Y * 0.30f);
        var iconRadius = 40f * scale;
        dl.AddCircleFilled(iconCenter, iconRadius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.16f * alpha)), 48);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Ban.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), iconCenter - size * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, alpha)), glyph);
        }

        var y = iconCenter.Y + iconRadius + 28f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenTitle),
            Palette.WithAlpha(theme.TextStrong, alpha), new TextStyle(1.5f, FontWeight.SemiBold), maxWidth);

        var bodyStyle = new TextStyle(1f, FontWeight.Regular);
        y += 14f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenBody),
            Palette.WithAlpha(theme.TextMuted, alpha), bodyStyle, maxWidth);

        var reason = session.BanReason;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            y += 12f * scale;
            y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenReason, reason),
                Palette.WithAlpha(theme.TextStrong, 0.85f * alpha), bodyStyle, maxWidth);
        }

        y += 18f * scale;
        Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenContact),
            Palette.WithAlpha(theme.TextMuted, 0.8f * alpha), new TextStyle(0.9f, FontWeight.Regular), maxWidth);
    }
}

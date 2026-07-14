using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class IncomingCallOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float PresenceSmoothTime = 0.14f;
    private static readonly Vector4 Green = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);
    private readonly CallHub calls;
    private Spring presence;
    private float clock;

    public IncomingCallOverlay(CallHub calls)
    {
        this.calls = calls;
    }

    public bool IsRinging => calls.Snapshot().State == CallState.Ringing;

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var view = calls.Snapshot();
        var ringing = view.State == CallState.Ringing;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        presence.Step(ringing ? 1f : 0f, PresenceSmoothTime, delta);
        if (presence.Value <= 0.01f)
        {
            if (!ringing)
            {
                presence.SnapTo(0f);
            }

            return;
        }

        clock += delta;
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##incomingCall", screen.Size, false, OverlayFlags))
        {
            DrawContent(screen, theme, view, Math.Clamp(presence.Value, 0f, 1f), ringing);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, CallView view, float reveal, bool live)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var alpha = Math.Clamp(reveal * 1.4f, 0f, 1f);
        var rise = (1f - reveal) * 26f * scale;
        dl.AddRectFilled(screen.Min, screen.Max,
            ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.05f, 0.78f * alpha)));
        var centerX = screen.Center.X;
        var caller = view.IncomingFrom?.DisplayName ?? view.PeerLabel;
        var avatarRadius = 50f * scale * (0.9f + 0.1f * reveal);
        var avatarCenter = new Vector2(centerX, screen.Min.Y + 150f * scale + rise);
        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 2.2f);
        dl.AddCircle(avatarCenter, avatarRadius + (6f + 7f * pulse) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(Green, (0.30f + 0.25f * pulse) * alpha)), 64, 2.5f * scale);
        dl.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, alpha)), 64);
        Typography.DrawCentered(dl, avatarCenter, Initial(caller), Ink with { W = alpha }, 2.3f, FontWeight.Regular);
        Typography.DrawCentered(dl, new Vector2(centerX, avatarCenter.Y + avatarRadius + 30f * scale), caller,
            Palette.WithAlpha(theme.TextStrong, alpha), 1.7f, FontWeight.Regular);
        var subtitle = view.OthersCount > 1
            ? string.Concat(AepConstants.Name, " · ", Loc.T(L.Phone.PlusOthers, view.OthersCount - 1))
            : Loc.T(L.Phone.AudioCall);
        Typography.DrawCentered(dl, new Vector2(centerX, avatarCenter.Y + avatarRadius + 56f * scale), subtitle,
            Palette.WithAlpha(theme.TextMuted, alpha), 0.95f, FontWeight.Regular);
        var buttonY = screen.Max.Y - 96f * scale + rise;
        var buttonRadius = 32f * scale;
        if (ActionButton(new Vector2(centerX - 70f * scale, buttonY), buttonRadius, FontAwesomeIcon.PhoneSlash,
                theme.Danger, Loc.T(L.Phone.Decline), theme, alpha, live))
        {
            calls.Decline();
        }

        if (ActionButton(new Vector2(centerX + 70f * scale, buttonY), buttonRadius, FontAwesomeIcon.Phone, Green,
                Loc.T(L.Phone.Accept), theme, alpha, live))
        {
            calls.Accept();
        }
    }

    private static bool ActionButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill, string label,
        PhoneTheme theme, float alpha, bool live)
    {
        var dl = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = live &&
                      ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius),
                          center + new Vector2(radius, radius));
        var color = hovered ? Palette.Mix(fill, Ink, 0.14f) : fill;
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha)), 40);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, Ink with { W = alpha }))
            {
                Typography.Plain(glyph);
            }
        }

        Typography.DrawCentered(dl, new Vector2(center.X, center.Y + radius + 16f * scale), label,
            Palette.WithAlpha(theme.TextMuted, alpha), 0.85f, FontWeight.Regular);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string Initial(string value) => value.Length > 0 ? value.Substring(0, 1).ToUpperInvariant() : "?";
}

using System.Numerics;
using Aetherphone.Core;
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

    private static readonly Vector4 Green = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);
    private readonly CallHub calls;
    private float clock;

    public IncomingCallOverlay(CallHub calls)
    {
        this.calls = calls;
    }

    public bool IsRinging => calls.Snapshot().State == CallState.Ringing;

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var view = calls.Snapshot();
        if (view.State != CallState.Ringing)
        {
            return;
        }

        clock += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##incomingCall", screen.Size, false, OverlayFlags))
        {
            DrawContent(screen, theme, view);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, CallView view)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.05f, 0.78f)));
        var centerX = screen.Center.X;
        var caller = view.IncomingFrom?.DisplayName ?? view.PeerLabel;
        var avatarRadius = 50f * scale;
        var avatarCenter = new Vector2(centerX, screen.Min.Y + 150f * scale);
        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 2.2f);
        dl.AddCircle(avatarCenter, avatarRadius + (6f + 7f * pulse) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(Green, 0.30f + 0.25f * pulse)), 64, 2.5f * scale);
        dl.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(theme.Accent), 64);
        Typography.DrawCentered(avatarCenter, Initial(caller), Ink, 2.3f);
        Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 30f * scale), caller,
            theme.TextStrong, 1.7f);
        var subtitle = view.OthersCount > 1
            ? $"Aetherphone · +{view.OthersCount - 1} others"
            : "Aetherphone audio call";
        Typography.DrawCentered(new Vector2(centerX, avatarCenter.Y + avatarRadius + 56f * scale), subtitle,
            theme.TextMuted, 0.95f);
        var buttonY = screen.Max.Y - 96f * scale;
        var buttonRadius = 32f * scale;
        if (ActionButton(new Vector2(centerX - 70f * scale, buttonY), buttonRadius, FontAwesomeIcon.PhoneSlash,
                theme.Danger, "Decline", theme))
        {
            calls.Decline();
        }

        if (ActionButton(new Vector2(centerX + 70f * scale, buttonY), buttonRadius, FontAwesomeIcon.Phone, Green,
                "Accept", theme))
        {
            calls.Accept();
        }
    }

    private static bool ActionButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill, string label,
        PhoneTheme theme)
    {
        var dl = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var color = hovered ? Palette.Mix(fill, Ink, 0.14f) : fill;
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 40);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, Ink))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        Typography.DrawCentered(new Vector2(center.X, center.Y + radius + 16f * scale), label, theme.TextMuted, 0.85f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string Initial(string value) => value.Length > 0 ? value.Substring(0, 1).ToUpperInvariant() : "?";
}

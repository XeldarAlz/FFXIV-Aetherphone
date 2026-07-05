using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class CallIsland
{
    private const ImGuiWindowFlags IslandFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private static readonly Vector4 Green = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);
    private const float ExpandedHeight = 104f;
    private const float ExpandedHalfWidth = 138f;
    private const float CompactPadX = 22f;
    private const float CompactPadY = 5f;
    private readonly CallHub calls;
    private float expand;
    private float clock;

    public CallIsland(CallHub calls)
    {
        this.calls = calls;
    }

    private static bool Visible(CallView view) =>
        view.State is CallState.Dialing or CallState.Connecting or CallState.Active;

    public bool CapturesPointer(Rect screen)
    {
        var view = calls.Snapshot();
        if (!Visible(view))
        {
            return false;
        }

        var bounds = PreviewBounds(screen);
        return ImGui.IsMouseHoveringRect(bounds.Min, bounds.Max);
    }

    public void Draw(Rect screen, PhoneTheme theme, INavigator navigation)
    {
        var view = calls.Snapshot();
        if (!Visible(view))
        {
            expand = 0f;
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##callIsland", screen.Size, false, IslandFlags))
        {
            DrawContent(screen, theme, navigation, view);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, INavigator navigation, CallView view)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        clock += delta;
        var rest = StatusBar.BaseIsland(screen);
        var compact = CompactBounds(rest, scale);
        var expanded = ExpandedBounds(screen, rest, scale);
        var currentBounds = LerpRect(compact, expanded, Ease(expand));
        var hovered = ImGui.IsMouseHoveringRect(currentBounds.Min, currentBounds.Max);
        var target = hovered ? 1f : 0f;
        expand = Math.Clamp(expand + (target - expand) * MathF.Min(1f, delta * 16f), 0f, 1f);
        var eased = Ease(expand);
        var bounds = LerpRect(compact, expanded, eased);
        var collapsedAlpha = 1f - eased;
        var dl = ImGui.GetWindowDrawList();
        var rounding = float.Lerp(compact.Height * 0.5f, 26f * scale, eased);
        dl.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(theme.BezelOuter), rounding);
        dl.AddRect(bounds.Min, bounds.Max, ImGui.GetColorU32(Palette.WithAlpha(Green, 0.20f + 0.5f * eased)), rounding,
            ImDrawFlags.RoundCornersAll, 1.5f * scale);
        DrawCompact(dl, compact, scale, view, collapsedAlpha);
        var consumed = DrawExpanded(dl, bounds, scale, theme, view, eased);
        if (consumed || !hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (eased < 0.5f && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.Open("phone");
        }
    }

    private void DrawCompact(ImDrawListPtr dl, Rect compact, float scale, CallView view, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 3f);
        var dotCenter = new Vector2(compact.Min.X + 13f * scale, compact.Center.Y);
        dl.AddCircleFilled(dotCenter, (3.4f + 1.2f * pulse) * scale, ImGui.GetColorU32(Palette.WithAlpha(Green, alpha)),
            16);
        var label = CompactLabel(view);
        var size = Typography.Measure(label, 0.82f);
        Typography.Draw(new Vector2(compact.Max.X - size.X - 11f * scale, compact.Center.Y - size.Y * 0.5f), label,
            Palette.WithAlpha(Ink, alpha), 0.82f);
    }

    private bool DrawExpanded(ImDrawListPtr dl, Rect bounds, float scale, PhoneTheme theme, CallView view, float alpha)
    {
        if (alpha <= 0.05f)
        {
            return false;
        }

        var centerX = bounds.Center.X;
        var top = bounds.Min.Y;
        Typography.DrawCentered(new Vector2(centerX, top + 20f * scale), Truncate(view.PeerLabel, 18),
            Palette.WithAlpha(theme.TextStrong, alpha), 1.05f);
        Typography.DrawCentered(new Vector2(centerX, top + 42f * scale), CompactLabel(view),
            Palette.WithAlpha(Green, 0.9f * alpha), 0.82f);
        var active = alpha > 0.6f;
        var buttonY = top + 74f * scale;
        var consumed = false;
        var muteFill = view.Muted ? Green : Palette.WithAlpha(theme.TextStrong, 0.18f);
        if (Button(new Vector2(centerX - 34f * scale, buttonY), 17f * scale,
                view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone, muteFill, theme.TextStrong,
                alpha, active))
        {
            calls.ToggleMute();
            consumed = true;
        }

        if (Button(new Vector2(centerX + 34f * scale, buttonY), 17f * scale, FontAwesomeIcon.PhoneSlash, theme.Danger,
                Ink, alpha, active))
        {
            calls.Hangup();
            consumed = true;
        }

        return consumed;
    }

    private static bool Button(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill, Vector4 ink,
        float alpha, bool active)
    {
        var dl = ImGui.GetWindowDrawList();
        var hovered = active &&
                      ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius),
                          center + new Vector2(radius, radius));
        var color = hovered ? Palette.Mix(fill, Ink, 0.14f) : fill;
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha * color.W)), 28);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(ink, alpha)))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string CompactLabel(CallView view)
    {
        return view.State switch
        {
            CallState.Dialing => "Calling…",
            CallState.Connecting => "Connecting…",
            CallState.Active => CallFormat.Duration(view.Seconds),
            _ => string.Empty,
        };
    }

    private Rect PreviewBounds(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rest = StatusBar.BaseIsland(screen);
        return LerpRect(CompactBounds(rest, scale), ExpandedBounds(screen, rest, scale), Ease(expand));
    }

    private static Rect CompactBounds(Rect rest, float scale)
    {
        return new Rect(rest.Min - new Vector2(CompactPadX * scale, CompactPadY * scale),
            rest.Max + new Vector2(CompactPadX * scale, CompactPadY * scale));
    }

    private static Rect ExpandedBounds(Rect screen, Rect rest, float scale)
    {
        var halfWidth = MathF.Min(screen.Width * 0.5f - 14f * scale, ExpandedHalfWidth * scale);
        var centerX = screen.Center.X;
        var top = rest.Min.Y - 2f * scale;
        return new Rect(new Vector2(centerX - halfWidth, top),
            new Vector2(centerX + halfWidth, top + ExpandedHeight * scale));
    }

    private static Rect LerpRect(Rect from, Rect to, float t)
    {
        return new Rect(Vector2.Lerp(from.Min, to.Min, t), Vector2.Lerp(from.Max, to.Max, t));
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }
}

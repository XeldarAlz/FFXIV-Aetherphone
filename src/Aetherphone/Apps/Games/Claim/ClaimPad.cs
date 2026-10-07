using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Claim;

internal readonly struct ClaimPadInput
{
    public readonly ClaimMove Move;
    public readonly bool FastTapped;
    public readonly bool SlowTapped;

    public ClaimPadInput(ClaimMove move, bool fastTapped, bool slowTapped)
    {
        Move = move;
        FastTapped = fastTapped;
        SlowTapped = slowTapped;
    }
}

internal sealed class ClaimPad
{
    private const string SurfaceId = "claim.pad";
    private const float KeySize = 44f;
    private const float KeyGap = 6f;
    private const float ButtonRadius = 27f;
    private const float ButtonSpread = 34f;
    private const float DeadZone = 9f;
    private const float PadShare = 0.3f;
    private const float ButtonShare = 0.72f;

    private bool steering;

    public ClaimPadInput Draw(ImDrawListPtr drawList, Rect band, Vector4 accent, Vector4 slowColor, PhoneTheme theme,
        bool fastOn, bool slowOn, bool interactive, string fastLabel, string slowLabel)
    {
        var scale = UiScale.Current;
        var gap = KeyGap * scale;
        var key = MathF.Min(KeySize * scale, (band.Height - gap * 4f) / 3f);
        var padCenter = new Vector2(band.Min.X + band.Width * PadShare, band.Center.Y);
        var reach = key * 1.5f + gap;
        var padRect = new Rect(padCenter - new Vector2(reach, reach), padCenter + new Vector2(reach, reach));
        var move = interactive ? Steer(padRect, padCenter, scale) : ClaimMove.None;
        if (!interactive)
        {
            steering = false;
        }

        DrawKey(drawList, padCenter + new Vector2(0f, -(key + gap)), key, FontAwesomeIcon.ChevronUp, move == ClaimMove.Up,
            accent, theme, scale);
        DrawKey(drawList, padCenter + new Vector2(0f, key + gap), key, FontAwesomeIcon.ChevronDown,
            move == ClaimMove.Down, accent, theme, scale);
        DrawKey(drawList, padCenter + new Vector2(-(key + gap), 0f), key, FontAwesomeIcon.ChevronLeft,
            move == ClaimMove.Left, accent, theme, scale);
        DrawKey(drawList, padCenter + new Vector2(key + gap, 0f), key, FontAwesomeIcon.ChevronRight,
            move == ClaimMove.Right, accent, theme, scale);
        var radius = MathF.Min(ButtonRadius * scale, band.Height * 0.3f);
        var spread = ButtonSpread * scale;
        var buttonCenter = new Vector2(band.Min.X + band.Width * ButtonShare, band.Center.Y);
        var fastCenter = buttonCenter + new Vector2(spread, -spread * 0.45f);
        var slowCenter = buttonCenter + new Vector2(-spread, spread * 0.45f);
        var fastTapped = DrawToggle(drawList, fastCenter, radius, fastLabel, accent, fastOn, interactive, theme, scale);
        var slowTapped = DrawToggle(drawList, slowCenter, radius, slowLabel, slowColor, slowOn, interactive, theme,
            scale);
        return new ClaimPadInput(move, fastTapped, slowTapped);
    }

    private ClaimMove Steer(Rect padRect, Vector2 padCenter, float scale)
    {
        PressSurface.Claim(SurfaceId, padRect, out var activated);
        if (activated)
        {
            steering = true;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            steering = false;
        }

        if (!steering)
        {
            return ClaimMove.None;
        }

        var offset = ImGui.GetMousePos() - padCenter;
        if (offset.LengthSquared() < DeadZone * DeadZone * scale * scale)
        {
            return ClaimMove.None;
        }

        if (MathF.Abs(offset.X) > MathF.Abs(offset.Y))
        {
            return offset.X > 0f ? ClaimMove.Right : ClaimMove.Left;
        }

        return offset.Y > 0f ? ClaimMove.Down : ClaimMove.Up;
    }

    private static void DrawKey(ImDrawListPtr drawList, Vector2 center, float size, FontAwesomeIcon icon, bool held,
        Vector4 accent, PhoneTheme theme, float scale)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var min = center - half;
        var max = center + half;
        var radius = size * 0.28f;
        Material.Frosted(drawList, min, max, radius, scale, held ? 1f : 0.85f);
        if (held)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.32f }));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.9f }), 1.5f * scale);
        }

        ProgressRing.CenterIcon(drawList, center, icon, held ? accent : theme.TextStrong, size * 0.36f);
    }

    private static bool DrawToggle(ImDrawListPtr drawList, Vector2 center, float radius, string label, Vector4 color,
        bool on, bool interactive, PhoneTheme theme, float scale)
    {
        var corner = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - corner, center + corner) &&
                      (ImGui.GetMousePos() - center).LengthSquared() <= radius * radius;
        if (on)
        {
            ProgressRing.Glow(center, radius * 1.35f, color, 0.8f);
        }

        Material.Frosted(drawList, center - corner, center + corner, radius, scale, hovered ? 1f : 0.85f);
        drawList.AddCircleFilled(center, radius * 0.92f, ImGui.GetColorU32(color with { W = on ? 0.62f : 0.14f }), 32);
        drawList.AddCircle(center, radius * 0.92f, ImGui.GetColorU32(color with { W = on ? 1f : 0.55f }), 32,
            (on ? 2f : 1.3f) * scale);
        var ink = on ? GamePalette.InkOn(color) : theme.TextStrong;
        var textScale = TextStyles.Footnote.Scale * MathF.Min(1f, radius * 1.5f /
            MathF.Max(1f, Typography.Measure(label, TextStyles.Footnote.Scale, FontWeight.Bold).X));
        Typography.DrawCentered(drawList, center, label, ink, textScale, FontWeight.Bold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return interactive && UiInteract.HoverClickCircle(center, radius);
    }
}

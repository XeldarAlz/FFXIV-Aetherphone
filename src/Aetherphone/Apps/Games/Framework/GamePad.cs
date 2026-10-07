using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum PadDirection : byte
{
    None,
    Up,
    Down,
    Left,
    Right,
}

internal readonly struct ShooterPadInput
{
    public readonly bool Left;
    public readonly bool Right;
    public readonly bool Fire;

    public ShooterPadInput(bool left, bool right, bool fire)
    {
        Left = left;
        Right = right;
        Fire = fire;
    }
}

internal static class GamePad
{
    public const float KeySize = 50f;
    public const float Gap = 8f;

    public static float DPadHeight(float scale) => (KeySize * 3f + Gap * 2f) * scale;

    public static float ShooterHeight(float scale) => (KeySize + Gap * 2f) * scale;

    public static PadDirection DPad(Rect area, Vector4 accent, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var gap = Gap * scale;
        var key = MathF.Min(KeySize * scale, MathF.Min((area.Height - gap * 2f) / 3f, (area.Width - gap * 2f) / 3f));
        var half = key * 0.5f;
        var center = area.Center;
        var upMin = new Vector2(center.X - half, center.Y - half - key - gap);
        var leftMin = new Vector2(center.X - half - key - gap, center.Y - half);
        var rightMin = new Vector2(center.X + half + gap, center.Y - half);
        var downMin = new Vector2(center.X - half, center.Y + half + gap);
        var size = new Vector2(key, key);
        var pressed = PadDirection.None;
        if (Key(upMin, upMin + size, "W", accent, theme, scale, out _))
        {
            pressed = PadDirection.Up;
        }

        if (Key(leftMin, leftMin + size, "A", accent, theme, scale, out _))
        {
            pressed = PadDirection.Left;
        }

        if (Key(rightMin, rightMin + size, "D", accent, theme, scale, out _))
        {
            pressed = PadDirection.Right;
        }

        if (Key(downMin, downMin + size, "S", accent, theme, scale, out _))
        {
            pressed = PadDirection.Down;
        }

        return pressed;
    }

    public static ShooterPadInput Shooter(Rect area, Vector4 accent, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var gap = Gap * scale;
        var key = MathF.Min(KeySize * scale, MathF.Min(area.Height - gap * 2f, (area.Width - gap * 4f) / 3f));
        var wide = key * 1.35f;
        var totalWidth = wide * 2f + key + gap * 2f;
        var left = area.Center.X - totalWidth * 0.5f;
        var top = area.Center.Y - key * 0.5f;
        var leftMin = new Vector2(left, top);
        var fireMin = new Vector2(left + wide + gap, top);
        var rightMin = new Vector2(left + wide + gap + key + gap, top);
        var wideSize = new Vector2(wide, key);
        var keySize = new Vector2(key, key);
        Key(leftMin, leftMin + wideSize, "A", accent, theme, scale, out var leftHeld);
        var fire = Key(fireMin, fireMin + keySize, "W", accent, theme, scale, out _);
        Key(rightMin, rightMin + wideSize, "D", accent, theme, scale, out var rightHeld);
        return new ShooterPadInput(leftHeld, rightHeld, fire);
    }

    public static ShooterPadInput Walker(Rect area, Vector4 accent, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var gap = Gap * scale;
        var key = MathF.Min(KeySize * scale, MathF.Min(area.Height - gap * 2f, (area.Width - gap * 3f) / 2.7f));
        var wide = key * 1.35f;
        var left = area.Center.X - (wide * 2f + gap) * 0.5f;
        var top = area.Center.Y - key * 0.5f;
        var leftMin = new Vector2(left, top);
        var rightMin = new Vector2(left + wide + gap, top);
        var size = new Vector2(wide, key);
        Key(leftMin, leftMin + size, "A", accent, theme, scale, out var leftHeld);
        Key(rightMin, rightMin + size, "D", accent, theme, scale, out var rightHeld);
        return new ShooterPadInput(leftHeld, rightHeld, false);
    }

    private static bool Key(Vector2 min, Vector2 max, string glyph, Vector4 accent, PhoneTheme theme, float scale,
        out bool held)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var radius = (max.Y - min.Y) * 0.28f;
        Material.Frosted(drawList, min, max, radius, scale, held ? 1f : 0.85f);
        if (held)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.32f }));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.9f }), 1.5f * scale);
        }
        else if (hovered)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.45f }), 1f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, (min + max) * 0.5f, glyph, held ? accent : StageInks.Strong,
            TextStyles.Title3.Scale, TextStyles.Title3.Weight);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}

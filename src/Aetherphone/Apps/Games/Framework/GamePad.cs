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
    public readonly bool FireHeld;

    public ShooterPadInput(bool left, bool right, bool fire, bool fireHeld = false)
    {
        Left = left;
        Right = right;
        Fire = fire;
        FireHeld = fireHeld;
    }
}

internal static class GamePad
{
    public const float KeySize = 50f;
    public const float Gap = 8f;
    private const string HeldPadSurfaceId = "stage.heldpad";
    private const float KeyRadiusFraction = 0.28f;
    private const float HeldDeadZone = 0.5f;

    private static HeldPadState heldPad;

    private readonly struct CrossKeys
    {
        public readonly Vector2 Center;
        public readonly float Key;
        public readonly Rect Up;
        public readonly Rect Left;
        public readonly Rect Right;
        public readonly Rect Down;

        public CrossKeys(Rect area, float scale)
        {
            var gap = Gap * scale;
            Key = MathF.Min(KeySize * scale, MathF.Min((area.Height - gap * 2f) / 3f, (area.Width - gap * 2f) / 3f));
            Center = area.Center;
            var half = Key * 0.5f;
            var size = new Vector2(Key, Key);
            var upMin = new Vector2(Center.X - half, Center.Y - half - Key - gap);
            var leftMin = new Vector2(Center.X - half - Key - gap, Center.Y - half);
            var rightMin = new Vector2(Center.X + half + gap, Center.Y - half);
            var downMin = new Vector2(Center.X - half, Center.Y + half + gap);
            Up = new Rect(upMin, upMin + size);
            Left = new Rect(leftMin, leftMin + size);
            Right = new Rect(rightMin, rightMin + size);
            Down = new Rect(downMin, downMin + size);
        }
    }

    public static float DPadHeight(float scale) => (KeySize * 3f + Gap * 2f) * scale;

    public static float ShooterHeight(float scale) => (KeySize + Gap * 2f) * scale;

    public static Vector4 GlyphInk(bool held, Vector4 accent) => held ? accent : StageInks.Strong;

    public static PadDirection DPad(Rect area, Vector4 accent, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var cross = new CrossKeys(area, scale);
        var pressed = PadDirection.None;
        if (HoldButton(cross.Up, "W", accent, out _))
        {
            pressed = PadDirection.Up;
        }

        if (HoldButton(cross.Left, "A", accent, out _))
        {
            pressed = PadDirection.Left;
        }

        if (HoldButton(cross.Right, "D", accent, out _))
        {
            pressed = PadDirection.Right;
        }

        if (HoldButton(cross.Down, "S", accent, out _))
        {
            pressed = PadDirection.Down;
        }

        return pressed;
    }

    public static PadDirection HeldDPad(Rect area, Vector4 accent, PhoneTheme theme, out PadDirection held,
        bool enabled = true)
    {
        var scale = UiScale.Current;
        var cross = new CrossKeys(area, scale);
        var pressed = PadDirection.None;
        if (enabled)
        {
            PressSurface.Claim(HeldPadSurfaceId, area, out var activated);
            pressed = heldPad.Update(ImGui.GetMousePos(), ImGui.IsMouseDown(ImGuiMouseButton.Left), activated,
                cross.Center, cross.Key * HeldDeadZone);
        }
        else
        {
            heldPad.Release();
        }

        held = heldPad.Held;
        var drawList = ImGui.GetWindowDrawList();
        HeldKey(drawList, cross.Up, "W", held == PadDirection.Up, accent, scale);
        HeldKey(drawList, cross.Left, "A", held == PadDirection.Left, accent, scale);
        HeldKey(drawList, cross.Right, "D", held == PadDirection.Right, accent, scale);
        HeldKey(drawList, cross.Down, "S", held == PadDirection.Down, accent, scale);
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
        HoldButton(new Rect(leftMin, leftMin + wideSize), "A", accent, out var leftHeld);
        var fire = HoldButton(new Rect(fireMin, fireMin + keySize), "W", accent, out var fireHeld);
        HoldButton(new Rect(rightMin, rightMin + wideSize), "D", accent, out var rightHeld);
        return new ShooterPadInput(leftHeld, rightHeld, fire, fireHeld);
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
        HoldButton(new Rect(leftMin, leftMin + size), "A", accent, out var leftHeld);
        HoldButton(new Rect(rightMin, rightMin + size), "D", accent, out var rightHeld);
        return new ShooterPadInput(leftHeld, rightHeld, false);
    }

    public static bool HoldButton(Rect key, string glyph, Vector4 accent, out bool held)
    {
        var pressed = HoldButton(key, accent, out held);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), key.Center, glyph, GlyphInk(held, accent),
            TextStyles.Title3.Scale, TextStyles.Title3.Weight);
        return pressed;
    }

    public static bool HoldButton(Rect key, Vector4 accent, out bool held)
    {
        var hovered = UiInteract.Hover(key.Min, key.Max);
        held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var scale = UiScale.Current;
        KeyFace(ImGui.GetWindowDrawList(), key, key.Height * KeyRadiusFraction, held, hovered, accent, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void KeyFace(ImDrawListPtr drawList, Rect key, float radius, bool held, bool hovered,
        Vector4 accent, float scale)
    {
        Material.Frosted(drawList, key.Min, key.Max, radius, scale, held ? 1f : 0.85f);
        if (held)
        {
            Squircle.Fill(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(accent with { W = 0.32f }));
            Squircle.Stroke(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(accent with { W = 0.9f }),
                1.5f * scale);
            return;
        }

        if (hovered)
        {
            Squircle.Stroke(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(accent with { W = 0.45f }),
                1f * scale);
        }
    }

    private static void HeldKey(ImDrawListPtr drawList, Rect key, string glyph, bool held, Vector4 accent, float scale)
    {
        KeyFace(drawList, key, key.Height * KeyRadiusFraction, held, false, accent, scale);
        Typography.DrawCentered(drawList, key.Center, glyph, GlyphInk(held, accent), TextStyles.Title3.Scale,
            TextStyles.Title3.Weight);
    }
}

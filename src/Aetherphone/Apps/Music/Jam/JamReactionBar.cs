using Aetherphone.Core;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Jam;

internal static class JamReactionBar
{
    public const float Height = 40f;

    private const float GlyphSize = 22f;
    private const float HoverGrow = 1.14f;
    private const float HoverFillAlpha = 0.10f;
    private const float LifetimeSeconds = 2.6f;
    private const float RiseHeight = 150f;
    private const float RisingSize = 30f;
    private const float FadeStart = 0.62f;
    private const float PopShare = 8f;
    private const float PopFloor = 0.7f;
    private const float SwayShare = 0.35f;
    private const float SwayTurns = 1.25f;
    private const float MillisecondsPerSecond = 1000f;
    private const uint LaneMultiplier = 2654435761u;
    private const float LaneRange = 65535f;

    private static readonly string[] Tokens =
    [
        ":heart:", ":fire:", ":clap:", ":joy:", ":notes:", ":dancer:", ":heart_eyes:", ":raised_hands:",
    ];

    private static readonly string[] PressIds =
    [
        "music.jam.react.0", "music.jam.react.1", "music.jam.react.2", "music.jam.react.3",
        "music.jam.react.4", "music.jam.react.5", "music.jam.react.6", "music.jam.react.7",
    ];

    public static int Draw(ImDrawListPtr drawList, Rect row, GlassTone tone, Vector4 ink, float scale,
        bool interactive = true)
    {
        var radius = row.Height * 0.5f;
        Material.LiquidGlass(drawList, row.Min, row.Max, radius, scale, tone, 0f);
        var slot = row.Width / JamReactions.MaxKinds;
        var hitRadius = MathF.Min(radius, slot * 0.5f);
        var tapped = -1;
        for (var kind = 0; kind < JamReactions.MaxKinds; kind++)
        {
            var center = SlotCenter(row, kind);
            var extent = new Vector2(hitRadius, hitRadius);
            var hovered = interactive && UiInteract.Hover(center - extent, center + extent);
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var grow = PressFx.Scale(PressIds[kind], pressed, PressFx.ControlPressedScale) * (hovered ? HoverGrow : 1f);
            if (hovered)
            {
                drawList.AddCircleFilled(center, hitRadius,
                    ImGui.GetColorU32(Palette.WithAlpha(ink, HoverFillAlpha)), 24);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            ReactionArt.Draw(drawList, Tokens[kind], center, GlyphSize * scale * grow, 1f, 1f);
            if (UiInteract.Click(center - extent, center + extent, hovered))
            {
                tapped = kind;
            }
        }

        return tapped;
    }

    public static void DrawRising(ImDrawListPtr drawList, Rect row, JamReactions reactions, float scale)
    {
        var now = Environment.TickCount64;
        var count = reactions.Count;
        for (var index = 0; index < count; index++)
        {
            ref readonly var reaction = ref reactions.NewestAt(index);
            var age = (now - reaction.AtTicks) / MillisecondsPerSecond;
            if (age >= LifetimeSeconds)
            {
                break;
            }

            if (age < 0f || reaction.Kind < 0 || reaction.Kind >= JamReactions.MaxKinds)
            {
                continue;
            }

            var progress = age / LifetimeSeconds;
            var rise = 1f - (1f - progress) * (1f - progress);
            var pop = PopFloor + (1f - PopFloor) * MathF.Min(1f, progress * PopShare);
            var size = RisingSize * scale * pop;
            var lane = Lane(reaction.AtTicks, reaction.Kind);
            var sway = MathF.Sin((progress * SwayTurns + lane) * MathF.Tau) * size * SwayShare;
            var origin = SlotCenter(row, reaction.Kind);
            var center = new Vector2(origin.X + sway, origin.Y - RiseHeight * scale * rise);
            var alpha = progress < FadeStart ? 1f : 1f - (progress - FadeStart) / (1f - FadeStart);
            ReactionArt.Draw(drawList, Tokens[reaction.Kind], center, size, alpha, 1f);
        }
    }

    private static Vector2 SlotCenter(Rect row, int kind)
    {
        var slot = row.Width / JamReactions.MaxKinds;
        return new Vector2(row.Min.X + slot * (kind + 0.5f), row.Center.Y);
    }

    private static float Lane(long atTicks, int kind)
    {
        var hash = unchecked((uint)atTicks * LaneMultiplier + (uint)kind);
        return ((hash >> 8) & 0xFFFFu) / LaneRange;
    }
}

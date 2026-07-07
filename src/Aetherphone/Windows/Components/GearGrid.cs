using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Character;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal static class GearGrid
{
    private const int Columns = 6;
    private static readonly Dictionary<int, Spring> Scales = new();

    public static void Draw(IReadOnlyList<EquippedItem> gear, ITextureProvider textures, PhoneTheme theme)
    {
        if (gear.Count == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var gap = 8f * scale;
        var tile = (width - gap * (Columns - 1)) / Columns;
        var rounding = 10f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        for (var index = 0; index < gear.Count; index++)
        {
            var column = index % Columns;
            var rowIndex = index / Columns;
            var min = new Vector2(origin.X + column * (tile + gap), origin.Y + rowIndex * (tile + gap));
            var max = min + new Vector2(tile, tile);
            var hovered = ImGui.IsMouseHoveringRect(min, max);
            if (!Scales.TryGetValue(index, out var spring))
            {
                spring = new Spring(1f);
            }

            var grow = spring.Step(hovered ? 1.07f : 1f, 0.09f, deltaSeconds);
            Scales[index] = spring;
            var center = (min + max) * 0.5f;
            var half = (max - min) * 0.5f * grow;
            var tileMin = center - half;
            var tileMax = center + half;
            if (hovered)
            {
                Elevation.Card(dl, tileMin, tileMax, rounding, scale, 0.85f);
            }

            Squircle.Fill(dl, tileMin, tileMax, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            var texture = textures.GetFromGameIcon(new GameIconLookup(gear[index].IconId)).GetWrapOrEmpty();
            dl.AddImageRounded(texture.Handle, tileMin, tileMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding);
            Material.EdgeSquircle(dl, tileMin, tileMax, rounding, scale);
            HoverTooltip.Show(new Rect(tileMin, tileMax), gear[index].Name);
        }

        var rows = (gear.Count + Columns - 1) / Columns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * tile + (rows - 1) * gap));
    }
}

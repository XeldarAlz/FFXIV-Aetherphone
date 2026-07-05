using System.Numerics;
using Aetherphone.Core.Character;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class CharacterHeader
{
    public static void Draw(LocalCharacter character, PhoneTheme theme, LodestoneService lodestone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var dl = ImGui.GetWindowDrawList();
        var radius = 36f * scale;
        var height = radius * 2f + 112f * scale;
        var cardMin = origin;
        var cardMax = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 22f * scale;
        Elevation.Card(dl, cardMin, cardMax, rounding, scale, 0.7f);
        Squircle.Fill(dl, cardMin, cardMax, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.TopGlow(dl, cardMin, cardMax, rounding, theme.Accent, 0.82f, 0.15f);
        Material.EdgeSquircle(dl, cardMin, cardMax, rounding, scale);
        var avatarCenter = new Vector2(centerX, cardMin.Y + 20f * scale + radius);
        ProgressRing.Glow(avatarCenter, radius, theme.Accent, 0.5f);
        AvatarView.Draw(dl, avatarCenter, radius, theme.Accent, Initials.Of(character.Name), 2.0f,
            lodestone.Avatar(character.Name, character.WorldName), 64);
        var nameY = avatarCenter.Y + radius + 18f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), character.Name, theme.TextStrong, TextStyles.Title2);
        var worldLine = character.DataCenter.Length > 0
            ? $"{character.WorldName} [{character.DataCenter}]"
            : character.WorldName;
        Typography.DrawCentered(new Vector2(centerX, nameY + 27f * scale), worldLine, theme.TextMuted,
            TextStyles.Subheadline);
        Typography.DrawCentered(new Vector2(centerX, nameY + 49f * scale), JobLine(character), theme.TextMuted,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static string JobLine(LocalCharacter character)
    {
        var job = character.Job.Length > 0 ? $"Lv {character.Level} · {character.Job}" : $"Lv {character.Level}";
        return character.AverageItemLevel > 0 ? $"{job} · i{character.AverageItemLevel}" : job;
    }
}

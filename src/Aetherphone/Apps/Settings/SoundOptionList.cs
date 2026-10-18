using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal static class SoundOptionList
{
    private const float RemoveRadius = 10f;

    public static void Draw(PhoneTheme theme, SoundService sound, SoundKind kind, string? currentToken,
        bool includeDefault, Action<string?> onSelect, Action<string>? onRemove = null)
    {
        var options = sound.Options(kind);
        var card = GroupCard.Begin(theme, options.Count + (includeDefault ? 1 : 0));
        if (includeDefault &&
            SettingsRow.Selectable(card.NextRow(), Loc.T(L.Settings.SoundDefault), currentToken is null, theme))
        {
            onSelect(null);
        }

        var effective = includeDefault && currentToken is null ? null : sound.Resolve(kind, currentToken);
        for (var index = 0; index < options.Count; index++)
        {
            var token = options[index];
            var row = card.NextRow();
            var selected = string.Equals(effective, token, StringComparison.Ordinal);
            var label = sound.Label(kind, token);
            if (onRemove is null || !sound.CanRemove(kind, token))
            {
                if (SettingsRow.Selectable(row, label, selected, theme))
                {
                    onSelect(token);
                }

                continue;
            }

            if (DrawRemovable(row, label, selected, theme, out var remove))
            {
                onSelect(token);
            }

            if (remove)
            {
                onRemove(token);
            }
        }

        card.End();
    }

    private static bool DrawRemovable(Rect row, string label, bool selected, PhoneTheme theme, out bool remove)
    {
        var scale = UiScale.Current;
        var radius = RemoveRadius * scale;
        var gap = Metrics.Space.Sm * scale;
        var center = new Vector2(row.Max.X - SettingsRow.CheckWidth * scale - gap - radius, row.Center.Y);
        var extent = new Vector2(radius, radius);
        var buttonHovered = UiInteract.Hover(center - extent, center + extent);
        var picked = SettingsRow.Selectable(row, label, selected, theme, null, radius * 2f + gap * 2f,
            !buttonHovered);
        remove = SettingsReorder.Button(center, radius, FontAwesomeIcon.TrashAlt, theme, true);
        return picked;
    }
}

using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class SoundOptionList
{
    public static void Draw(PhoneTheme theme, SoundService sound, SoundKind kind, string? currentToken,
        bool includeDefault, Action<string?> onSelect)
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
            var selected = string.Equals(effective, token, StringComparison.Ordinal);
            if (SettingsRow.Selectable(card.NextRow(), sound.Label(kind, token), selected, theme))
            {
                onSelect(token);
            }
        }

        card.End();
    }
}

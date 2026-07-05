using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class SoundOptionList
{
    public static void Draw(PhoneTheme theme, SoundService sound, string? currentToken, bool includeDefault,
        Action<string?> onSelect)
    {
        var options = sound.Options;
        var card = GroupCard.Begin(theme, options.Count + (includeDefault ? 1 : 0));
        if (includeDefault &&
            SettingsRow.Selectable(card.NextRow(), Loc.T(L.Settings.SoundDefault), currentToken is null, theme))
        {
            onSelect(null);
        }

        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var selected = string.Equals(currentToken, option.Token, StringComparison.Ordinal);
            if (SettingsRow.Selectable(card.NextRow(), sound.Label(option.Token), selected, theme))
            {
                onSelect(option.Token);
            }
        }

        card.End();
    }
}

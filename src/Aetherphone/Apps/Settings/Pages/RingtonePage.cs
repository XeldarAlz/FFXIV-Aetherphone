using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class RingtonePage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Ringtone);
    public string Summary => CurrentName();
    public string Glyph => "R";
    public Vector4 Tint => new(0.95f, 0.40f, 0.65f, 1f);
    private readonly Configuration configuration;
    private readonly IRingtone ringtone;

    public RingtonePage(Configuration configuration, IRingtone ringtone)
    {
        this.configuration = configuration;
        this.ringtone = ringtone;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Sound), theme);
            var options = RingtoneCatalog.Options;
            var card = GroupCard.Begin(theme, options.Count);
            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                if (SettingsRow.Selectable(card.NextRow(), CatalogLabels.Ringtone(option.SoundId),
                        option.SoundId == configuration.RingtoneId, theme))
                {
                    configuration.RingtoneId = option.SoundId;
                    configuration.Save();
                    ringtone.Play();
                }
            }

            card.End();
        }
    }

    private string CurrentName() => CatalogLabels.Ringtone(configuration.RingtoneId);
}

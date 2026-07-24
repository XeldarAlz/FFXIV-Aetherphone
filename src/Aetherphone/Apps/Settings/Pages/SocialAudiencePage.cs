using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal enum SocialAudienceKind
{
    Mentions,
    Tags,
    Messages,
}

internal sealed class SocialAudiencePage : ISettingsPage
{
    private readonly Func<SocialAudienceKind, int> read;
    private readonly Action<SocialAudienceKind, int> write;
    private SocialAudienceKind kind;

    public SocialAudiencePage(Func<SocialAudienceKind, int> read, Action<SocialAudienceKind, int> write)
    {
        this.read = read;
        this.write = write;
    }

    public string Title => Loc.T(kind switch
    {
        SocialAudienceKind.Tags => L.PhotoTag.AllowTags,
        SocialAudienceKind.Messages => L.Social.AllowMessages,
        _ => L.PhotoTag.AllowMentions,
    });
    public string Summary => Loc.T(SocialAudience.Label(read(kind)));
    public FontAwesomeIcon Icon => FontAwesomeIcon.Users;
    public Vector4 Tint => new(0.22f, 0.72f, 0.68f, 1f);

    public void Show(SocialAudienceKind target) => kind = target;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Title, theme);
            var current = read(kind);
            var card = GroupCard.Begin(theme, SocialAudience.Options.Length);
            for (var index = 0; index < SocialAudience.Options.Length; index++)
            {
                if (SettingsRow.Selectable(card.NextRow(), Loc.T(SocialAudience.Options[index]), current == index, theme)
                    && current != index)
                {
                    write(kind, index);
                }
            }

            card.End();
            SettingsSection.Hint(Loc.T(L.PhotoTag.AudienceHint), theme);
        }
    }
}

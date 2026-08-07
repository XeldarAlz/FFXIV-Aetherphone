using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Moderation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SafetyPage : ISettingsPage
{
    private readonly AethernetSession session;
    private readonly ModerationNoticeArchive archive;
    private readonly ISettingsNavigator navigator;
    private readonly SafetyNoticePage detail = new();

    public SafetyPage(AethernetSession session, ModerationNoticeArchive archive, ISettingsNavigator navigator)
    {
        this.session = session;
        this.archive = archive;
        this.navigator = navigator;
    }

    public string Title => Loc.T(L.Safety.Title);

    public string Summary => archive.PendingCount > 0
        ? Loc.T(L.Safety.UnreadSummary, archive.PendingCount)
        : Loc.T(L.Safety.Summary);

    public FontAwesomeIcon Icon => FontAwesomeIcon.ShieldAlt;

    public Vector4 Tint => new(0.86f, 0.42f, 0.32f, 1f);

    public bool ShowsBadge => archive.PendingCount > 0;

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (!session.IsSignedIn)
            {
                SettingsSection.Hint(Loc.T(L.Safety.SignInPrompt), theme);
                return;
            }

            archive.EnsureFresh();
            SettingsSection.Header(Loc.T(L.Safety.Title), theme);

            var items = archive.Items;
            if (items.Length == 0)
            {
                SettingsSection.Hint(archive.Loaded ? Loc.T(L.Safety.Empty) : Loc.T(L.Common.Loading), theme);
                return;
            }

            var card = GroupCard.Begin(theme, items.Length);
            for (var index = 0; index < items.Length; index++)
            {
                var notice = items[index];
                if (SettingsRow.Link(card.NextRow(), IconFor(notice), Tint,
                        ModerationNoticeText.Title(notice), TimeText.Short(notice.CreatedAtUnix), theme,
                        !notice.Acknowledged, "safety:" + notice.Id))
                {
                    detail.Show(notice);
                    navigator.Open(detail);
                }
            }

            card.End();

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            SettingsSection.Hint(Loc.T(L.Safety.RetentionHint), theme);

            if (archive.HasMore)
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                var moreCard = GroupCard.Begin(theme, 1);
                var loadOlder = SettingsRow.Disclosure(moreCard.NextRow(), Loc.T(L.Safety.LoadOlder),
                    string.Empty, theme);
                moreCard.End();
                if (loadOlder)
                {
                    archive.LoadMore();
                }
            }
        }
    }

    private static FontAwesomeIcon IconFor(ModerationNoticeDto notice)
    {
        return notice.Kind switch
        {
            ModerationNoticeKinds.ContentRemoved => FontAwesomeIcon.TrashAlt,
            ModerationNoticeKinds.Warning => FontAwesomeIcon.ExclamationTriangle,
            ModerationNoticeKinds.ProfileMediaRemoved => FontAwesomeIcon.UserSlash,
            ModerationNoticeKinds.ProfileTextCleared => FontAwesomeIcon.Eraser,
            ModerationNoticeKinds.Suspended => FontAwesomeIcon.Ban,
            ModerationNoticeKinds.SignedOut => FontAwesomeIcon.SignOutAlt,
            _ => FontAwesomeIcon.Heart,
        };
    }
}

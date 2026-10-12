using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SafetyPage : ISettingsPage
{
    private readonly AethernetSession session;
    private readonly ModerationNoticeArchive archive;
    private readonly ConductGateService conduct;
    private readonly ISettingsNavigator navigator;
    private readonly SafetyNoticePage detail = new();

    public SafetyPage(AethernetSession session, ModerationNoticeArchive archive, ConductGateService conduct,
        ISettingsNavigator navigator)
    {
        this.session = session;
        this.archive = archive;
        this.conduct = conduct;
        this.navigator = navigator;
    }

    public string Title => Loc.T(L.Safety.Title);

    public string Summary =>
        archive.PendingCount > 0 ? Loc.T(L.Safety.UnreadSummary, archive.PendingCount) : string.Empty;

    public FontAwesomeIcon Icon => FontAwesomeIcon.ShieldAlt;

    public Vector4 Tint => new(0.86f, 0.42f, 0.32f, 1f);

    public bool ShowsBadge => archive.PendingCount > 0;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            DrawNotices(theme);
            DrawGuidelines(theme);
        }
    }

    private void DrawNotices(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        if (!session.IsSignedIn)
        {
            SettingsSection.Hint(Loc.T(L.Safety.SignInPrompt), theme);
            return;
        }

        archive.EnsureFresh();
        SettingsSection.Header(Loc.T(L.Safety.Title), theme, Loc.T(L.Safety.RetentionHint));

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
        if (!archive.HasMore)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var moreCard = GroupCard.Begin(theme, 1);
        var loadOlder = SettingsRow.Disclosure(moreCard.NextRow(), Loc.T(L.Safety.LoadOlder), string.Empty, theme);
        moreCard.End();
        if (loadOlder)
        {
            archive.LoadMore();
        }
    }

    private void DrawGuidelines(PhoneTheme theme)
    {
        var gates = ConductRules.Gates;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * UiScale.Current));
        SettingsSection.Header(Loc.T(L.Safety.GuidelinesTitle), theme, Loc.T(L.Safety.GuidelinesHint));
        var card = GroupCard.Begin(theme, gates.Length);
        for (var index = 0; index < gates.Length; index++)
        {
            var gate = gates[index];
            if (SettingsRow.Link(card.NextRow(), gate.Icon, AppAccents.For(gate.AppId), Loc.T(gate.AppName),
                    string.Empty, theme, false, gate.AppId))
            {
                conduct.ShowRules(gate.AppId);
            }
        }

        card.End();
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
            ModerationNoticeKinds.EconomyAction => FontAwesomeIcon.Coins,
            _ => FontAwesomeIcon.Heart,
        };
    }
}

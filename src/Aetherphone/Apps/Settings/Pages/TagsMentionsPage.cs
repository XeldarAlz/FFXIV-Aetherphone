using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class TagsMentionsPage : ISettingsPage, IDisposable
{
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly ISettingsNavigator navigator;
    private readonly SocialAudiencePage audiencePage;
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool loaded;
    private volatile bool loading;
    private volatile int mentionPolicy;
    private volatile int tagPolicy;
    private volatile bool requireTagApproval;

    public TagsMentionsPage(AethernetSession session, AethernetClient client, ISettingsNavigator navigator)
    {
        this.session = session;
        this.client = client;
        this.navigator = navigator;
        audiencePage = new SocialAudiencePage(Read, Write);
    }

    public string Title => Loc.T(L.PhotoTag.SettingsTitle);
    public string Summary => Loc.T(SocialAudience.Label(tagPolicy));
    public FontAwesomeIcon Icon => FontAwesomeIcon.UserTag;
    public Vector4 Tint => new(0.22f, 0.72f, 0.68f, 1f);

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (!session.IsSignedIn)
            {
                SettingsSection.Hint(Loc.T(L.PhotoTag.SignInPrompt), theme);
                return;
            }

            EnsureLoaded();
            SettingsSection.Header(Loc.T(L.PhotoTag.SettingsTitle), theme);
            if (!loaded)
            {
                SettingsSection.Hint(Loc.T(L.Common.Loading), theme);
                return;
            }

            var card = GroupCard.Begin(theme, 2);
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.PhotoTag.AllowMentions),
                    Loc.T(SocialAudience.Label(mentionPolicy)), theme))
            {
                audiencePage.Show(SocialAudienceKind.Mentions);
                navigator.Open(audiencePage);
            }

            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.PhotoTag.AllowTags),
                    Loc.T(SocialAudience.Label(tagPolicy)), theme))
            {
                audiencePage.Show(SocialAudienceKind.Tags);
                navigator.Open(audiencePage);
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.PhotoTag.AudienceHint), theme);

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.PhotoTag.ApprovalHeader), theme);
            var approvalCard = GroupCard.Begin(theme, 1);
            var approve = SettingsRow.Bool(approvalCard.NextRow(), Loc.T(L.PhotoTag.ApproveManually),
                requireTagApproval, theme);
            approvalCard.End();
            if (approve != requireTagApproval)
            {
                requireTagApproval = approve;
                PushTags();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.PhotoTag.ApproveHint), theme);
        }
    }

    private int Read(SocialAudienceKind kind) => kind == SocialAudienceKind.Tags ? tagPolicy : mentionPolicy;

    private void Write(SocialAudienceKind kind, int policy)
    {
        if (!SocialAudience.IsDefined(policy))
        {
            return;
        }

        if (kind == SocialAudienceKind.Tags)
        {
            tagPolicy = policy;
            PushTags();
            return;
        }

        mentionPolicy = policy;
        PushMentions();
    }

    private void EnsureLoaded()
    {
        if (loaded || loading)
        {
            return;
        }

        loading = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.MeAsync(token).ConfigureAwait(false);
                if (me is not null)
                {
                    mentionPolicy = me.MentionPolicy;
                    tagPolicy = me.TagPolicy;
                    requireTagApproval = me.RequireTagApproval;
                    loaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Tag privacy load failed: {exception.Message}");
            }
            finally
            {
                loading = false;
            }
        });
    }

    private void PushMentions()
    {
        var policy = mentionPolicy;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.UpdateMentionPrivacyAsync(policy, token).ConfigureAwait(false);
                if (me is not null)
                {
                    mentionPolicy = me.MentionPolicy;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Mention privacy update failed: {exception.Message}");
            }
        });
    }

    private void PushTags()
    {
        var policy = tagPolicy;
        var approval = requireTagApproval;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.UpdateTagPrivacyAsync(policy, approval, token).ConfigureAwait(false);
                if (me is not null)
                {
                    tagPolicy = me.TagPolicy;
                    requireTagApproval = me.RequireTagApproval;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Tag privacy update failed: {exception.Message}");
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

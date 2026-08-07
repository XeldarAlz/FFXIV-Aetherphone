using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class AccountClient
{
    private readonly AethernetTransport net;

    public AccountClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<UserDto?> MeAsync(CancellationToken token)
    {
        return net.GetAsync("/me", AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> MeWithBearerAsync(string bearer, CancellationToken token)
    {
        return net.GetWithBearerAsync("/me", bearer, AethernetJsonContext.Default.UserDto, token);
    }

    public void EnsureCurrentUser()
    {
        var session = net.Session;
        if (!session.IsSignedIn || session.CurrentUser is not null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var user = await MeAsync(CancellationToken.None).ConfigureAwait(false);
                if (user is not null)
                {
                    session.SetUser(user);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet account load failed: {exception.Message}");
            }
        });
    }

    public Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken token,
        Action<int>? onStatus = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, "/me", request, AethernetJsonContext.Default.UpdateProfileRequest, AethernetJsonContext.Default.UserDto, token, onStatus);
    }

    public Task<UserDto?> UpdateTimeZoneAsync(UpdateTimeZoneRequest request, CancellationToken token)
    {
        return net.PostAsync("/me/timezone", request, AethernetJsonContext.Default.UpdateTimeZoneRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateRegionAsync(string region, CancellationToken token)
    {
        return net.PostAsync("/me/region", new UpdateRegionRequest(region), AethernetJsonContext.Default.UpdateRegionRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateMentionPrivacyAsync(int policy, CancellationToken token)
    {
        return net.PostAsync("/me/mention-privacy", new UpdateMentionPrivacyRequest(policy), AethernetJsonContext.Default.UpdateMentionPrivacyRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateMessagePrivacyAsync(int policy, CancellationToken token)
    {
        return net.PostAsync("/me/message-privacy", new UpdateMessagePrivacyRequest(policy), AethernetJsonContext.Default.UpdateMessagePrivacyRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateChatPrivacyAsync(UpdateChatPrivacyRequest request, CancellationToken token)
    {
        return net.PostAsync("/me/chat-privacy", request, AethernetJsonContext.Default.UpdateChatPrivacyRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateBadgesAsync(int equipped, CancellationToken token)
    {
        return net.PostAsync("/me/badges", new UpdateBadgeLoadoutRequest(equipped), AethernetJsonContext.Default.UpdateBadgeLoadoutRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<BadgeCatalogDto?> BadgeCatalogAsync(CancellationToken token)
    {
        return net.GetAsync("/badges/catalog", AethernetJsonContext.Default.BadgeCatalogDto, token);
    }

    public Task<AwardedBadgesDto?> AwardedBadgesAsync(CancellationToken token)
    {
        return net.GetAsync("/me/badges/awarded", AethernetJsonContext.Default.AwardedBadgesDto, token);
    }

    public Task<BadgeDescriptorDto?> SetBadgeVisibilityAsync(string badgeId, bool hidden, CancellationToken token)
    {
        return net.PostAsync("/me/badges/awarded/" + badgeId, new UpdateBadgeVisibilityRequest(hidden), AethernetJsonContext.Default.UpdateBadgeVisibilityRequest, AethernetJsonContext.Default.BadgeDescriptorDto, token);
    }

    public Task<UserDto?> UpdateAccountPrivacyAsync(bool isPrivate, CancellationToken token)
    {
        return net.PostAsync("/me/account-privacy", new UpdateAccountPrivacyRequest(isPrivate), AethernetJsonContext.Default.UpdateAccountPrivacyRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateTagPrivacyAsync(int? tagPolicy, bool? requireApproval, CancellationToken token)
    {
        return net.PostAsync("/me/tag-privacy", new UpdateTagPrivacyRequest(tagPolicy, requireApproval), AethernetJsonContext.Default.UpdateTagPrivacyRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<bool> DeleteAsync(CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, "/me", token);
    }

    public Task<UserDto?> UserAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/users/{Uri.EscapeDataString(userId)}", AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserSearchResult?> SearchAsync(string query, CancellationToken token)
    {
        return net.GetAsync($"/users/search?q={Uri.EscapeDataString(query)}", AethernetJsonContext.Default.UserSearchResult, token);
    }

    public Task<MentionSuggestResult?> MentionSuggestAsync(string query, CancellationToken token)
    {
        return net.GetAsync($"/users/mention-suggest?q={Uri.EscapeDataString(query)}", AethernetJsonContext.Default.MentionSuggestResult, token);
    }

    public Task<NotificationPage?> NotificationsAsync(CancellationToken token)
    {
        return net.GetAsync("/notifications", AethernetJsonContext.Default.NotificationPage, token);
    }

    public Task<NotificationPage?> NotificationsAsync(string app, string? cursor, CancellationToken token)
    {
        var path = $"/notifications?app={Uri.EscapeDataString(app)}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.NotificationPage, token);
    }

    public Task<NotificationReadResult?> MarkNotificationsReadAsync(long upToUnix, string? app, CancellationToken token)
    {
        return net.PostAsync("/notifications/read", new NotificationReadRequest(upToUnix, app),
            AethernetJsonContext.Default.NotificationReadRequest, AethernetJsonContext.Default.NotificationReadResult, token);
    }

    public Task<ModerationNoticePage?> NoticesAsync(string? cursor, CancellationToken token)
    {
        var path = cursor is null ? "/notices" : $"/notices?cursor={Uri.EscapeDataString(cursor)}";
        return net.GetAsync(path, AethernetJsonContext.Default.ModerationNoticePage, token);
    }

    public Task<bool> AcknowledgeNoticeAsync(string noticeId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/notices/{Uri.EscapeDataString(noticeId)}/ack", token);
    }

    public Task<PatreonLinkStartResponse?> StartPatreonLinkAsync(CancellationToken token, Action<int>? onStatus = null)
    {
        return net.RequestAsync(HttpMethod.Post, "/patreon/link/start",
            AethernetJsonContext.Default.PatreonLinkStartResponse, token, onStatus);
    }

    public Task<PatreonLinkStatusResponse?> PatreonLinkStatusAsync(CancellationToken token, Action<int>? onStatus = null)
    {
        return net.GetAsync("/patreon/link", AethernetJsonContext.Default.PatreonLinkStatusResponse, token, onStatus);
    }

    public Task<PatreonLinkStatusResponse?> UnlinkPatreonAsync(CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Delete, "/patreon/link",
            AethernetJsonContext.Default.PatreonLinkStatusResponse, token);
    }
}

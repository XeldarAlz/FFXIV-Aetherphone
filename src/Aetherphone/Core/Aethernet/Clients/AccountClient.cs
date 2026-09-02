using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class AccountClient
{
    private readonly AethernetTransport net;
    private readonly RetryGate meGate = new(TimeSpan.FromSeconds(30));

    public AccountClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<UserDto?> MeAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/me", AethernetJsonContext.Default.UserDto, token, null, onFailure);
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

        if (!meGate.TryPass())
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
                AepLog.Warning(exception, "Aethernet account load failed");
            }
        });
    }

    public Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken token,
        Action<int>? onStatus = null, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Patch, "/me", request, AethernetJsonContext.Default.UpdateProfileRequest, AethernetJsonContext.Default.UserDto, token, onStatus, onFailure);
    }

    public Task<UserDto?> UpdateTimeZoneAsync(UpdateTimeZoneRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/timezone", request, AethernetJsonContext.Default.UpdateTimeZoneRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateRegionAsync(string region, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/region", new UpdateRegionRequest(region), AethernetJsonContext.Default.UpdateRegionRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateMentionPrivacyAsync(int policy, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/mention-privacy", new UpdateMentionPrivacyRequest(policy), AethernetJsonContext.Default.UpdateMentionPrivacyRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateMessagePrivacyAsync(int policy, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/message-privacy", new UpdateMessagePrivacyRequest(policy), AethernetJsonContext.Default.UpdateMessagePrivacyRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateChatPrivacyAsync(UpdateChatPrivacyRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/chat-privacy", request, AethernetJsonContext.Default.UpdateChatPrivacyRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateBadgesAsync(int equipped, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/badges", new UpdateBadgeLoadoutRequest(equipped), AethernetJsonContext.Default.UpdateBadgeLoadoutRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<BadgeCatalogDto?> BadgeCatalogAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/badges/catalog", AethernetJsonContext.Default.BadgeCatalogDto, token, null, onFailure);
    }

    public Task<AwardedBadgesDto?> AwardedBadgesAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/me/badges/awarded", AethernetJsonContext.Default.AwardedBadgesDto, token, null, onFailure);
    }

    public Task<FrameCatalogDto?> FrameCatalogAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/frames/catalog", AethernetJsonContext.Default.FrameCatalogDto, token, null, onFailure);
    }

    public Task<InventoryDto?> InventoryAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/me/inventory", AethernetJsonContext.Default.InventoryDto, token, null, onFailure);
    }

    public Task<InventoryDto?> EquipAsync(string kind, string itemId, int? slot, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/inventory/equip", new InventoryEquipRequest(kind, itemId, slot),
            AethernetJsonContext.Default.InventoryEquipRequest, AethernetJsonContext.Default.InventoryDto, token,
            null, onFailure);
    }

    public Task<BadgeDescriptorDto?> SetBadgeVisibilityAsync(string badgeId, bool hidden, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/badges/awarded/" + badgeId, new UpdateBadgeVisibilityRequest(hidden), AethernetJsonContext.Default.UpdateBadgeVisibilityRequest, AethernetJsonContext.Default.BadgeDescriptorDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateAccountPrivacyAsync(bool isPrivate, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/account-privacy", new UpdateAccountPrivacyRequest(isPrivate), AethernetJsonContext.Default.UpdateAccountPrivacyRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserDto?> UpdateTagPrivacyAsync(int? tagPolicy, bool? requireApproval, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/me/tag-privacy", new UpdateTagPrivacyRequest(tagPolicy, requireApproval), AethernetJsonContext.Default.UpdateTagPrivacyRequest, AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<bool> DeleteAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, "/me", token, null, onFailure);
    }

    public Task<UserDto?> UserAsync(string userId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/users/{Uri.EscapeDataString(userId)}", AethernetJsonContext.Default.UserDto, token, null, onFailure);
    }

    public Task<UserSearchResult?> SearchAsync(string query, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/users/search?q={Uri.EscapeDataString(query)}", AethernetJsonContext.Default.UserSearchResult, token, null, onFailure);
    }

    public Task<MentionSuggestResult?> MentionSuggestAsync(string query, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/users/mention-suggest?q={Uri.EscapeDataString(query)}", AethernetJsonContext.Default.MentionSuggestResult, token, null, onFailure);
    }

    public Task<NotificationPage?> NotificationsAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/notifications", AethernetJsonContext.Default.NotificationPage, token, null, onFailure);
    }

    public Task<NotificationPage?> NotificationsAsync(string app, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/notifications?app={Uri.EscapeDataString(app)}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.NotificationPage, token, null, onFailure);
    }

    public Task<NotificationReadResult?> MarkNotificationsReadAsync(long upToUnix, string? app, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/notifications/read", new NotificationReadRequest(upToUnix, app),
            AethernetJsonContext.Default.NotificationReadRequest, AethernetJsonContext.Default.NotificationReadResult, token, null, onFailure);
    }

    public Task<ModerationNoticePage?> NoticesAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = cursor is null ? "/notices" : $"/notices?cursor={Uri.EscapeDataString(cursor)}";
        return net.GetAsync(path, AethernetJsonContext.Default.ModerationNoticePage, token, null, onFailure);
    }

    public Task<bool> AcknowledgeNoticeAsync(string noticeId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/notices/{Uri.EscapeDataString(noticeId)}/ack", token, null, onFailure);
    }

    public Task<PatreonLinkStartResponse?> StartPatreonLinkAsync(CancellationToken token, Action<int>? onStatus = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, "/patreon/link/start",
            AethernetJsonContext.Default.PatreonLinkStartResponse, token, onStatus, onFailure);
    }

    public Task<PatreonLinkStatusResponse?> PatreonLinkStatusAsync(CancellationToken token, Action<int>? onStatus = null,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/patreon/link", AethernetJsonContext.Default.PatreonLinkStatusResponse, token, onStatus, onFailure);
    }

    public Task<PatreonLinkStatusResponse?> UnlinkPatreonAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, "/patreon/link",
            AethernetJsonContext.Default.PatreonLinkStatusResponse, token, null, onFailure);
    }
}

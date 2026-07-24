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

    public Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken token)
    {
        return net.SendJsonAsync(HttpMethod.Patch, "/me", request, AethernetJsonContext.Default.UpdateProfileRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateTimeZoneAsync(UpdateTimeZoneRequest request, CancellationToken token)
    {
        return net.PostAsync("/me/timezone", request, AethernetJsonContext.Default.UpdateTimeZoneRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateMentionPrivacyAsync(int policy, CancellationToken token)
    {
        return net.PostAsync("/me/mention-privacy", new UpdateMentionPrivacyRequest(policy), AethernetJsonContext.Default.UpdateMentionPrivacyRequest, AethernetJsonContext.Default.UserDto, token);
    }

    public Task<UserDto?> UpdateChatPrivacyAsync(UpdateChatPrivacyRequest request, CancellationToken token)
    {
        return net.PostAsync("/me/chat-privacy", request, AethernetJsonContext.Default.UpdateChatPrivacyRequest, AethernetJsonContext.Default.UserDto, token);
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
}

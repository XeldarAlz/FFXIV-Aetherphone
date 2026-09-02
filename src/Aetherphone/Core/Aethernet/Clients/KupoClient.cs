using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class KupoClient
{
    private readonly AethernetTransport net;

    public KupoClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<ConfessionDto?> CreateConfessionAsync(string text, long expiresAtUnix, CancellationToken token,
        Action<int>? statusSink = null)
    {
        return net.PostAsync("/kupo/confessions", new CreateConfessionRequest(text, expiresAtUnix),
            AethernetJsonContext.Default.CreateConfessionRequest,
            AethernetJsonContext.Default.ConfessionDto, token, statusSink);
    }

    public Task<ConfessionDto?> GetConfessionAsync(string confessionId, CancellationToken token)
    {
        return net.GetAsync($"/kupo/confessions/{Uri.EscapeDataString(confessionId)}",
            AethernetJsonContext.Default.ConfessionDto, token);
    }

    public Task<bool> DeleteConfessionAsync(string confessionId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/kupo/confessions/{Uri.EscapeDataString(confessionId)}", token);
    }

    public Task<ConfessionPage?> FeedAsync(string? cursor, CancellationToken token)
    {
        var path = "/kupo/feed";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.ConfessionPage, token);
    }

    public Task<ConfessionPage?> MyConfessionsAsync(string userId, string? cursor, CancellationToken token)
    {
        var path = $"/kupo/users/{Uri.EscapeDataString(userId)}/confessions";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.ConfessionPage, token);
    }

    public Task<ConfessionResponseDto?> RespondAsync(string confessionId, string text, CancellationToken token,
        Action<int>? statusSink = null)
    {
        return net.PostAsync($"/kupo/confessions/{Uri.EscapeDataString(confessionId)}/responses",
            new CreateConfessionResponseRequest(text),
            AethernetJsonContext.Default.CreateConfessionResponseRequest,
            AethernetJsonContext.Default.ConfessionResponseDto, token, statusSink);
    }

    public Task<ConfessionResponsePage?> ResponsesAsync(string confessionId, string? cursor, CancellationToken token)
    {
        var path = $"/kupo/confessions/{Uri.EscapeDataString(confessionId)}/responses";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.ConfessionResponsePage, token);
    }

    public Task<bool> DeleteResponseAsync(string confessionId, string responseId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete,
            $"/kupo/confessions/{Uri.EscapeDataString(confessionId)}/responses/{Uri.EscapeDataString(responseId)}", token);
    }

    public Task<bool> SendKudosAsync(string confessionId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/kupo/confessions/{Uri.EscapeDataString(confessionId)}/kudos", token);
    }

    public Task<bool> LikeResponseAsync(string confessionId, string responseId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post,
            $"/kupo/confessions/{Uri.EscapeDataString(confessionId)}/responses/{Uri.EscapeDataString(responseId)}/like",
            token);
    }

    public Task<KupoStatsDto?> StatsAsync(CancellationToken token)
    {
        return net.GetAsync("/kupo/stats", AethernetJsonContext.Default.KupoStatsDto, token);
    }

    public Task<KupoStatsDto?> UserStatsAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/kupo/users/{Uri.EscapeDataString(userId)}/stats",
            AethernetJsonContext.Default.KupoStatsDto, token);
    }
}

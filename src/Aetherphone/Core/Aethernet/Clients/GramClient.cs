using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class GramClient
{
    private readonly AethernetTransport net;

    public GramClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<PostDto?> CreateAsync(string caption, string[] mediaKeys, int width, int height, PhotoTagInput[]? photoTags, CancellationToken token)
    {
        return net.PostAsync("/grams", new CreateGramRequest(caption, mediaKeys[0], width, height, mediaKeys, photoTags), AethernetJsonContext.Default.CreateGramRequest, AethernetJsonContext.Default.PostDto, token);
    }

    public Task<FeedPage?> FeedAsync(string scope, string? cursor, CancellationToken token)
    {
        var path = $"/feed?scope={scope}&kind=1";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token);
    }

    public Task<FeedPage?> UserGramsAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/users/{Uri.EscapeDataString(userId)}/posts?kind=1", AethernetJsonContext.Default.FeedPage, token);
    }

    public Task<FeedPage?> UserTaggedAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/users/{Uri.EscapeDataString(userId)}/tagged", AethernetJsonContext.Default.FeedPage, token);
    }

    public Task<StoryDto?> CreateStoryAsync(string caption, string mediaKey, int width, int height, CancellationToken token)
    {
        return net.PostAsync("/stories", new CreateStoryRequest(caption, mediaKey, width, height), AethernetJsonContext.Default.CreateStoryRequest, AethernetJsonContext.Default.StoryDto, token);
    }

    public Task<StoryTray?> StoryTrayAsync(CancellationToken token)
    {
        return net.GetAsync("/stories", AethernetJsonContext.Default.StoryTray, token);
    }

    public Task<StoryGroup?> UserStoriesAsync(string userId, CancellationToken token)
    {
        return net.GetAsync($"/stories/{Uri.EscapeDataString(userId)}", AethernetJsonContext.Default.StoryGroup, token);
    }

    public Task<StoryViewersPage?> StoryViewersAsync(string storyId, CancellationToken token)
    {
        return net.GetAsync($"/stories/{Uri.EscapeDataString(storyId)}/views", AethernetJsonContext.Default.StoryViewersPage, token);
    }

    public Task<bool> MarkStoryViewedAsync(string storyId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Post, $"/stories/{Uri.EscapeDataString(storyId)}/view", token);
    }

    public Task<bool> DeleteStoryAsync(string storyId, CancellationToken token)
    {
        return net.SendAsync(HttpMethod.Delete, $"/stories/{Uri.EscapeDataString(storyId)}", token);
    }
}

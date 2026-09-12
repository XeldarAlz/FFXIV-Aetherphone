using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class GramClient
{
    private readonly AethernetTransport net;

    public GramClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<PostDto?> CreateAsync(string caption, string[] mediaKeys, int width, int height, PhotoTagInput[]? photoTags, bool sensitive, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/grams", new CreateGramRequest(caption, mediaKeys[0], width, height, mediaKeys, photoTags, sensitive), AethernetJsonContext.Default.CreateGramRequest, AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<PostDto?> EditAsync(string postId, string caption, PhotoTagInput[] photoTags, bool sensitive,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/posts/{Uri.EscapeDataString(postId)}",
            new EditGramRequest(caption, photoTags, sensitive), AethernetJsonContext.Default.EditGramRequest,
            AethernetJsonContext.Default.PostDto, token, null, onFailure);
    }

    public Task<FeedPage?> FeedAsync(string scope, string? cursor, string? regions, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/feed?scope={scope}&kind=1";
        if (regions is not null)
        {
            path += $"&regions={Uri.EscapeDataString(regions)}";
        }

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<FeedPage?> UserGramsAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/users/{Uri.EscapeDataString(userId)}/posts?kind=1";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<FeedPage?> UserTaggedAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/users/{Uri.EscapeDataString(userId)}/tagged";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<FeedPage?> TagPostsAsync(string tag, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/tags/{Uri.EscapeDataString(tag)}/posts?kind=1";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.FeedPage, token, null, onFailure);
    }

    public Task<StoryDto?> CreateStoryAsync(string caption, string mediaKey, int width, int height, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/stories", new CreateStoryRequest(caption, mediaKey, width, height), AethernetJsonContext.Default.CreateStoryRequest, AethernetJsonContext.Default.StoryDto, token, null, onFailure);
    }

    public Task<StoryTray?> StoryTrayAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/stories", AethernetJsonContext.Default.StoryTray, token, null, onFailure);
    }

    public Task<StoryGroup?> UserStoriesAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync($"/stories/{Uri.EscapeDataString(userId)}", AethernetJsonContext.Default.StoryGroup, token, null, onFailure);
    }

    public Task<StoryViewersPage?> StoryViewersAsync(string storyId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = $"/stories/{Uri.EscapeDataString(storyId)}/views";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.StoryViewersPage, token, null, onFailure);
    }

    public Task<bool> MarkStoryViewedAsync(string storyId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Post, $"/stories/{Uri.EscapeDataString(storyId)}/view", token, null, onFailure);
    }

    public Task<bool> DeleteStoryAsync(string storyId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, $"/stories/{Uri.EscapeDataString(storyId)}", token, null, onFailure);
    }
}

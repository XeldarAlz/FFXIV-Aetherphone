using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class GlamClient
{
    internal const string ReportTarget = "prism_glam";
    internal const string SavedCollection = "saved";
    internal const string ScopeNew = "new";
    internal const string ScopeToday = "today";
    internal const string ScopePopular = "popular";
    internal const string ScopeFollowing = "following";

    private readonly AethernetTransport net;

    public GlamClient(AethernetTransport net)
    {
        this.net = net;
    }

    internal static string FeedPath(string scope, bool? vanilla, string? cursor, int race = 0, int? gender = null)
    {
        var path = $"/prism/glams?scope={Uri.EscapeDataString(scope)}";
        if (vanilla is { } only)
        {
            path += only ? "&vanilla=true" : "&vanilla=false";
        }

        return WithCursor(WithBody(path, race, gender), cursor);
    }

    private static string WithBody(string path, int race, int? gender)
    {
        if (race > 0)
        {
            path += $"&race={race}";
        }

        return gender is { } chosen ? $"{path}&gender={chosen}" : path;
    }

    internal static string GlamPath(string glamId) => $"/prism/glams/{Uri.EscapeDataString(glamId)}";

    internal static string LikePath(string glamId) => GlamPath(glamId) + "/like";

    internal static string TryOnPath(string glamId) => GlamPath(glamId) + "/tryon";

    internal static string UserGlamsPath(string userId, string? cursor) =>
        WithCursor($"/users/{Uri.EscapeDataString(userId)}/glams", cursor);

    internal static string UserProfilePath(string userId) => $"/users/{Uri.EscapeDataString(userId)}/prism";

    internal static string CollectionPath(string collectionId) =>
        $"/prism/collections/{Uri.EscapeDataString(collectionId)}";

    internal static string CollectionGlamsPath(string collectionId, string? cursor) =>
        WithCursor(CollectionPath(collectionId) + "/glams", cursor);

    internal static string CollectionGlamPath(string collectionId, string glamId) =>
        CollectionPath(collectionId) + $"/glams/{Uri.EscapeDataString(glamId)}";

    internal static string ItemPath(uint itemId) => $"/prism/items/{itemId}";

    internal static string ItemGlamsPath(uint itemId, string? cursor) => WithCursor(ItemPath(itemId) + "/glams", cursor);

    internal static string ModGlamsPath(string name, string author, string? cursor) =>
        WithCursor($"/prism/mods/glams?name={Uri.EscapeDataString(name)}&author={Uri.EscapeDataString(author)}", cursor);

    internal static string TagGlamsPath(string tag, string? cursor) =>
        WithCursor($"/prism/tags/{Uri.EscapeDataString(tag)}/glams", cursor);

    internal static string WeekGlamsPath(string weekId, string track, string? cursor) =>
        WithCursor($"/prism/weeks/{Uri.EscapeDataString(weekId)}/glams?track={Uri.EscapeDataString(track)}", cursor);

    public Task<GlamPage?> WeekGlamsAsync(string weekId, string track, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(WeekGlamsPath(weekId, track, cursor), AethernetJsonContext.Default.GlamPage, token, null,
            onFailure);

    public Task<GlamPage?> FeedAsync(string scope, bool? vanilla, (int Race, int? Gender) body, string? cursor,
        CancellationToken token, Action<AepFailure>? onFailure = null) =>
        net.GetAsync(FeedPath(scope, vanilla, cursor, body.Race, body.Gender), AethernetJsonContext.Default.GlamPage,
            token, null, onFailure);

    public Task<GlamDto?> GlamAsync(string glamId, CancellationToken token, Action<AepFailure>? onFailure = null) =>
        net.GetAsync(GlamPath(glamId), AethernetJsonContext.Default.GlamDto, token, null, onFailure);

    public Task<GlamDto?> CreateAsync(CreateGlamRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.PostAsync("/prism/glams", request, AethernetJsonContext.Default.CreateGlamRequest,
            AethernetJsonContext.Default.GlamDto, token, null, onFailure);

    public Task<bool> DeleteAsync(string glamId, CancellationToken token, Action<AepFailure>? onFailure = null) =>
        net.SendAsync(HttpMethod.Delete, GlamPath(glamId), token, null, onFailure);

    public Task<bool> LikeAsync(string glamId, bool liked, CancellationToken token, Action<AepFailure>? onFailure = null) =>
        net.SendAsync(liked ? HttpMethod.Put : HttpMethod.Delete, LikePath(glamId), token, null, onFailure);

    public Task<bool> TriedOnAsync(string glamId, CancellationToken token) =>
        net.SendAsync(HttpMethod.Post, TryOnPath(glamId), token);

    public Task<GlamPage?> UserGlamsAsync(string userId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(UserGlamsPath(userId, cursor), AethernetJsonContext.Default.GlamPage, token, null, onFailure);

    public Task<GlamProfileDto?> ProfileAsync(string userId, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(UserProfilePath(userId), AethernetJsonContext.Default.GlamProfileDto, token, null, onFailure);

    public Task<GlamPage?> CollectionGlamsAsync(string collectionId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(CollectionGlamsPath(collectionId, cursor), AethernetJsonContext.Default.GlamPage, token, null,
            onFailure);

    public Task<bool> SetInCollectionAsync(string collectionId, string glamId, bool inside, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.SendAsync(inside ? HttpMethod.Put : HttpMethod.Delete, CollectionGlamPath(collectionId, glamId), token, null,
            onFailure);

    public Task<GlamItemDto?> ItemAsync(uint itemId, CancellationToken token, Action<AepFailure>? onFailure = null) =>
        net.GetAsync(ItemPath(itemId), AethernetJsonContext.Default.GlamItemDto, token, null, onFailure);

    public Task<GlamPage?> ItemGlamsAsync(uint itemId, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(ItemGlamsPath(itemId, cursor), AethernetJsonContext.Default.GlamPage, token, null, onFailure);

    public Task<GlamPage?> ModGlamsAsync(string name, string author, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(ModGlamsPath(name, author, cursor), AethernetJsonContext.Default.GlamPage, token, null, onFailure);

    public Task<GlamPage?> TagGlamsAsync(string tag, string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null) =>
        net.GetAsync(TagGlamsPath(tag, cursor), AethernetJsonContext.Default.GlamPage, token, null, onFailure);

    public Task<GlamSeasonDto?> SeasonAsync(CancellationToken token, Action<AepFailure>? onFailure = null) =>
        net.GetAsync("/prism/seasons/current", AethernetJsonContext.Default.GlamSeasonDto, token, null, onFailure);

    private static string WithCursor(string path, string? cursor)
    {
        if (cursor is null)
        {
            return path;
        }

        return path + (path.Contains('?') ? "&cursor=" : "?cursor=") + Uri.EscapeDataString(cursor);
    }
}

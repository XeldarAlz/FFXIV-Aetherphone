using Newtonsoft.Json;

namespace Aetherphone.Apps.KupoAi;

internal static class KupoAiRoles
{
    public const int User = 0;
    public const int Assistant = 1;
    public const int System = 2;
}

internal static class KupoAiNotes
{
    public const string Quota = "quota";
    public const string GlobalQuota = "global";
    public const string Indexing = "indexing";
    public const string NoMatch = "nomatch";
    public const string Offline = "offline";
    public const string RateLimited = "ratelimited";
    public const string Error = "error";
}

internal sealed class KupoAiMessage
{
    [JsonProperty("r")] public int Role { get; set; }

    [JsonProperty("t")] public string Text { get; set; } = string.Empty;

    [JsonProperty("st")] public string[] SourceTitles { get; set; } = Array.Empty<string>();

    [JsonProperty("su")] public string[] SourceUrls { get; set; } = Array.Empty<string>();

    [JsonProperty("u")] public long AtUnix { get; set; }
}

internal sealed class KupoAiConversation
{
    [JsonProperty("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonProperty("title")] public string Title { get; set; } = string.Empty;

    [JsonProperty("updated")] public long UpdatedAtUnix { get; set; }

    [JsonProperty("messages")] public List<KupoAiMessage> Messages { get; set; } = new();
}

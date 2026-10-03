using System.Text.Json.Serialization;

namespace Aetherphone.Core.Rolladeck;

internal sealed class LiveResponse
{
    [JsonPropertyName("liveDJs")]    public List<LiveDjEntry>    LiveDJs    { get; set; } = [];
    [JsonPropertyName("openVenues")] public List<OpenVenueEntry> OpenVenues { get; set; } = [];
}

internal sealed class LiveDjEntry
{
    [JsonPropertyName("djName")]    public string  DjName    { get; set; } = "";
    [JsonPropertyName("djSlug")]    public string? DjSlug    { get; set; }
    [JsonPropertyName("twitchUrl")] public string? TwitchUrl { get; set; }
    [JsonPropertyName("venueName")] public string? VenueName { get; set; }
    [JsonPropertyName("server")]    public string? Server    { get; set; }
    [JsonPropertyName("datacenter")] public string? Datacenter { get; set; }
    [JsonPropertyName("district")]  public string? District  { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    [JsonPropertyName("viewerCount")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ViewerCount { get; set; }

    [JsonPropertyName("lifestream")]  public string? LifestreamArg { get; set; }
    [JsonPropertyName("streamTitle")] public string? StreamTitle   { get; set; }
    [JsonPropertyName("avatarUrl")]   public string? AvatarUrl     { get; set; }
    [JsonPropertyName("eventName")]   public string? EventName     { get; set; }
    [JsonPropertyName("genres")]        public List<string> Genres        { get; set; } = [];
    [JsonPropertyName("bio")]           public string? Bio           { get; set; }
    [JsonPropertyName("discordName")]   public string? DiscordName   { get; set; }
    [JsonPropertyName("twitter")]       public string? Twitter       { get; set; }
    [JsonPropertyName("bluesky")]       public string? Bluesky       { get; set; }
    [JsonPropertyName("instagram")]     public string? Instagram     { get; set; }
    [JsonPropertyName("youtube")]       public string? Youtube       { get; set; }
    [JsonPropertyName("tiktok")]        public string? Tiktok        { get; set; }
    [JsonPropertyName("website")]       public string? Website       { get; set; }
    [JsonPropertyName("musicPlatform")] public string? MusicPlatform { get; set; }

    private string? normalizedName;
    private string? normalizedTitle;

    public bool    CanTeleport  => !string.IsNullOrEmpty(LifestreamArg);
    public string? TeleportDestination => RolladeckText.TeleportDestination(LifestreamArg);
    public string  ServerLabel  => Server ?? Datacenter ?? "";
    public string? RolladeckUrl => TwitchUsername != null ? $"https://xivrolladeck.com/{TwitchUsername}" : null;
    public bool    HasLocation  => VenueName != null || District != null;

    public string  NormalizedName  => normalizedName ?? DjName;
    public string? NormalizedTitle => normalizedTitle;

    public void InitNormalized()
    {
        normalizedName  = RolladeckText.Normalize(DjName);
        normalizedTitle = string.IsNullOrEmpty(StreamTitle) ? null : RolladeckText.Normalize(StreamTitle);
    }

    public string? TwitchUsername
    {
        get
        {
            if (TwitchUrl == null)
            {
                return null;
            }
            var uri        = TwitchUrl.TrimEnd('/');
            var slashIndex = uri.LastIndexOf('/');
            return slashIndex >= 0 ? uri[(slashIndex + 1)..] : null;
        }
    }

    public string FormattedAddress =>
        District != null && Ward.HasValue && Plot.HasValue
            ? $"{District} W{Ward} P{Plot}"
            : District ?? "";
}

internal sealed class OpenVenueEntry
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Id { get; set; }

    [JsonPropertyName("name")]      public string? Name       { get; set; }
    [JsonPropertyName("slug")]      public string? Slug       { get; set; }
    [JsonPropertyName("server")]    public string? Server     { get; set; }
    [JsonPropertyName("datacenter")] public string? Datacenter { get; set; }
    [JsonPropertyName("district")]  public string? District   { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    [JsonPropertyName("logoUrl")]    public string? LogoUrl    { get; set; }
    [JsonPropertyName("bannerUrl")]  public string? BannerUrl  { get; set; }
    [JsonPropertyName("lifestream")] public string? Lifestream { get; set; }
    [JsonPropertyName("openReason")] public string? OpenReason { get; set; }
    [JsonPropertyName("eventName")]  public string? EventName  { get; set; }
    [JsonPropertyName("eventStart")] public string? EventStart { get; set; }
    [JsonPropertyName("eventEnd")]   public string? EventEnd   { get; set; }
    [JsonPropertyName("djName")]     public string? DjName     { get; set; }
    [JsonPropertyName("djTwitch")]   public string? DjTwitch   { get; set; }
    [JsonPropertyName("activeDiscordEventName")] public string? DiscordEventName { get; set; }
    [JsonPropertyName("description")]    public string? Description    { get; set; }
    [JsonPropertyName("firstOpened")]    public string? FirstOpened    { get; set; }
    [JsonPropertyName("websiteOrCarrd")] public string? WebsiteOrCarrd { get; set; }
    [JsonPropertyName("discordServer")]  public string? DiscordServer  { get; set; }
    [JsonPropertyName("amenities")] public List<string> Amenities { get; set; } = [];

    public bool    CanTeleport   => !string.IsNullOrEmpty(Lifestream);
    public string? TeleportDestination => RolladeckText.TeleportDestination(Lifestream);
    public string  ServerLabel   => Server ?? Datacenter ?? "";
    public string  DisplayName   => Name ?? "Unknown Venue";
    public string? RolladeckUrl  => Slug != null ? $"https://xivrolladeck.com/venue/{Slug}" : null;
    public string? DjRolladeckUrl
    {
        get
        {
            if (DjTwitch == null)
            {
                return null;
            }

            var uri        = DjTwitch.TrimEnd('/');
            var slashIndex = uri.LastIndexOf('/');
            var username   = slashIndex >= 0 ? uri[(slashIndex + 1)..] : null;
            return username != null ? $"https://xivrolladeck.com/{username}" : null;
        }
    }

    public string FormattedAddress =>
        District != null && Ward.HasValue && Plot.HasValue
            ? $"{District} W{Ward} P{Plot}"
            : District ?? "";

    public string? ActiveLabel => EventName ?? DjName ?? DiscordEventName;

    public string? WebsiteUrl => WebsiteOrCarrd;
    public string? DiscordUrl => DiscordServer;
    public string? FirstOpenedYear
    {
        get
        {
            if (string.IsNullOrEmpty(FirstOpened))
            {
                return null;
            }
            if (DateTime.TryParse(FirstOpened, out var dt))
            {
                return dt.ToString("MMM yyyy");
            }
            return null;
        }
    }
}

internal sealed class DirectoryResponse
{
    [JsonPropertyName("venues")] public List<DirectoryVenueEntry> Venues { get; set; } = [];
}

internal sealed class DirectoryVenueEntry
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Id { get; set; }

    [JsonPropertyName("name")]       public string? Name       { get; set; }
    [JsonPropertyName("slug")]       public string? Slug       { get; set; }
    [JsonPropertyName("server")]     public string? Server     { get; set; }
    [JsonPropertyName("datacenter")] public string? Datacenter { get; set; }
    [JsonPropertyName("district")]   public string? District   { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    [JsonPropertyName("logoUrl")]        public string? LogoUrl        { get; set; }
    [JsonPropertyName("bannerUrl")]      public string? BannerUrl      { get; set; }
    [JsonPropertyName("websiteOrCarrd")] public string? WebsiteOrCarrd { get; set; }
    [JsonPropertyName("discordServer")]  public string? DiscordServer  { get; set; }
    [JsonPropertyName("lifestream")]     public string? Lifestream     { get; set; }
    [JsonPropertyName("sfw")]            public bool? Sfw              { get; set; }
    [JsonPropertyName("amenities")]      public List<string> Amenities { get; set; } = [];

    [JsonPropertyName("upcomingDiscordEvents")]
    public List<DirectoryEventEntry> UpcomingDiscordEvents { get; set; } = [];
}

internal sealed class DirectoryEventEntry
{
    [JsonPropertyName("name")]      public string? Name              { get; set; }
    [JsonPropertyName("startTime")] public string? StartTime { get; set; }
    [JsonPropertyName("endTime")]   public string? EndTime   { get; set; }
}

[JsonSerializable(typeof(LiveResponse))]
[JsonSerializable(typeof(DirectoryResponse))]
internal partial class RolladeckJsonContext : JsonSerializerContext { }

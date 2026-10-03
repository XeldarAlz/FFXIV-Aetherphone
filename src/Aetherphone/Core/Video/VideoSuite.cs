using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Video;

internal sealed class VideoSuite : IDisposable
{
    internal VideoSuite(PhoneServices services, Configuration configuration, IChatGui chatGui)
    {
        Screen = new ScreenController(() => configuration.VideoHideNameplates);
        Player = new VideoPlayer(Screen.Engine);
        Library = new VideoLibrary(configuration);
        Queue = new AetherStreamQueue(Player, services.VideoMetadata, Library, configuration);
        WatchAlong = new WatchAlongSession(services.AethernetSession, configuration, services.Confirm, Player, Queue,
            services.StreamSignals, Screen);
        Suggestions = new StreamSuggestionNotifier(WatchAlong, services.Notifications);
        ChatFeed = new ScreenChatFeed(chatGui, configuration, Screen.Engine);
        ApplySettings(configuration);
    }

    internal ScreenController Screen { get; }
    internal VideoPlayer Player { get; }
    internal VideoLibrary Library { get; }
    internal AetherStreamQueue Queue { get; }
    internal WatchAlongSession WatchAlong { get; }
    internal StreamSuggestionNotifier Suggestions { get; }
    internal ScreenChatFeed ChatFeed { get; }

    internal bool PlacingScreen { get; set; }

    internal void ApplySettings(Configuration configuration)
    {
        Player.SetVolume((int)(configuration.VideoVolume * 100));
        Player.HardwareDecoding = configuration.VideoHardwareDecoding;
        Player.AllowInsecureDirectUrls = configuration.VideoAllowInsecureDirectUrls;
        Player.MaxQualityHeight = configuration.VideoMaxQualityHeight;
        Player.SpatialAudio = configuration.VideoSpatialAudio;
        Player.SpatialRange = configuration.VideoSpatialRange;
        Player.MuteInBackground = configuration.VideoMuteInBackground;
        Screen.Engine.ScreenVisible = configuration.VideoScreenVisible;
        if (!WatchAlong.IsViewing)
        {
            Screen.Engine.ScreenCurve = configuration.VideoScreenCurve;
        }
    }

    internal void OnFrameworkUpdate(float deltaSeconds)
    {
        Player.OnFrameworkUpdate();
        Queue.OnFrameworkUpdate();
        WatchAlong.OnFrameworkUpdate(deltaSeconds);
        if (PlacingScreen && !Screen.Engine.IsActive)
        {
            PlacingScreen = false;
        }
    }

    public void Dispose()
    {
        ChatFeed.Dispose();
        Suggestions.Dispose();
        WatchAlong.Dispose();
        Queue.Dispose();
        Player.Dispose();
        Screen.Dispose();
    }
}

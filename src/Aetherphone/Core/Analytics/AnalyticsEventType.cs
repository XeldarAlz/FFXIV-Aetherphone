namespace Aetherphone.Core.Analytics;

internal static class AnalyticsEventType
{
    public const string FirstRun = "first_run";
    public const string SessionStart = "session_start";
    public const string SessionEnd = "session_end";
    public const string ShellOpened = "shell_opened";
    public const string ShellClosed = "shell_closed";
    public const string AppOpen = "app_open";
    public const string AppClose = "app_close";
    public const string ScreenView = "screen_view";
    public const string NotificationOpened = "notification_opened";
    public const string OnboardingStep = "onboarding_step";
    public const string SignupStep = "signup_step";
    public const string PostCreated = "post_created";
    public const string Reaction = "reaction";
    public const string Comment = "comment";
    public const string Follow = "follow";
    public const string DmSent = "dm_sent";
    public const string Call = "call";
    public const string MusicListen = "music_listen";
    public const string MarketSearch = "market_search";
    public const string GameStarted = "game_started";
    public const string GameEnded = "game_ended";
    public const string ErrorOccurred = "error_occurred";
    public const string SettingChanged = "setting_changed";
}

using System.Globalization;
using System.Reflection;
using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Updates;
using Dalamud.Plugin;

namespace Aetherphone.Core.Platform;

internal static class SupportInfo
{
    public static string Build(Configuration configuration, GameData gameData, AethernetSession session)
    {
        var builder = new StringBuilder(2048);
        AppendVersions(builder);
        AppendEnvironment(builder, configuration, gameData);
        AppendInterface(builder, configuration);
        AppendAccount(builder, configuration, session);
        AppendNotifications(builder, configuration);
        AppendFeatures(builder, configuration);
        AppendPlugins(builder);
        return builder.ToString();
    }

    private static void AppendVersions(StringBuilder builder)
    {
        builder.Append(AepConstants.Name).Append(' ').Append(AepConstants.Version);
        if (!string.Equals(InstallSource.Build, AepConstants.Version, StringComparison.Ordinal))
        {
            builder.Append(" (").Append(InstallSource.Build).Append(')');
        }

        builder.Append('\n');
        builder.Append("Source: ").Append(InstallSource.Repository.Length == 0 ? "unknown" : InstallSource.Repository);
        if (Plugin.PluginInterface.IsTesting)
        {
            builder.Append(" (testing)");
        }

        builder.Append('\n');
        builder.Append("Dalamud: ").Append(DalamudVersion()).Append('\n');
    }

    private static void AppendEnvironment(StringBuilder builder, Configuration configuration, GameData gameData)
    {
        builder.Append("OS: ").Append(Environment.OSVersion.VersionString).Append('\n');
        builder.Append("Wine: ").Append(NativeFileDialog.RunsUnderWine).Append('\n');
        builder.Append("Game language: ").Append(Plugin.ClientState.ClientLanguage).Append('\n');
        builder.Append("Region: ").Append(gameData.LocalRegionCode()).Append('\n');
        builder.Append("Chinese client: ").Append(gameData.IsChineseGameClient()).Append('\n');
        AppendWorld(builder, gameData);
        builder.Append("Phone language: ").Append(Loc.Current.Code).Append('\n');
        builder.Append("Time zone: ")
            .Append(SocialTimeZone.FormatOffset(SocialTimeZone.EffectiveOffsetMinutes(configuration)));
        if (configuration.TimeZoneManual)
        {
            builder.Append(" (manual)");
        }

        builder.Append('\n');
    }

    private static void AppendWorld(StringBuilder builder, GameData gameData)
    {
        var currentWorldId = gameData.LocalCurrentWorldId;
        if (currentWorldId == 0)
        {
            return;
        }

        builder.Append("World: ").Append(gameData.WorldName(currentWorldId));
        var dataCenter = gameData.DataCenterName(currentWorldId);
        if (dataCenter.Length > 0)
        {
            builder.Append(" (").Append(dataCenter).Append(')');
        }

        var homeWorldId = gameData.LocalHomeWorldId;
        if (homeWorldId != 0 && homeWorldId != currentWorldId)
        {
            builder.Append(", home ").Append(gameData.WorldName(homeWorldId));
        }

        builder.Append('\n');
    }

    private static void AppendInterface(StringBuilder builder, Configuration configuration)
    {
        builder.Append('\n');
        builder.Append("UI scale: game ").Append(Scale(UiScale.Global))
            .Append(", phone ").Append(Scale(UiScale.Phone))
            .Append(", text zoom ").Append(Scale(configuration.TextZoom)).Append('\n');
        builder.Append("Phone width: ").Append((int)configuration.PhoneWidth);
        if (configuration.LandscapePhoneWidth > 0f)
        {
            builder.Append(", landscape ").Append((int)configuration.LandscapePhoneWidth);
        }

        builder.Append('\n');
        builder.Append("Theme: ").Append(configuration.ThemeMode)
            .Append(", accent ").Append(configuration.AccentName)
            .Append(", case ").Append(configuration.PhoneCaseName).Append('\n');
        builder.Append("Open on startup: ").Append(configuration.OpenOnStartup)
            .Append(" (minimized: ").Append(configuration.OpenMinimizedOnStartup).Append(")\n");
    }

    private static void AppendAccount(StringBuilder builder, Configuration configuration, AethernetSession session)
    {
        builder.Append('\n');
        builder.Append("Signed in: ").Append(session.IsSignedIn);
        var handle = session.CurrentUser?.Handle;
        if (session.IsSignedIn && !string.IsNullOrEmpty(handle))
        {
            builder.Append(" (@").Append(handle).Append(')');
        }

        builder.Append('\n');
        if (session.TokenRejected)
        {
            builder.Append("Session: token rejected\n");
        }

        if (session.IsBanned)
        {
            builder.Append("Session: banned\n");
        }

        if (session.IsSourceBlocked)
        {
            builder.Append("Session: source blocked\n");
        }

        builder.Append("Server: ").Append(ServerLabel(session.BaseUrl)).Append('\n');
        builder.Append("Accounts: ").Append(configuration.CharacterSessions.Count)
            .Append(" (follow character: ").Append(configuration.FollowCharacterAccount).Append(")\n");
        builder.Append("E2E keys stored: ").Append(configuration.EncryptionKeysByUserId.Count).Append('\n');
        builder.Append("Calls enabled: ").Append(configuration.CallsEnabled).Append('\n');
    }

    private static void AppendNotifications(StringBuilder builder, Configuration configuration)
    {
        builder.Append('\n');
        builder.Append("Do Not Disturb: ").Append(configuration.DoNotDisturb).Append('\n');
        builder.Append("Quiet While Busy: ").Append(configuration.QuietWhileBusy).Append('\n');
        builder.Append("Silent mode: ").Append(configuration.SilentMode).Append('\n');
        builder.Append("Banners: ").Append(configuration.ShowNotificationBanner).Append('\n');
        builder.Append("Notification sounds: ").Append(configuration.NotificationSoundsEnabled)
            .Append(" (volume ").Append(Percent(configuration.NotificationVolume)).Append("%)\n");
        builder.Append("Ringtone: ").Append(configuration.RingtoneEnabled)
            .Append(" (volume ").Append(Percent(configuration.RingtoneVolume)).Append("%)\n");
    }

    private static void AppendFeatures(StringBuilder builder, Configuration configuration)
    {
        builder.Append('\n');
        builder.Append("Native file dialog: ").Append(TriState(configuration.UseNativeFileDialog)).Append('\n');
        builder.Append("Import screenshots: ").Append(configuration.ImportScreenshots).Append('\n');
        builder.Append("Lodestone portraits: ").Append(configuration.ShowLodestonePortraits).Append('\n');
        builder.Append("Video: hardware decoding ").Append(configuration.VideoHardwareDecoding)
            .Append(", max quality ").Append(configuration.VideoMaxQualityHeight).Append("p\n");
    }

    private static void AppendPlugins(StringBuilder builder)
    {
        var loaded = new List<string>();
        foreach (var installed in Plugin.PluginInterface.InstalledPlugins)
        {
            if (!installed.IsLoaded)
            {
                continue;
            }

            var entry = installed.IsDev
                ? $"{installed.Name} {installed.Version} (dev)"
                : $"{installed.Name} {installed.Version}";
            loaded.Add(entry);
        }

        loaded.Sort(StringComparer.OrdinalIgnoreCase);
        builder.Append('\n');
        builder.Append("Plugins loaded (").Append(loaded.Count).Append("): ");
        for (var index = 0; index < loaded.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(loaded[index]);
        }
    }

    private static string DalamudVersion()
    {
        var assembly = typeof(IDalamudPluginInterface).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            return informational;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string ServerLabel(string baseUrl) =>
        string.Equals(baseUrl, Configuration.DefaultAethernetBaseUrl, StringComparison.OrdinalIgnoreCase)
            ? "default"
            : baseUrl;

    private static string TriState(bool? value)
    {
        if (value is null)
        {
            return "auto";
        }

        return value.Value ? "on" : "off";
    }

    private static string Scale(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static int Percent(float value) => (int)MathF.Round(value * 100f);
}

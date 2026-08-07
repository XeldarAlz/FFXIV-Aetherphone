using System.Text;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;

namespace Aetherphone.Core;

internal static class SupportInfo
{
    public static string Build(Configuration configuration)
    {
        var builder = new StringBuilder(320);
        builder.Append(AepConstants.Name).Append(' ').Append(AepConstants.Version).Append('\n');
        builder.Append("Game: ").Append(Plugin.ClientState.ClientLanguage).Append('\n');
        builder.Append("OS: ").Append(Environment.OSVersion.VersionString).Append('\n');
        builder.Append("Wine: ").Append(NativeFileDialog.RunsUnderWine).Append('\n');
        builder.Append("Language: ").Append(Loc.Current.Code).Append('\n');
        builder.Append("Do Not Disturb: ").Append(configuration.DoNotDisturb).Append('\n');
        builder.Append("Quiet While Busy: ").Append(configuration.QuietWhileBusy).Append('\n');
        builder.Append("Banners: ").Append(configuration.ShowNotificationBanner).Append('\n');
        builder.Append("Sound volume: ").Append(configuration.NotificationVolume);
        return builder.ToString();
    }
}

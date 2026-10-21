using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;

namespace Aetherphone.Core.Campaigns;

internal sealed class CampaignIconSync
{
    private const string FolderName = "campaign-icons";
    private const string BackgroundSuffix = ".png";
    private const string ForegroundSuffix = ".fg.png";

    private readonly CampaignDefinition definition;
    private readonly Configuration configuration;
    private readonly RemoteImageCache images;
    private readonly string directory;
    private int syncing;
    private bool applied;

    public CampaignIconSync(CampaignDefinition definition, Configuration configuration, RemoteImageCache images,
        string configDirectory)
    {
        this.definition = definition;
        this.configuration = configuration;
        this.images = images;
        directory = Path.Combine(configDirectory, FolderName);
    }

    private string BackgroundPath => Path.Combine(directory, definition.AppId + BackgroundSuffix);

    private string ForegroundPath => Path.Combine(directory, definition.AppId + ForegroundSuffix);

    public void Restore()
    {
        if (configuration.LoveIslandIconUrl.Length == 0 || !FilesPresent())
        {
            return;
        }

        ApplyOverride();
    }

    public void Sync(CampaignDto campaign)
    {
        var iconUrl = campaign.IconUrl ?? string.Empty;
        var foregroundUrl = campaign.IconForegroundUrl ?? string.Empty;
        if (iconUrl.Length == 0 || foregroundUrl.Length == 0)
        {
            Clear();
            return;
        }

        var unchanged = string.Equals(iconUrl, configuration.LoveIslandIconUrl, StringComparison.Ordinal)
                        && string.Equals(foregroundUrl, configuration.LoveIslandIconForegroundUrl,
                            StringComparison.Ordinal);
        if (unchanged && FilesPresent())
        {
            if (!applied)
            {
                ApplyOverride();
            }

            return;
        }

        if (Interlocked.Exchange(ref syncing, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var background = await images.FetchBytesAsync(iconUrl, CancellationToken.None).ConfigureAwait(false);
                var foreground = await images.FetchBytesAsync(foregroundUrl, CancellationToken.None)
                    .ConfigureAwait(false);
                if (background is null || foreground is null)
                {
                    AepLog.Warning($"[{definition.AppId}] icon download failed");
                    return;
                }

                Directory.CreateDirectory(directory);
                await ReplaceAsync(BackgroundPath, background).ConfigureAwait(false);
                await ReplaceAsync(ForegroundPath, foreground).ConfigureAwait(false);
                await Plugin.Framework.RunOnFrameworkThread(() => Remember(iconUrl, foregroundUrl))
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"[{definition.AppId}] icon sync failed");
            }
            finally
            {
                Volatile.Write(ref syncing, 0);
            }
        });
    }

    private void Remember(string iconUrl, string foregroundUrl)
    {
        configuration.LoveIslandIconUrl = iconUrl;
        configuration.LoveIslandIconForegroundUrl = foregroundUrl;
        configuration.Save();
        applied = false;
        ApplyOverride();
    }

    private void ApplyOverride()
    {
        var use = CampaignShellBridge.UseIconOverride;
        if (use is null)
        {
            return;
        }

        applied = use(definition.AppId, directory);
    }

    private void Clear()
    {
        if (configuration.LoveIslandIconUrl.Length == 0 && configuration.LoveIslandIconForegroundUrl.Length == 0)
        {
            return;
        }

        configuration.LoveIslandIconUrl = string.Empty;
        configuration.LoveIslandIconForegroundUrl = string.Empty;
        configuration.Save();
        applied = false;
        CampaignShellBridge.ClearIconOverride?.Invoke(definition.AppId);
    }

    private bool FilesPresent() => File.Exists(BackgroundPath) && File.Exists(ForegroundPath);

    private static async Task ReplaceAsync(string path, byte[] bytes)
    {
        var staging = path + ".download";
        await File.WriteAllBytesAsync(staging, bytes).ConfigureAwait(false);
        File.Move(staging, path, true);
    }
}

using System.Text.Json;
using Aetherphone.Harness.Scenarios;

namespace Aetherphone.Harness.Host;

internal sealed class HarnessOptions
{
    private const string CacheEnvironmentVariable = "AETHERPHONE_HARNESS_CACHE";
    private const string SqpackEnvironmentVariable = "AETHERPHONE_SQPACK";
    private const string LauncherConfigFile = "launcherConfigV3.json";
    private const string LauncherGamePathKey = "GamePath";
    private const string ExdIndexFile = "0a0000.win32.index";
    private const int DefaultWidth = 1600;
    private const int DefaultHeight = 1200;
    private const int DefaultFrames = 90;
    private const int DefaultPort = 47821;

    public string CacheDirectory { get; private set; } = DefaultCacheDirectory();

    public string ConfigDirectory { get; private set; } = string.Empty;

    public bool ConfigDefaulted { get; private set; }

    public string BackendUrl { get; private set; } = Configuration.TestAethernetBaseUrl;

    public string AssetDirectory { get; private set; } = string.Empty;

    public string SqpackDirectory { get; private set; } = string.Empty;

    public string Scenario { get; private set; } = ScenarioCatalog.Default;

    public string OutputPath { get; private set; } = Path.Combine(Environment.CurrentDirectory, "phone.png");

    public int Width { get; private set; } = DefaultWidth;

    public int Height { get; private set; } = DefaultHeight;

    public int Frames { get; private set; } = DefaultFrames;

    public int Port { get; private set; } = DefaultPort;

    public static HarnessOptions Parse(string[] arguments, int start)
    {
        var options = new HarnessOptions();
        for (var index = start; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--cache":
                    options.CacheDirectory = Value(arguments, ref index);
                    break;
                case "--config":
                    options.ConfigDirectory = Value(arguments, ref index);
                    break;
                case "--backend":
                    options.BackendUrl = HarnessBackend.Resolve(Value(arguments, ref index));
                    break;
                case "--assets":
                    options.AssetDirectory = Value(arguments, ref index);
                    break;
                case "--sqpack":
                    options.SqpackDirectory = Value(arguments, ref index);
                    break;
                case "--scenario":
                    options.Scenario = Value(arguments, ref index);
                    break;
                case "--out":
                    options.OutputPath = Value(arguments, ref index);
                    break;
                case "--width":
                    options.Width = int.Parse(Value(arguments, ref index));
                    break;
                case "--height":
                    options.Height = int.Parse(Value(arguments, ref index));
                    break;
                case "--frames":
                    options.Frames = int.Parse(Value(arguments, ref index));
                    break;
                case "--port":
                    options.Port = int.Parse(Value(arguments, ref index));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arguments[index]}'.");
            }
        }

        options.FillDefaults();
        return options;
    }

    private void FillDefaults()
    {
        if (ConfigDirectory.Length == 0)
        {
            ConfigDefaulted = true;
            ConfigDirectory = Path.Combine(CacheDirectory, HarnessBackend.ConfigFolder(BackendUrl));
        }

        if (AssetDirectory.Length == 0)
        {
            AssetDirectory = Path.Combine(CacheDirectory, "assets");
        }

        if (SqpackDirectory.Length == 0)
        {
            SqpackDirectory = Environment.GetEnvironmentVariable(SqpackEnvironmentVariable) ??
                              InstalledGameSqpack() ?? Path.Combine(CacheDirectory, "sqpack");
        }
    }

    private static string? InstalledGameSqpack()
    {
        var launcherConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", LauncherConfigFile);
        if (!File.Exists(launcherConfig))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(launcherConfig));
            if (!document.RootElement.TryGetProperty(LauncherGamePathKey, out var gamePath) ||
                gamePath.GetString() is not { Length: > 0 } installation)
            {
                return null;
            }

            var sqpack = Path.Combine(installation, "game", "sqpack");
            return File.Exists(Path.Combine(sqpack, "ffxiv", ExdIndexFile)) ? sqpack : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string DefaultCacheDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(CacheEnvironmentVariable);
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aetherphone-harness");
    }

    private static string Value(string[] arguments, ref int index)
    {
        if (index + 1 >= arguments.Length)
        {
            throw new ArgumentException($"'{arguments[index]}' needs a value.");
        }

        index += 1;
        return arguments[index];
    }
}

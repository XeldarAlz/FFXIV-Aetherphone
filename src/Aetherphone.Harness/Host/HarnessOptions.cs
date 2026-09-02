namespace Aetherphone.Harness.Host;

internal sealed class HarnessOptions
{
    private const string CacheEnvironmentVariable = "AETHERPHONE_HARNESS_CACHE";
    private const string SqpackEnvironmentVariable = "AETHERPHONE_SQPACK";
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;
    private const int DefaultFrames = 90;

    public string CacheDirectory { get; private set; } = DefaultCacheDirectory();

    public string ConfigDirectory { get; private set; } = string.Empty;

    public string AssetDirectory { get; private set; } = string.Empty;

    public string SqpackDirectory { get; private set; } = string.Empty;

    public string OutputPath { get; private set; } = Path.Combine(Environment.CurrentDirectory, "phone.png");

    public int Width { get; private set; } = DefaultWidth;

    public int Height { get; private set; } = DefaultHeight;

    public int Frames { get; private set; } = DefaultFrames;

    public static HarnessOptions Parse(string[] arguments)
    {
        var options = new HarnessOptions();
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--cache":
                    options.CacheDirectory = Value(arguments, ref index);
                    break;
                case "--config":
                    options.ConfigDirectory = Value(arguments, ref index);
                    break;
                case "--assets":
                    options.AssetDirectory = Value(arguments, ref index);
                    break;
                case "--sqpack":
                    options.SqpackDirectory = Value(arguments, ref index);
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
            ConfigDirectory = Path.Combine(CacheDirectory, "config");
        }

        if (AssetDirectory.Length == 0)
        {
            AssetDirectory = Path.Combine(CacheDirectory, "assets");
        }

        if (SqpackDirectory.Length == 0)
        {
            SqpackDirectory = Environment.GetEnvironmentVariable(SqpackEnvironmentVariable) ?? Path.Combine(CacheDirectory, "sqpack");
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

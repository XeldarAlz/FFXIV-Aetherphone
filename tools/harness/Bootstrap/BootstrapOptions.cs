namespace Aetherphone.Harness.Bootstrap;

internal sealed class BootstrapOptions
{
    private const string DefaultDalamudUrl = "https://goatcorp.github.io/dalamud-distrib/latest.zip";
    private const string CacheEnvironmentVariable = "AETHERPHONE_HARNESS_CACHE";

    public string CacheDirectory { get; private set; } = DefaultCacheDirectory();

    public string DalamudUrl { get; private set; } = DefaultDalamudUrl;

    public bool Refresh { get; private set; }

    public static BootstrapOptions Parse(string[] arguments)
    {
        var options = new BootstrapOptions();
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--cache":
                    options.CacheDirectory = RequireValue(arguments, ref index);
                    break;
                case "--dalamud-url":
                    options.DalamudUrl = RequireValue(arguments, ref index);
                    break;
                case "--refresh":
                    options.Refresh = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arguments[index]}'.");
            }
        }

        return options;
    }

    public static string DefaultCacheDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(CacheEnvironmentVariable);
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".aetherphone-harness");
    }

    private static string RequireValue(string[] arguments, ref int index)
    {
        if (index + 1 >= arguments.Length)
        {
            throw new ArgumentException($"'{arguments[index]}' needs a value.");
        }

        index += 1;
        return arguments[index];
    }
}

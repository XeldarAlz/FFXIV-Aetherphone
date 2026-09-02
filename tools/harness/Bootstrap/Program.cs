namespace Aetherphone.Harness.Bootstrap;

internal static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        BootstrapOptions options;
        try
        {
            options = BootstrapOptions.Parse(arguments);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Usage: dotnet run --project tools/harness/Bootstrap -- [--cache DIR] [--dalamud-url URL] [--refresh]");
            return 2;
        }

        Directory.CreateDirectory(options.CacheDirectory);
        var dalamudDirectory = Path.Combine(options.CacheDirectory, "dalamud");
        await DalamudDownload.EnsureAsync(options.DalamudUrl, dalamudDirectory, options.Refresh);
        var patched = PortableExecutablePatcher.PatchDirectoryToHost(dalamudDirectory);
        if (patched > 0)
        {
            Console.WriteLine($"Retargeted {patched} x64-stamped managed assemblies to this host");
        }

        var nativePath = await NativeImGuiBuild.EnsureAsync(dalamudDirectory, options.CacheDirectory, options.Refresh);
        var assetDirectory = await DalamudAssets.EnsureFontsAsync(options.CacheDirectory, options.Refresh);
        Console.WriteLine();
        Console.WriteLine("Harness cache ready.");
        Console.WriteLine($"  Dalamud:      {dalamudDirectory}");
        Console.WriteLine($"  Native ImGui: {nativePath}");
        Console.WriteLine($"  Fonts:        {assetDirectory}");
        Console.WriteLine($"  Set DALAMUD_HOME={dalamudDirectory} to build the plugin and the harness against this cache.");
        return 0;
    }
}

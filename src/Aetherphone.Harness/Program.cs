using System.Runtime.CompilerServices;
using Aetherphone.Harness.Bootstrap;
using Aetherphone.Harness.Driver;
using Aetherphone.Harness.Host;

namespace Aetherphone.Harness;

internal static class Program
{
    public static int Main(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            Console.Error.WriteLine("Usage: serve [options] | render [options] | <driver command>");
            return 2;
        }

        if (arguments[0] != "serve" && arguments[0] != "render")
        {
            return DriverClient.Run(arguments, HarnessOptions.Parse(Array.Empty<string>(), 0).CacheDirectory);
        }

        PortableExecutablePatcher.PatchDirectoryToHost(AppContext.BaseDirectory);
        return RunHost(arguments);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunHost(string[] arguments)
    {
        HarnessOptions options;
        try
        {
            options = HarnessOptions.Parse(arguments, 1);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        using var host = new PhoneHost(options);
        host.Step(3);
        host.OpenPhone();
        if (arguments[0] == "render")
        {
            host.Step(options.Frames);
            host.Screenshot(options.OutputPath, true);
            return 0;
        }

        host.Step(options.Frames);
        new DriverServer(host, options.Port, options.CacheDirectory).Run();
        return 0;
    }
}

using System.Runtime.CompilerServices;
using Aetherphone.Harness.Bootstrap;
using Aetherphone.Harness.Host;

namespace Aetherphone.Harness;

internal static class Program
{
    public static int Main(string[] arguments)
    {
        PortableExecutablePatcher.PatchDirectoryToHost(AppContext.BaseDirectory);
        return RunHost(arguments);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunHost(string[] arguments)
    {
        HarnessOptions options;
        try
        {
            options = HarnessOptions.Parse(arguments);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        using var host = new PhoneHost(options);
        host.Step(3);
        host.OpenPhone();
        host.Step(options.Frames);
        host.Screenshot(options.OutputPath);
        return 0;
    }
}

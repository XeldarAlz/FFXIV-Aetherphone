using System.Reflection;

namespace Aetherphone.Harness.Fakes;

public class NullProxy : DispatchProxy
{
    private static readonly HashSet<string> Reported = new();
    private string interfaceName = string.Empty;

    public static TInterface Create<TInterface>()
        where TInterface : class
    {
        var proxy = Create<TInterface, NullProxy>();
        ((NullProxy)(object)proxy).interfaceName = typeof(TInterface).Name;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        var name = targetMethod.Name;
        if (name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal))
        {
            return null;
        }

        Report(name);
        return DefaultValue.For(targetMethod.ReturnType);
    }

    private void Report(string member)
    {
        var key = interfaceName + "." + member;
        lock (Reported)
        {
            if (!Reported.Add(key))
            {
                return;
            }
        }

        HarnessLog.Note($"{key} is not faked; returning a default value");
    }
}

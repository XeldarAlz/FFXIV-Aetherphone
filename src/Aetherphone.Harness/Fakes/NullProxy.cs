using System.Reflection;

namespace Aetherphone.Harness.Fakes;

public class NullProxy : DispatchProxy
{
    private static readonly Dictionary<MethodInfo, object?> Answers = new();
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

        lock (Answers)
        {
            if (Answers.TryGetValue(targetMethod, out var answer))
            {
                return answer;
            }
        }

        var name = targetMethod.Name;
        var subscription = name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal);
        if (!subscription)
        {
            HarnessLog.Note($"{interfaceName}.{name} is not faked; returning a default value");
        }

        var value = subscription ? null : DefaultValue.For(targetMethod.ReturnType);
        lock (Answers)
        {
            Answers[targetMethod] = value;
        }

        return value;
    }
}

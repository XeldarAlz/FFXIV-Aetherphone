using System.Collections.Concurrent;
using System.Reflection;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Fakes;

public class UnlockStateProxy : DispatchProxy
{
    private const string QuestMethod = nameof(IUnlockState.IsQuestCompleted);
    private const string RowIdProperty = "RowId";
    private const uint Buckets = 10000;
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> RowIdProperties = new();

    private bool questsCompleted;
    private uint collectedBuckets;

    public static IUnlockState Create() => Create<IUnlockState, UnlockStateProxy>();

    public static void Apply(IUnlockState state, bool questsCompleted, double collected)
    {
        var proxy = (UnlockStateProxy)(object)state;
        proxy.questsCompleted = questsCompleted;
        proxy.collectedBuckets = (uint)Math.Round(Math.Clamp(collected, 0d, 1d) * Buckets);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        if (targetMethod.ReturnType != typeof(bool))
        {
            return DefaultValue.For(targetMethod.ReturnType);
        }

        if (targetMethod.IsSpecialName)
        {
            return true;
        }

        if (string.Equals(targetMethod.Name, QuestMethod, StringComparison.Ordinal))
        {
            return questsCompleted;
        }

        if (collectedBuckets == 0)
        {
            return false;
        }

        return collectedBuckets >= Buckets || Bucket(targetMethod.Name, RowId(args)) < collectedBuckets;
    }

    private static uint RowId(object?[]? args)
    {
        if (args is null || args.Length == 0 || args[0] is not { } row)
        {
            return 0;
        }

        if (row is uint direct)
        {
            return direct;
        }

        var property = RowIdProperties.GetOrAdd(row.GetType(), type => type.GetProperty(RowIdProperty));
        return property?.GetValue(row) is uint rowId ? rowId : 0;
    }

    private static uint Bucket(string kind, uint rowId)
    {
        var hash = FnvOffsetBasis;
        for (var index = 0; index < kind.Length; index++)
        {
            hash = (hash ^ kind[index]) * FnvPrime;
        }

        for (var shift = 0; shift < 32; shift += 8)
        {
            hash = (hash ^ ((rowId >> shift) & 0xFF)) * FnvPrime;
        }

        return hash % Buckets;
    }
}

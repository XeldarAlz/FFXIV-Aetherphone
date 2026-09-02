using System.Collections;
using System.Reflection;

namespace Aetherphone.Harness.Fakes;

internal static class DefaultValue
{
    private static readonly MethodInfo EmptyEnumeratorMethod =
        typeof(DefaultValue).GetMethod(nameof(EmptyEnumerator), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CompletedTaskMethod =
        typeof(DefaultValue).GetMethod(nameof(CompletedTask), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static object? For(Type type)
    {
        if (type == typeof(void))
        {
            return null;
        }

        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (type == typeof(IEnumerator))
        {
            return Array.Empty<object>().GetEnumerator();
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var argument = type.GetGenericArguments()[0];
            if (definition == typeof(Task<>))
            {
                return CompletedTaskMethod.MakeGenericMethod(argument).Invoke(null, null);
            }

            if (definition == typeof(IEnumerator<>))
            {
                return EmptyEnumeratorMethod.MakeGenericMethod(argument).Invoke(null, null);
            }

            if (definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>) ||
                definition == typeof(IReadOnlyCollection<>) || definition == typeof(IList<>) ||
                definition == typeof(ICollection<>))
            {
                return Array.CreateInstance(argument, 0);
            }

            if (definition == typeof(List<>))
            {
                return Activator.CreateInstance(type);
            }
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static IEnumerator<T> EmptyEnumerator<T>() => ((IEnumerable<T>)Array.Empty<T>()).GetEnumerator();

    private static Task<T> CompletedTask<T>() => Task.FromResult((T)For(typeof(T))!);
}

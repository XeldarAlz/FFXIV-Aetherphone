using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet;

internal static class CopyOnWrite
{
    public static T[] Replace<T>(T[] source, T updated) where T : class, IIdentified
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != updated.Id)
            {
                continue;
            }

            var result = (T[])source.Clone();
            result[index] = updated;
            return result;
        }

        return source;
    }

    public static T[] RemoveById<T>(T[] source, string id) where T : class, IIdentified
    {
        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != id)
            {
                count++;
            }
        }

        if (count == source.Length)
        {
            return source;
        }

        var result = new T[count];
        var resultIndex = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != id)
            {
                result[resultIndex++] = source[index];
            }
        }

        return result;
    }

    public static T[] Prepend<T>(T[] source, T item) where T : class, IIdentified
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id == item.Id)
            {
                return source;
            }
        }

        var result = new T[source.Length + 1];
        result[0] = item;
        Array.Copy(source, 0, result, 1, source.Length);
        return result;
    }

    public static T[] Append<T>(T[] source, T item)
    {
        var result = new T[source.Length + 1];
        Array.Copy(source, 0, result, 0, source.Length);
        result[source.Length] = item;
        return result;
    }

    public static T[] Reversed<T>(T[] source)
    {
        var result = new T[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            result[index] = source[source.Length - 1 - index];
        }

        return result;
    }

    public static T[] MapById<T>(T[] source, string id, Func<T, T> transform) where T : class, IIdentified
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != id)
            {
                continue;
            }

            var result = (T[])source.Clone();
            result[index] = transform(source[index]);
            return result;
        }

        return source;
    }

    public static T[] Map<T>(T[] source, Func<T, bool> match, Func<T, T> transform)
    {
        var changed = false;
        var result = new T[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var item = source[index];
            if (match(item))
            {
                result[index] = transform(item);
                changed = true;
            }
            else
            {
                result[index] = item;
            }
        }

        return changed ? result : source;
    }
}

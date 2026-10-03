using System.Globalization;
using System.Text;

namespace Aetherphone.Core.Home;

internal static class WidgetConfig
{
    private const int CacheCapacity = 128;
    private const char PairSeparator = ';';
    private const char ValueSeparator = '=';

    private readonly struct Entry
    {
        public readonly string Key;
        public readonly string Value;

        public Entry(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    private static readonly Dictionary<string, Entry[]> Cache = new(StringComparer.Ordinal);

    public static string Get(string config, string key) => Get(config, key, string.Empty);

    public static string Get(string config, string key, string fallback)
    {
        if (string.IsNullOrEmpty(config))
        {
            return fallback;
        }

        var entries = Parsed(config);
        for (var index = 0; index < entries.Length; index++)
        {
            if (string.Equals(entries[index].Key, key, StringComparison.Ordinal))
            {
                return entries[index].Value;
            }
        }

        return fallback;
    }

    public static int GetInt(string config, string key, int fallback)
    {
        var value = Get(config, key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public static bool GetBool(string config, string key, bool fallback)
    {
        var value = Get(config, key);
        if (value.Length == 0)
        {
            return fallback;
        }

        return string.Equals(value, "1", StringComparison.Ordinal) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static string Set(string config, string key, string value)
    {
        var cleanKey = Clean(key);
        var cleanValue = Clean(value);
        var entries = string.IsNullOrEmpty(config) ? Array.Empty<Entry>() : Parsed(config);
        var builder = new StringBuilder();
        var replaced = false;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (string.Equals(entry.Key, cleanKey, StringComparison.Ordinal))
            {
                replaced = true;
                if (cleanValue.Length > 0)
                {
                    Append(builder, cleanKey, cleanValue);
                }

                continue;
            }

            Append(builder, entry.Key, entry.Value);
        }

        if (!replaced && cleanValue.Length > 0)
        {
            Append(builder, cleanKey, cleanValue);
        }

        return builder.ToString();
    }

    private static Entry[] Parsed(string config)
    {
        if (Cache.TryGetValue(config, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= CacheCapacity)
        {
            Cache.Clear();
        }

        var parsed = Parse(config);
        Cache[config] = parsed;
        return parsed;
    }

    private static Entry[] Parse(string config)
    {
        var entries = new List<Entry>();
        var start = 0;
        while (start < config.Length)
        {
            var end = config.IndexOf(PairSeparator, start);
            if (end < 0)
            {
                end = config.Length;
            }

            var separator = config.IndexOf(ValueSeparator, start, end - start);
            if (separator > start)
            {
                entries.Add(new Entry(config.Substring(start, separator - start),
                    config.Substring(separator + 1, end - separator - 1)));
            }

            start = end + 1;
        }

        return entries.ToArray();
    }

    private static void Append(StringBuilder builder, string key, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(PairSeparator);
        }

        builder.Append(key).Append(ValueSeparator).Append(value);
    }

    private static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.IndexOf(PairSeparator) < 0 && text.IndexOf(ValueSeparator) < 0)
        {
            return text;
        }

        return text.Replace(PairSeparator.ToString(), string.Empty, StringComparison.Ordinal)
            .Replace(ValueSeparator.ToString(), string.Empty, StringComparison.Ordinal);
    }
}

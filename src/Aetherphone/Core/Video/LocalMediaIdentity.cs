using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Aetherphone.Core.Video;

internal sealed class LocalMediaIdentity
{
    internal LocalMediaIdentity(string fileName, long sizeBytes, string contentHash)
    {
        FileName = fileName;
        SizeBytes = sizeBytes;
        ContentHash = contentHash;
        MapKey = contentHash + ":" + sizeBytes.ToString(CultureInfo.InvariantCulture);
        Token = LocalMediaToken.Format(fileName, sizeBytes, contentHash);
    }

    internal string FileName { get; }
    internal long SizeBytes { get; }
    internal string ContentHash { get; }
    internal string MapKey { get; }
    internal string Token { get; }

    internal bool Matches(LocalMediaIdentity other) =>
        SizeBytes == other.SizeBytes && string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal);
}

internal static class LocalMediaToken
{
    internal const string Prefix = "aep-local:";

    private const string Version = "1";
    private const int SampleBytes = 256 * 1024;
    private const int MaxFileNameChars = 180;
    private const int ContentHashChars = 64;

    internal static bool IsToken(string url) => url.StartsWith(Prefix, StringComparison.Ordinal);

    internal static string Format(string fileName, long sizeBytes, string contentHash)
    {
        var boundedName = fileName.Length > MaxFileNameChars ? fileName[..MaxFileNameChars] : fileName;
        return Prefix + Version + ":" + contentHash + ":" + sizeBytes.ToString(CultureInfo.InvariantCulture)
            + ":" + Uri.EscapeDataString(boundedName);
    }

    internal static bool TryParse(string url, out LocalMediaIdentity identity)
    {
        identity = null!;
        if (!IsToken(url))
        {
            return false;
        }

        var segments = url.Split(':', 5);
        if (segments.Length != 5 || segments[1] != Version)
        {
            return false;
        }

        var contentHash = segments[2];
        if (contentHash.Length != ContentHashChars || !IsLowerHex(contentHash))
        {
            return false;
        }

        if (!long.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var sizeBytes)
            || sizeBytes <= 0)
        {
            return false;
        }

        string fileName;
        try
        {
            fileName = Uri.UnescapeDataString(segments[4]);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (fileName.Length == 0)
        {
            return false;
        }

        identity = new LocalMediaIdentity(fileName, sizeBytes, contentHash);
        return true;
    }

    internal static LocalMediaIdentity? TryCompute(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var length = stream.Length;
            if (length <= 0)
            {
                return null;
            }

            var headLength = (int)Math.Min(SampleBytes, length);
            var tailLength = length > SampleBytes ? (int)Math.Min(SampleBytes, length - SampleBytes) : 0;
            var buffer = new byte[headLength + tailLength + sizeof(long)];
            stream.ReadExactly(buffer, 0, headLength);
            if (tailLength > 0)
            {
                stream.Seek(-tailLength, SeekOrigin.End);
                stream.ReadExactly(buffer, headLength, tailLength);
            }

            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(headLength + tailLength), length);
            var hash = SHA256.HashData(buffer);
            return new LocalMediaIdentity(Path.GetFileName(path), length, Convert.ToHexStringLower(hash));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[WatchAlong] could not fingerprint a local file: {exception.Message}");
            return null;
        }
    }

    private static bool IsLowerHex(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is (< '0' or > '9') and (< 'a' or > 'f'))
            {
                return false;
            }
        }

        return true;
    }
}

internal static class LocalMediaFiles
{
    private const int MaxMappedFiles = 64;

    internal static bool TryResolve(Configuration configuration, LocalMediaIdentity identity, out string path)
    {
        path = string.Empty;
        var records = configuration.VideoLocalFileMap;
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Key != identity.MapKey)
            {
                continue;
            }

            try
            {
                var file = new FileInfo(record.Path);
                if (file.Exists && file.Length == record.SizeBytes)
                {
                    path = record.Path;
                    return true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[WatchAlong] could not stat a mapped local file: {exception.Message}");
            }

            records.RemoveAt(index);
            configuration.Save();
            return false;
        }

        return false;
    }

    internal static void Remember(Configuration configuration, LocalMediaIdentity identity, string path,
        long sizeBytes)
    {
        var records = configuration.VideoLocalFileMap;
        for (var index = records.Count - 1; index >= 0; index--)
        {
            if (records[index].Key == identity.MapKey)
            {
                records.RemoveAt(index);
            }
        }

        records.Add(new VideoLocalFileMapRecord { Key = identity.MapKey, Path = path, SizeBytes = sizeBytes });
        while (records.Count > MaxMappedFiles)
        {
            records.RemoveAt(0);
        }

        configuration.Save();
    }
}

using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Aetherphone.Core.Linkpearl;

internal sealed class StoredLine
{
    [JsonProperty("d")] public int Direction { get; set; }

    [JsonProperty("t")] public string Text { get; set; } = string.Empty;

    [JsonProperty("u")] public long AtUnix { get; set; }
}

internal sealed class StoredConversation
{
    [JsonProperty("contact")] public string Contact { get; set; } = string.Empty;

    [JsonProperty("target")] public string SendTarget { get; set; } = string.Empty;

    [JsonProperty("lines")] public List<StoredLine> Lines { get; set; } = new();
}

internal readonly struct ArchivedConversation
{
    public readonly string Contact;
    public readonly string SendTarget;
    public readonly List<ChatLine> Lines;

    public ArchivedConversation(string contact, string sendTarget, List<ChatLine> lines)
    {
        Contact = contact;
        SendTarget = sendTarget;
        Lines = lines;
    }
}

internal sealed class MessageArchive
{
    private const int MaxStoredLines = 500;
    private readonly object sync = new();
    private readonly DirectoryInfo baseDir;
    private DirectoryInfo? activeRoot;

    public MessageArchive(DirectoryInfo baseDir)
    {
        this.baseDir = baseDir;
        if (!baseDir.Exists)
        {
            baseDir.Create();
        }
    }

    public void SetCharacter(ulong contentId)
    {
        lock (sync)
        {
            if (contentId == 0)
            {
                activeRoot = null;
                return;
            }

            var directory = new DirectoryInfo(Path.Combine(baseDir.FullName, contentId.ToString("x16")));
            if (!directory.Exists)
            {
                directory.Create();
            }

            activeRoot = directory;
        }
    }

    public void MigrateLegacyTo(ulong contentId)
    {
        if (contentId == 0)
        {
            return;
        }

        try
        {
            lock (sync)
            {
                var target = new DirectoryInfo(Path.Combine(baseDir.FullName, contentId.ToString("x16")));
                if (!target.Exists)
                {
                    target.Create();
                }

                var legacy = baseDir.GetFiles("*.json");
                for (var index = 0; index < legacy.Length; index++)
                {
                    File.Move(legacy[index].FullName, Path.Combine(target.FullName, legacy[index].Name), true);
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"MessageArchive legacy migration failed: {exception.Message}");
        }
    }

    public List<ArchivedConversation> LoadAll()
    {
        var result = new List<ArchivedConversation>();
        DirectoryInfo? directory;
        lock (sync)
        {
            directory = activeRoot;
        }

        if (directory is null)
        {
            return result;
        }

        FileInfo[] files;
        try
        {
            files = directory.GetFiles("*.json");
        }
        catch (Exception exception)
        {
            AepLog.Warning($"MessageArchive list failed: {exception.Message}");
            return result;
        }

        for (var index = 0; index < files.Length; index++)
        {
            var stored = TryLoad(files[index]);
            if (stored is null || stored.SendTarget.Length == 0 || stored.Lines.Count == 0)
            {
                continue;
            }

            var lines = new List<ChatLine>(stored.Lines.Count);
            for (var lineIndex = 0; lineIndex < stored.Lines.Count; lineIndex++)
            {
                var line = stored.Lines[lineIndex];
                var direction = line.Direction == (int)MessageDirection.Outgoing
                    ? MessageDirection.Outgoing
                    : MessageDirection.Incoming;
                lines.Add(new ChatLine(direction, line.Text,
                    DateTimeOffset.FromUnixTimeMilliseconds(line.AtUnix).LocalDateTime));
            }

            result.Add(new ArchivedConversation(stored.Contact, stored.SendTarget, lines));
        }

        result.Sort(static (left, right) => LastActivity(right).CompareTo(LastActivity(left)));
        return result;
    }

    public void Save(string contact, string sendTarget, IReadOnlyList<ChatLine> lines)
    {
        if (sendTarget.Length == 0)
        {
            return;
        }

        var stored = new StoredConversation { Contact = contact, SendTarget = sendTarget };
        var start = lines.Count > MaxStoredLines ? lines.Count - MaxStoredLines : 0;
        for (var index = start; index < lines.Count; index++)
        {
            var line = lines[index];
            stored.Lines.Add(new StoredLine
            {
                Direction = (int)line.Direction,
                Text = line.Text,
                AtUnix = new DateTimeOffset(line.At).ToUnixTimeMilliseconds(),
            });
        }

        try
        {
            lock (sync)
            {
                if (activeRoot is null)
                {
                    return;
                }

                var path = PathFor(activeRoot, sendTarget);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(stored));
                File.Move(temp, path, true);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"MessageArchive write failed for {sendTarget}: {exception.Message}");
        }
    }

    public void Delete(string sendTarget)
    {
        if (sendTarget.Length == 0)
        {
            return;
        }

        try
        {
            lock (sync)
            {
                if (activeRoot is null)
                {
                    return;
                }

                var path = PathFor(activeRoot, sendTarget);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"MessageArchive delete failed for {sendTarget}: {exception.Message}");
        }
    }

    private static DateTime LastActivity(ArchivedConversation conversation) =>
        conversation.Lines.Count > 0 ? conversation.Lines[conversation.Lines.Count - 1].At : DateTime.MinValue;

    private static StoredConversation? TryLoad(FileInfo file)
    {
        try
        {
            return JsonConvert.DeserializeObject<StoredConversation>(File.ReadAllText(file.FullName));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"MessageArchive load failed for {file.Name}: {exception.Message}");
            return null;
        }
    }

    private static string PathFor(DirectoryInfo directory, string sendTarget)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sendTarget.ToLowerInvariant()));
        var builder = new StringBuilder(hash.Length * 2 + 5);
        for (var index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2"));
        }

        builder.Append(".json");
        return Path.Combine(directory.FullName, builder.ToString());
    }
}

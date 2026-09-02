using Aetherphone.Core.Game;
using Newtonsoft.Json;

namespace Aetherphone.Core.GameChat;

internal sealed class StoredChunk
{
    [JsonProperty("k")] public byte Kind { get; set; }

    [JsonProperty("t")] public string Text { get; set; } = string.Empty;

    [JsonProperty("w")] public string World { get; set; } = string.Empty;

    [JsonProperty("i")] public uint Id { get; set; }

    [JsonProperty("p")] public string Plugin { get; set; } = string.Empty;

    [JsonProperty("r")] public uint TerritoryId { get; set; }

    [JsonProperty("m")] public uint MapId { get; set; }

    [JsonProperty("x")] public int RawX { get; set; }

    [JsonProperty("y")] public int RawY { get; set; }
}

internal sealed class StoredChatLine
{
    [JsonProperty("c")] public string ChannelKey { get; set; } = string.Empty;

    [JsonProperty("n")] public string AuthorName { get; set; } = string.Empty;

    [JsonProperty("w")] public string AuthorWorld { get; set; } = string.Empty;

    [JsonProperty("t")] public string Text { get; set; } = string.Empty;

    [JsonProperty("u")] public long AtUnix { get; set; }

    [JsonProperty("f")] public byte Flags { get; set; }

    [JsonProperty("k")] public List<StoredChunk>? Chunks { get; set; }
}

internal sealed class StoredStream
{
    [JsonProperty("stream")] public string StreamKey { get; set; } = string.Empty;

    [JsonProperty("lines")] public List<StoredChatLine> Lines { get; set; } = new();
}

internal sealed class ChatArchive : IDisposable
{
    private const int MaxStoredLines = ChatLog.MaxLinesPerStream;
    private const int RetentionDays = 30;
    private const long FlushIntervalMilliseconds = 30_000;

    private readonly object sync = new();
    private readonly DirectoryInfo baseDir;
    private readonly Configuration configuration;
    private readonly ChatLog log;
    private readonly MessageArchive legacyTells;
    private readonly HashSet<string> dirty = new(StringComparer.Ordinal);
    private readonly List<string> streamScratch = new(32);
    private DirectoryInfo? activeRoot;
    private long lastFlushMilliseconds;

    public ChatArchive(DirectoryInfo baseDir, Configuration configuration, ChatLog log, MessageArchive legacyTells,
        CharacterWatch characterWatch)
    {
        this.baseDir = baseDir;
        this.configuration = configuration;
        this.log = log;
        this.legacyTells = legacyTells;
        if (!baseDir.Exists)
        {
            baseDir.Create();
        }

        log.Appended += OnAppended;
        characterWatch.Changed += OnCharacterChanged;
    }

    public HistoryPolicy PolicyFor(string streamKey)
    {
        if (!configuration.ArchiveTellsToDisk)
        {
            return HistoryPolicy.Off;
        }

        var channelKey = ChannelKeyOf(streamKey);
        if (configuration.LinkpearlHistoryByChannel.TryGetValue(channelKey, out var stored))
        {
            return Clamp(stored);
        }

        return Clamp(configuration.LinkpearlHistory);
    }

    public void SetPolicy(string channelKey, HistoryPolicy policy)
    {
        configuration.LinkpearlHistoryByChannel[channelKey] = (int)policy;
        configuration.Save();
        if (policy != HistoryPolicy.Off && policy != HistoryPolicy.Session)
        {
            return;
        }

        DeleteChannel(channelKey);
    }

    public void Flush()
    {
        lock (sync)
        {
            if (activeRoot is null || dirty.Count == 0)
            {
                return;
            }

            foreach (var streamKey in dirty)
            {
                WriteStream(streamKey);
            }

            dirty.Clear();
            lastFlushMilliseconds = Environment.TickCount64;
        }
    }

    public void Delete(string streamKey)
    {
        lock (sync)
        {
            dirty.Remove(streamKey);
            if (activeRoot is null)
            {
                return;
            }

            TryDeleteFile(PathFor(activeRoot, streamKey));
        }
    }

    public void DeleteAll()
    {
        lock (sync)
        {
            dirty.Clear();
            if (activeRoot is null)
            {
                return;
            }

            try
            {
                var files = activeRoot.GetFiles("*.json");
                for (var index = 0; index < files.Length; index++)
                {
                    TryDeleteFile(files[index].FullName);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "ChatArchive purge failed");
            }
        }
    }

    public void Dispose()
    {
        log.Appended -= OnAppended;
        Flush();
    }

    private void OnAppended(ChatEntry entry)
    {
        var policy = PolicyFor(entry.StreamKey);
        if (policy is HistoryPolicy.Off or HistoryPolicy.Session)
        {
            return;
        }

        lock (sync)
        {
            if (activeRoot is null)
            {
                return;
            }

            dirty.Add(entry.StreamKey);
            if (Environment.TickCount64 - lastFlushMilliseconds < FlushIntervalMilliseconds)
            {
                return;
            }
        }

        Flush();
    }

    private void OnCharacterChanged(ulong contentId)
    {
        Flush();
        log.Clear();
        lock (sync)
        {
            dirty.Clear();
            activeRoot = null;
            if (contentId == 0)
            {
                return;
            }

            var directory = new DirectoryInfo(Path.Combine(baseDir.FullName, contentId.ToString("x16")));
            if (!directory.Exists)
            {
                directory.Create();
            }

            activeRoot = directory;
            lastFlushMilliseconds = Environment.TickCount64;
        }

        MigrateLegacyTells(contentId);
        Load();
    }

    private void Load()
    {
        FileInfo[] files;
        lock (sync)
        {
            if (activeRoot is null)
            {
                return;
            }

            try
            {
                files = activeRoot.GetFiles("*.json");
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "ChatArchive list failed");
                return;
            }
        }

        for (var index = 0; index < files.Length; index++)
        {
            var stored = TryRead(files[index]);
            if (stored is null || stored.StreamKey.Length == 0 || stored.Lines.Count == 0)
            {
                continue;
            }

            var policy = PolicyFor(stored.StreamKey);
            if (policy is HistoryPolicy.Off or HistoryPolicy.Session)
            {
                TryDeleteFile(files[index].FullName);
                continue;
            }

            var cutoff = policy == HistoryPolicy.Days30 ? DateTime.Now.AddDays(-RetentionDays) : DateTime.MinValue;
            var entries = new List<ChatEntry>(stored.Lines.Count);
            for (var lineIndex = 0; lineIndex < stored.Lines.Count; lineIndex++)
            {
                var line = stored.Lines[lineIndex];
                var at = DateTimeOffset.FromUnixTimeMilliseconds(line.AtUnix).LocalDateTime;
                if (at < cutoff)
                {
                    continue;
                }

                entries.Add(new ChatEntry(log.NextSequence(), line.ChannelKey, line.AuthorName, line.AuthorWorld,
                    line.Text, RestoreChunks(line), at, (ChatEntryFlags)line.Flags));
            }

            log.Restore(stored.StreamKey, entries);
        }
    }

    private void MigrateLegacyTells(ulong contentId)
    {
        if (contentId == 0 || configuration.LinkpearlMigratedCharacters.Contains(contentId))
        {
            return;
        }

        configuration.LinkpearlMigratedCharacters.Add(contentId);
        configuration.Save();
        if (!configuration.ArchiveTellsToDisk)
        {
            return;
        }

        try
        {
            legacyTells.SetCharacter(contentId);
            var conversations = legacyTells.LoadAll();
            for (var index = 0; index < conversations.Count; index++)
            {
                var conversation = conversations[index];
                var streamKey = ChatStreams.ForTell(conversation.SendTarget);
                var stored = new StoredStream { StreamKey = streamKey };
                var world = WorldOf(conversation.SendTarget);
                for (var lineIndex = 0; lineIndex < conversation.Lines.Count; lineIndex++)
                {
                    var line = conversation.Lines[lineIndex];
                    stored.Lines.Add(new StoredChatLine
                    {
                        ChannelKey = GameChannels.TellKey,
                        AuthorName = conversation.Contact,
                        AuthorWorld = world,
                        Text = line.Text,
                        AtUnix = new DateTimeOffset(line.At).ToUnixTimeMilliseconds(),
                        Flags = line.Direction == MessageDirection.Outgoing ? (byte)ChatEntryFlags.Self : (byte)0,
                    });
                }

                WriteStored(stored);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "ChatArchive tell migration failed");
        }
    }

    private void WriteStream(string streamKey)
    {
        var lines = log.Lines(streamKey);
        if (lines.Count == 0)
        {
            return;
        }

        var stored = new StoredStream { StreamKey = streamKey };
        var start = lines.Count > MaxStoredLines ? lines.Count - MaxStoredLines : 0;
        for (var index = start; index < lines.Count; index++)
        {
            var entry = lines[index];
            stored.Lines.Add(new StoredChatLine
            {
                ChannelKey = entry.ChannelKey,
                AuthorName = entry.AuthorName,
                AuthorWorld = entry.AuthorWorld,
                Text = entry.Text,
                AtUnix = new DateTimeOffset(entry.At).ToUnixTimeMilliseconds(),
                Flags = (byte)entry.Flags,
                Chunks = StoreChunks(entry.Chunks),
            });
        }

        WriteStored(stored);
    }

    private void WriteStored(StoredStream stored)
    {
        if (activeRoot is null)
        {
            return;
        }

        try
        {
            var path = PathFor(activeRoot, stored.StreamKey);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(stored));
            File.Move(temp, path, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"ChatArchive write failed for {stored.StreamKey}");
        }
    }

    private void DeleteChannel(string channelKey)
    {
        log.CollectStreams(streamScratch);
        for (var index = 0; index < streamScratch.Count; index++)
        {
            var streamKey = streamScratch[index];
            if (string.Equals(ChannelKeyOf(streamKey), channelKey, StringComparison.Ordinal))
            {
                Delete(streamKey);
            }
        }
    }

    private static List<StoredChunk>? StoreChunks(ChatChunk[] chunks)
    {
        var plain = true;
        for (var index = 0; index < chunks.Length; index++)
        {
            if (!chunks[index].IsPlainText)
            {
                plain = false;
                break;
            }
        }

        if (plain)
        {
            return null;
        }

        var stored = new List<StoredChunk>(chunks.Length);
        for (var index = 0; index < chunks.Length; index++)
        {
            var chunk = chunks[index];
            stored.Add(new StoredChunk
            {
                Kind = (byte)chunk.Kind,
                Text = chunk.Text,
                World = chunk.World,
                Plugin = chunk.Plugin,
                Id = chunk.Id,
                TerritoryId = chunk.TerritoryId,
                MapId = chunk.MapId,
                RawX = chunk.RawX,
                RawY = chunk.RawY,
            });
        }

        return stored;
    }

    private static ChatChunk[] RestoreChunks(StoredChatLine line)
    {
        if (line.Chunks is not { Count: > 0 } chunks)
        {
            return line.Text.Length > 0 ? new[] { ChatChunk.Plain(line.Text) } : Array.Empty<ChatChunk>();
        }

        var restored = new ChatChunk[chunks.Count];
        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            restored[index] = (ChatChunkKind)chunk.Kind switch
            {
                ChatChunkKind.AutoTranslate => ChatChunk.AutoTranslate(chunk.Text),
                ChatChunkKind.Player => ChatChunk.Player(chunk.Text, chunk.World),
                ChatChunkKind.Item => ChatChunk.Item(chunk.Text, chunk.Id),
                ChatChunkKind.Map => ChatChunk.Map(chunk.Text, chunk.TerritoryId, chunk.MapId, chunk.RawX, chunk.RawY),
                ChatChunkKind.Status => ChatChunk.Status(chunk.Text, chunk.Id),
                ChatChunkKind.Quest => ChatChunk.Quest(chunk.Text, chunk.Id),
                ChatChunkKind.PartyFinder => ChatChunk.PartyFinder(chunk.Text, chunk.Id),
                ChatChunkKind.PluginLink => ChatChunk.PluginLink(chunk.Text, chunk.Plugin, chunk.Id),
                _ => ChatChunk.Plain(chunk.Text),
            };
        }

        return restored;
    }

    private static string ChannelKeyOf(string streamKey) =>
        ChatStreams.IsTell(streamKey) ? GameChannels.TellKey : streamKey;

    private static HistoryPolicy Clamp(int stored) =>
        stored is >= (int)HistoryPolicy.Off and <= (int)HistoryPolicy.Forever
            ? (HistoryPolicy)stored
            : HistoryPolicy.Days30;

    private static string WorldOf(string sendTarget)
    {
        var at = sendTarget.IndexOf('@');
        return at >= 0 ? sendTarget[(at + 1)..] : string.Empty;
    }

    private static StoredStream? TryRead(FileInfo file)
    {
        try
        {
            return JsonConvert.DeserializeObject<StoredStream>(File.ReadAllText(file.FullName));
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"ChatArchive load failed for {file.Name}");
            return null;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "ChatArchive delete failed");
        }
    }

    private static string PathFor(DirectoryInfo directory, string streamKey) =>
        HashedFileName.For(directory, streamKey.ToLowerInvariant());
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet;
using Newtonsoft.Json;

namespace Aetherphone.Core.Crypto;

internal sealed class DecryptedHistoryStore : IDisposable
{
    private readonly DirectoryInfo baseDir;
    private readonly AethernetSession session;
    private readonly ConcurrentDictionary<string, RememberedBody> bodies = new(StringComparer.Ordinal);
    private readonly object fileGate = new();
    private string? accountId;
    private long sequence;
    private volatile bool dirty;
    private volatile bool writing;
    private float sinceFlush;

    private const int MaxEntries = 20000;
    private const int EvictionTarget = 16000;
    private const float FlushSeconds = 15f;

    public DecryptedHistoryStore(DirectoryInfo configDirectory, AethernetSession session)
    {
        baseDir = new DirectoryInfo(Path.Combine(configDirectory.FullName, "History"));
        this.session = session;
        session.Changed += OnSessionChanged;
        SetAccount(session.CurrentUser?.Id);
    }

    public bool TryGet(string messageId, out string text)
    {
        if (bodies.TryGetValue(messageId, out var stored))
        {
            text = stored.Text;
            return true;
        }

        text = string.Empty;
        return false;
    }

    public void Remember(string messageId, string text)
    {
        if (accountId is null || messageId.Length == 0 || text.Length == 0)
        {
            return;
        }

        if (bodies.TryGetValue(messageId, out var existing)
            && string.Equals(existing.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        bodies[messageId] = new RememberedBody(Interlocked.Increment(ref sequence), text);
        dirty = true;
    }

    public void Tick(float deltaSeconds)
    {
        if (!dirty || writing)
        {
            return;
        }

        sinceFlush += deltaSeconds;
        if (sinceFlush < FlushSeconds)
        {
            return;
        }

        sinceFlush = 0f;
        Flush();
    }

    public void Flush()
    {
        var owner = accountId;
        if (owner is null || !dirty || writing)
        {
            return;
        }

        writing = true;
        dirty = false;
        var snapshot = Snapshot();
        _ = Task.Run(() =>
        {
            try
            {
                Write(owner, snapshot);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Encryption] saving the readable chat history failed");
            }
            finally
            {
                writing = false;
            }
        });
    }

    private Dictionary<string, string> Snapshot()
    {
        if (bodies.Count > MaxEntries)
        {
            EvictOldest();
        }

        var snapshot = new Dictionary<string, string>(bodies.Count, StringComparer.Ordinal);
        foreach (var pair in bodies)
        {
            snapshot[pair.Key] = pair.Value.Text;
        }

        return snapshot;
    }

    private void EvictOldest()
    {
        var entries = new KeyValuePair<string, RememberedBody>[bodies.Count];
        var index = 0;
        foreach (var pair in bodies)
        {
            if (index == entries.Length)
            {
                break;
            }

            entries[index++] = pair;
        }

        Array.Sort(entries, 0, index, SequenceComparer.Instance);
        var removeCount = index - EvictionTarget;
        for (var removed = 0; removed < removeCount; removed++)
        {
            bodies.TryRemove(entries[removed].Key, out _);
        }
    }

    private void OnSessionChanged()
    {
        SetAccount(session.CurrentUser?.Id);
    }

    private void SetAccount(string? nextAccountId)
    {
        if (string.Equals(nextAccountId, accountId, StringComparison.Ordinal))
        {
            return;
        }

        Flush();
        accountId = nextAccountId;
        bodies.Clear();
        sequence = 0;
        dirty = false;
        if (nextAccountId is null)
        {
            return;
        }

        var owner = nextAccountId;
        _ = Task.Run(() =>
        {
            try
            {
                Load(owner);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Encryption] reading the readable chat history failed");
            }
        });
    }

    private void Load(string owner)
    {
        var path = PathFor(owner);
        string sealedText;
        lock (fileGate)
        {
            if (!File.Exists(path))
            {
                return;
            }

            sealedText = File.ReadAllText(path);
        }

        var plain = LocalKeyProtector.Unprotect(sealedText, owner);
        if (plain is null)
        {
            AepLog.Warning("[Encryption] the stored readable history could not be opened on this device; it is ignored.");
            return;
        }

        var json = Encoding.UTF8.GetString(plain);
        CryptographicOperations.ZeroMemory(plain);
        var stored = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        if (stored is null || !string.Equals(owner, accountId, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var pair in stored)
        {
            bodies.TryAdd(pair.Key, new RememberedBody(Interlocked.Increment(ref sequence), pair.Value));
        }

        AepLog.Info($"[Encryption] {bodies.Count} previously read messages stay readable on this device.");
    }

    private void Write(string owner, Dictionary<string, string> snapshot)
    {
        var json = JsonConvert.SerializeObject(snapshot);
        var plain = Encoding.UTF8.GetBytes(json);
        var sealedText = LocalKeyProtector.Protect(plain, owner);
        CryptographicOperations.ZeroMemory(plain);
        lock (fileGate)
        {
            baseDir.Refresh();
            if (!baseDir.Exists)
            {
                baseDir.Create();
            }

            var path = PathFor(owner);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, sealedText);
            File.Move(temporary, path, true);
        }
    }

    private string PathFor(string owner)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(owner));
        return Path.Combine(baseDir.FullName, Convert.ToHexString(digest) + ".dat");
    }

    public void Clear()
    {
        var owner = accountId;
        bodies.Clear();
        dirty = false;
        if (owner is null)
        {
            return;
        }

        lock (fileGate)
        {
            var path = PathFor(owner);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public int Count => bodies.Count;

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        Flush();
    }

    private readonly record struct RememberedBody(long Sequence, string Text);

    private sealed class SequenceComparer : IComparer<KeyValuePair<string, RememberedBody>>
    {
        public static readonly SequenceComparer Instance = new();

        public int Compare(KeyValuePair<string, RememberedBody> left, KeyValuePair<string, RememberedBody> right)
        {
            return left.Value.Sequence.CompareTo(right.Value.Sequence);
        }
    }
}

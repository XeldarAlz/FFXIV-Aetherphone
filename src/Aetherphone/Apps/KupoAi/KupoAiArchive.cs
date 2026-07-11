using Aetherphone.Core;
using Newtonsoft.Json;

namespace Aetherphone.Apps.KupoAi;

internal sealed class KupoAiArchive
{
    private const int MaxStoredMessages = 200;

    private readonly object sync = new();
    private readonly DirectoryInfo root;

    public KupoAiArchive(DirectoryInfo root)
    {
        this.root = root;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public List<KupoAiConversation> LoadAll()
    {
        var result = new List<KupoAiConversation>();
        FileInfo[] files;
        try
        {
            files = root.GetFiles("*.json");
        }
        catch (Exception exception)
        {
            AepLog.Warning($"KupoAiArchive list failed: {exception.Message}");
            return result;
        }

        for (var index = 0; index < files.Length; index++)
        {
            var conversation = TryLoad(files[index]);
            if (conversation is null || conversation.Id.Length == 0)
            {
                continue;
            }

            result.Add(conversation);
        }

        result.Sort(static (left, right) => right.UpdatedAtUnix.CompareTo(left.UpdatedAtUnix));
        return result;
    }

    public void Save(KupoAiConversation conversation)
    {
        if (conversation.Id.Length == 0)
        {
            return;
        }

        if (conversation.Messages.Count > MaxStoredMessages)
        {
            conversation.Messages.RemoveRange(0, conversation.Messages.Count - MaxStoredMessages);
        }

        try
        {
            lock (sync)
            {
                File.WriteAllText(PathFor(conversation.Id), JsonConvert.SerializeObject(conversation));
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"KupoAiArchive write failed for {conversation.Id}: {exception.Message}");
        }
    }

    public void Delete(string conversationId)
    {
        if (conversationId.Length == 0)
        {
            return;
        }

        try
        {
            lock (sync)
            {
                var path = PathFor(conversationId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"KupoAiArchive delete failed for {conversationId}: {exception.Message}");
        }
    }

    private KupoAiConversation? TryLoad(FileInfo file)
    {
        try
        {
            return JsonConvert.DeserializeObject<KupoAiConversation>(File.ReadAllText(file.FullName));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"KupoAiArchive load failed for {file.Name}: {exception.Message}");
            return null;
        }
    }

    private string PathFor(string conversationId)
    {
        var safeName = new string(Array.FindAll(conversationId.ToCharArray(), static ch => char.IsLetterOrDigit(ch)));
        return Path.Combine(root.FullName, safeName + ".json");
    }
}

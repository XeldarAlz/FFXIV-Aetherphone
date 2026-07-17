namespace Aetherphone.Core;

// Dalamud persists the plugin config with TypeNameHandling.Objects, so every saved object carries a
// "$type" with its full namespace. Moving or renaming a persisted type therefore breaks config
// loading for existing users. This rewrites the stored type names before Dalamud deserializes.
internal static class ConfigMigrations
{
    private static readonly (string Old, string New)[] TypeRenames =
    {
        ("Aetherphone.Apps.DirectMessages.StarredMessage, Aetherphone",
            "Aetherphone.Apps.Message.StarredMessage, Aetherphone"),
    };

    public static string RewriteTypeNames(string json)
    {
        var result = json;
        for (var index = 0; index < TypeRenames.Length; index++)
        {
            result = result.Replace(TypeRenames[index].Old, TypeRenames[index].New, StringComparison.Ordinal);
        }

        return result;
    }

    public static void Run(FileInfo configFile)
    {
        try
        {
            if (!configFile.Exists)
            {
                return;
            }

            var json = File.ReadAllText(configFile.FullName);
            var rewritten = RewriteTypeNames(json);
            if (string.Equals(rewritten, json, StringComparison.Ordinal))
            {
                return;
            }

            var backup = configFile.FullName + ".pre-migration.bak";
            if (!File.Exists(backup))
            {
                File.Copy(configFile.FullName, backup);
            }

            var temp = configFile.FullName + ".tmp";
            File.WriteAllText(temp, rewritten);
            File.Move(temp, configFile.FullName, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Config type migration skipped: {exception.Message}");
        }
    }
}

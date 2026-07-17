namespace Aetherphone.Core;

internal static class ConfigMigrations
{
    private static readonly (string Old, string New)[] TypeRenames =
    {
        ("Aetherphone.Apps.DirectMessages.StarredMessage, Aetherphone",
            "Aetherphone.Core.Message.StarredMessage, Aetherphone"),
        ("Aetherphone.Apps.Message.StarredMessage, Aetherphone",
            "Aetherphone.Core.Message.StarredMessage, Aetherphone"),
        ("Aetherphone.Apps.Calendar.CalendarCustomEvent, Aetherphone",
            "Aetherphone.Core.Calendar.CalendarCustomEvent, Aetherphone"),
        ("Aetherphone.Apps.Notes.PhoneNote, Aetherphone",
            "Aetherphone.Core.Notes.PhoneNote, Aetherphone"),
        ("Aetherphone.Apps.Notes.ReminderItem, Aetherphone",
            "Aetherphone.Core.Notes.ReminderItem, Aetherphone"),
        ("Aetherphone.Apps.Clock.WorldClockEntry, Aetherphone",
            "Aetherphone.Core.Clock.WorldClockEntry, Aetherphone"),
        ("Aetherphone.Apps.Clock.AlarmEntry, Aetherphone",
            "Aetherphone.Core.Clock.AlarmEntry, Aetherphone"),
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

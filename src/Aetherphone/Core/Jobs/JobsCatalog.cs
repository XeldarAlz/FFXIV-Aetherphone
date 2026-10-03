using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Jobs;

internal static class JobsCatalog
{
    private static ClassJobFacts[]? facts;

    public static ClassJobFacts[] Facts()
    {
        if (facts is not null)
        {
            return facts;
        }

        var rows = new List<ClassJobFacts>(48);
        foreach (var job in Plugin.DataManager.GetExcelSheet<ClassJob>())
        {
            if (job.RowId == 0 || job.ExpArrayIndex < 0)
            {
                continue;
            }

            var categoryId = job.ClassJobCategory.RowId;
            if (JobsRoster.BucketFor(job.JobType, job.Role, categoryId) < 0)
            {
                continue;
            }

            rows.Add(new ClassJobFacts(job.RowId, job.ClassJobParent.RowId, job.ExpArrayIndex, job.JobType, job.Role,
                categoryId, job.UIPriority, job.StartingLevel, job.IsLimitedJob, job.UnlockQuest.RowId));
        }

        facts = rows.ToArray();
        return facts;
    }

    public static bool IsQuestCompleted(uint questId)
    {
        if (questId == 0)
        {
            return false;
        }

        try
        {
            return Plugin.DataManager.GetExcelSheet<Quest>().TryGetRow(questId, out var quest) &&
                   Plugin.UnlockState.IsQuestCompleted(quest);
        }
        catch (Exception exception)
        {
            Plugin.Log.Debug(exception, "Jobs: quest completion check failed");
            return false;
        }
    }
}

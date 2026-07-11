using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Report;

internal sealed class ReportPrompt
{
    public required string Title;
    public string? Disclosure;
    public required Action<string?, Action<bool>> Submit;
}

internal static class ReportCategories
{
    public readonly record struct Entry(string Tag, LocString Label);

    public static readonly Entry[] All =
    {
        new("Spam", L.Report.CategorySpam),
        new("Harassment", L.Report.CategoryHarassment),
        new("Hate speech", L.Report.CategoryHateSpeech),
        new("Inappropriate content", L.Report.CategoryInappropriate),
        new("Impersonation", L.Report.CategoryImpersonation),
        new("Scam or fraud", L.Report.CategoryScam),
        new("Other", L.Report.CategoryOther),
    };

    public static string Compose(int categoryIndex, string details)
    {
        var tag = All[categoryIndex].Tag;
        return details.Length > 0 ? $"{tag}: {details}" : tag;
    }
}

internal sealed class ReportService
{
    public ReportPrompt? Active { get; private set; }
    public int CategoryIndex = -1;
    public string ReasonDraft = string.Empty;
    public volatile bool Busy;
    public bool Sent { get; private set; }
    public bool Failed { get; private set; }

    public void Open(ReportPrompt prompt)
    {
        Active = prompt;
        CategoryIndex = -1;
        ReasonDraft = string.Empty;
        Busy = false;
        Sent = false;
        Failed = false;
    }

    public void Submit()
    {
        if (Active is not { } prompt || Busy || Sent || CategoryIndex < 0)
        {
            return;
        }

        Busy = true;
        Failed = false;
        var reason = ReportCategories.Compose(CategoryIndex, ReasonDraft.Trim());
        prompt.Submit(reason, ok =>
        {
            Busy = false;
            Sent = ok;
            Failed = !ok;
        });
    }

    public void Dismiss()
    {
        if (Busy)
        {
            return;
        }

        Active = null;
    }
}

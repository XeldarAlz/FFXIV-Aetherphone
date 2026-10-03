using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Feedback;

internal readonly struct HistoryRow
{
    public readonly string Id;
    public readonly FeedbackCategory Category;
    public readonly FeedbackStatus Status;
    public readonly string[] Preview;
    public readonly string Date;
    public readonly string Photos;
    public readonly bool HasReply;
    public readonly float Height;

    public HistoryRow(string id, FeedbackCategory category, FeedbackStatus status, string[] preview, string date,
        string photos, bool hasReply, float height)
    {
        Id = id;
        Category = category;
        Status = status;
        Preview = preview;
        Date = date;
        Photos = photos;
        HasReply = hasReply;
        Height = height;
    }
}

internal sealed class FeedbackHistoryRows
{
    public const float Pad = 16f;
    public const float PadY = 12f;
    public const float IconSize = 36f;
    public const float IconGap = 12f;
    public const float MetaGap = 4f;
    public const float PillGap = 8f;
    public const int MaxPreviewLines = 2;

    private HistoryRow[] rows = Array.Empty<HistoryRow>();
    private MyFeedbackDto[]? source;
    private int revision = -1;
    private float width = -1f;
    private float scale = -1f;
    private LanguageInfo? language;
    private DateTime day;
    private int clockVersion = -1;

    public float MetaHeight { get; private set; }

    public float LineHeight { get; private set; }

    public static float TextInset(float scale) => (Pad + IconSize + IconGap) * scale;

    public HistoryRow[] Rows(MyFeedbackDto[] items, int itemsRevision, float rowWidth, float currentScale)
    {
        var today = DateTime.Today;
        if (ReferenceEquals(items, source) && itemsRevision == revision && MathF.Abs(rowWidth - width) < 0.5f
            && currentScale == scale && ReferenceEquals(language, Loc.Current) && today == day
            && clockVersion == TimeText.FormatVersion)
        {
            return rows;
        }

        source = items;
        revision = itemsRevision;
        width = rowWidth;
        scale = currentScale;
        language = Loc.Current;
        day = today;
        clockVersion = TimeText.FormatVersion;
        MetaHeight = Typography.LineHeight(TextStyles.Footnote);
        LineHeight = Typography.LineHeight(TextStyles.Subheadline);
        rows = Build(items, rowWidth, currentScale, MetaHeight, LineHeight);
        return rows;
    }

    private static HistoryRow[] Build(MyFeedbackDto[] items, float rowWidth, float scale, float metaHeight,
        float lineHeight)
    {
        if (items.Length == 0)
        {
            return Array.Empty<HistoryRow>();
        }

        var textWidth = MathF.Max(1f, rowWidth - TextInset(scale) - Pad * scale);
        var built = new HistoryRow[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var preview = ClampLines(item.Text ?? string.Empty, textWidth);
            var photoCount = item.ImageUrls?.Length ?? 0;
            var photos = photoCount > 0 ? Loc.Plural(L.Feedback.PhotoCount, photoCount) : string.Empty;
            var height = (PadY * 2f + MetaGap + PillGap + FeedbackArt.PillHeight) * scale + metaHeight
                         + preview.Length * lineHeight;
            built[index] = new HistoryRow(item.Id, FeedbackKinds.Parse(item.Category),
                FeedbackStatuses.Parse(item.Status), preview, TimeText.DayLabel(item.CreatedAtUnix), photos,
                !string.IsNullOrWhiteSpace(item.Reply), height);
        }

        return built;
    }

    private static string[] ClampLines(string text, float maxWidth)
    {
        var flattened = text.Replace('\n', ' ').Trim();
        if (flattened.Length == 0)
        {
            return Array.Empty<string>();
        }

        var wrapped = Typography.WrapText(flattened, TextStyles.Subheadline, maxWidth);
        if (wrapped.Length <= MaxPreviewLines)
        {
            return wrapped;
        }

        var clamped = new string[MaxPreviewLines];
        Array.Copy(wrapped, clamped, MaxPreviewLines);
        clamped[MaxPreviewLines - 1] =
            Typography.FitText(clamped[MaxPreviewLines - 1] + "…", maxWidth, TextStyles.Subheadline);
        return clamped;
    }
}

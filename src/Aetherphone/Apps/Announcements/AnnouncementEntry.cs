using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Announcements;

internal sealed class AnnouncementEntry
{
    private const string MetaSeparator = " · ";
    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public AnnouncementDto Source = null!;
    public string Title = string.Empty;
    public string Body = string.Empty;
    public string Preview = string.Empty;
    public string DayLabel = string.Empty;
    public string Clock = string.Empty;
    public string Meta = string.Empty;
    public DateTime LocalDay;
    public string[] Blocks = Array.Empty<string>();
    public bool[] BlockOpensParagraph = Array.Empty<bool>();

    public readonly ClampedText RowTitle = new();
    public readonly ClampedText RowPreview = new();
    public readonly ClampedText FeatureTitle = new();
    public readonly ClampedText FeaturePreview = new();
    public readonly ClampedText NeighborTitle = new();

    private Spring dot;
    private Spring highlight;
    private bool dotPrimed;

    public void Rebuild(AnnouncementDto source)
    {
        Source = source;
        var text = AnnouncementText.For(source);
        Title = (text.Title ?? string.Empty).Trim();
        Body = (text.Body ?? string.Empty).Trim();
        Preview = Flatten(Body);
        DayLabel = TimeText.DayLabel(source.CreatedAtUnix);
        Clock = TimeText.Clock(source.CreatedAtUnix);
        Meta = DayLabel.Length == 0 ? Clock : string.Concat(DayLabel, MetaSeparator, Clock);
        LocalDay = source.CreatedAtUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(source.CreatedAtUnix).ToLocalTime().Date
            : DateTime.MinValue;
        SplitBlocks(Body);
    }

    public bool Matches(string needle)
    {
        var compare = Loc.Culture.CompareInfo;
        return compare.IndexOf(Title, needle, SearchOptions) >= 0 || compare.IndexOf(Body, needle, SearchOptions) >= 0;
    }

    public float StepDot(bool unread, float deltaSeconds)
    {
        var target = unread ? 1f : 0f;
        if (!dotPrimed)
        {
            dotPrimed = true;
            dot.SnapTo(target);
            return target;
        }

        return Math.Clamp(dot.Step(target, Motion.Appear, deltaSeconds), 0f, 1f);
    }

    public float StepHighlight(float target, float deltaSeconds)
    {
        var smoothTime = target > highlight.Value ? Motion.PressIn : Motion.Release;
        return Math.Clamp(highlight.Step(target, smoothTime, deltaSeconds), 0f, 1f);
    }

    private static string Flatten(string body)
    {
        if (body.IndexOf('\n') < 0)
        {
            return body;
        }

        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', lines);
    }

    private void SplitBlocks(string body)
    {
        if (body.Length == 0)
        {
            Blocks = Array.Empty<string>();
            BlockOpensParagraph = Array.Empty<bool>();
            return;
        }

        var lines = body.Split('\n');
        var blocks = new List<string>(lines.Length);
        var opens = new List<bool>(lines.Length);
        var blankBefore = false;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                blankBefore = true;
                continue;
            }

            opens.Add(blocks.Count > 0 && blankBefore);
            blocks.Add(line);
            blankBefore = false;
        }

        Blocks = blocks.ToArray();
        BlockOpensParagraph = opens.ToArray();
    }
}

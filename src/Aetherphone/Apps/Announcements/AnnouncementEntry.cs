using System.Globalization;
using System.Text;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Announcements;

internal sealed class AnnouncementEntry
{
    private const string MetaSeparator = " · ";
    private const string TextKind = "text";
    private const string ImageKind = "image";
    private const string LinkKind = "link";
    private const string BodySeparator = "\n\n";
    private const string PreviewSeparator = " ";
    private const string LinkSeparator = ": ";
    private const string LinkKeyPrefix = "announcements.link.";
    private const string LinkKeySeparator = ".";
    private const float FallbackAspect = 16f / 9f;
    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public AnnouncementDto Source = null!;
    public string Title = string.Empty;
    public string Body = string.Empty;
    public string Preview = string.Empty;
    public string DayLabel = string.Empty;
    public string Clock = string.Empty;
    public string Meta = string.Empty;
    public DateTime LocalDay;
    public AnnouncementPiece[] Pieces = Array.Empty<AnnouncementPiece>();
    public string[] ImageUrls = Array.Empty<string>();
    public float CoverAspect;

    public readonly ClampedText RowTitle = new();
    public readonly ClampedText RowPreview = new();
    public readonly ClampedText FeatureTitle = new();
    public readonly ClampedText FeaturePreview = new();
    public readonly ClampedText NeighborTitle = new();

    private Spring dot;
    private Spring highlight;
    private bool dotPrimed;

    public bool HasCover => ImageUrls.Length > 0;

    public string CoverUrl => ImageUrls.Length > 0 ? ImageUrls[0] : string.Empty;

    public void Rebuild(AnnouncementDto source)
    {
        Source = source;
        var text = AnnouncementText.For(source);
        Title = (text.Title ?? string.Empty).Trim();
        var blocks = source.Blocks;
        if (blocks is null || blocks.Length == 0)
        {
            RebuildFromBody((text.Body ?? string.Empty).Trim());
        }
        else
        {
            RebuildFromBlocks(blocks);
        }

        DayLabel = TimeText.DayLabel(source.CreatedAtUnix);
        Clock = TimeText.Clock(source.CreatedAtUnix);
        Meta = DayLabel.Length == 0 ? Clock : string.Concat(DayLabel, MetaSeparator, Clock);
        LocalDay = source.CreatedAtUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(source.CreatedAtUnix).ToLocalTime().Date
            : DateTime.MinValue;
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

    private void RebuildFromBody(string body)
    {
        var pieces = new List<AnnouncementPiece>();
        AddLines(pieces, body);
        Pieces = pieces.ToArray();
        ImageUrls = Array.Empty<string>();
        CoverAspect = 0f;
        Body = body;
        Preview = Flatten(body);
    }

    private void RebuildFromBlocks(AnnouncementBlockDto[] blocks)
    {
        var pieces = new List<AnnouncementPiece>(blocks.Length * 2);
        var images = new List<string>();
        var body = new StringBuilder();
        var preview = new StringBuilder();
        for (var blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            var block = blocks[blockIndex];
            switch (block.Kind)
            {
                case TextKind:
                    AddText(pieces, body, preview, AnnouncementText.For(block).Trim());
                    break;
                case ImageKind:
                    AddImage(pieces, images, block);
                    break;
                case LinkKind:
                    AddLink(pieces, body, block, blockIndex);
                    break;
            }
        }

        Pieces = pieces.ToArray();
        ImageUrls = images.ToArray();
        CoverAspect = FirstImageAspect(Pieces);
        Body = body.ToString();
        Preview = preview.ToString();
    }

    private static void AddText(List<AnnouncementPiece> pieces, StringBuilder body, StringBuilder preview,
        string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        AddLines(pieces, text);
        AppendSeparated(body, text, BodySeparator);
        AppendSeparated(preview, Flatten(text), PreviewSeparator);
    }

    private static void AddImage(List<AnnouncementPiece> pieces, List<string> images, AnnouncementBlockDto block)
    {
        var url = block.ImageUrl ?? string.Empty;
        if (url.Length == 0)
        {
            return;
        }

        var aspect = block.Width > 0 && block.Height > 0 ? (float)block.Width / block.Height : FallbackAspect;
        pieces.Add(new AnnouncementPiece(AnnouncementPieceKind.Image, GapBefore(pieces), string.Empty, url, aspect,
            images.Count, string.Empty));
        images.Add(url);
    }

    private void AddLink(List<AnnouncementPiece> pieces, StringBuilder body, AnnouncementBlockDto block,
        int blockIndex)
    {
        var url = (block.Url ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            return;
        }

        var label = AnnouncementText.For(block).Trim();
        var key = string.Concat(LinkKeyPrefix, Source.Id, LinkKeySeparator,
            blockIndex.ToString(CultureInfo.InvariantCulture));
        pieces.Add(new AnnouncementPiece(AnnouncementPieceKind.Link, GapBefore(pieces),
            label.Length == 0 ? url : label, url, 0f, -1, key));
        AppendSeparated(body, label.Length == 0 ? url : string.Concat(label, LinkSeparator, url), BodySeparator);
    }

    private static void AddLines(List<AnnouncementPiece> pieces, string text)
    {
        var blockGap = GapBefore(pieces);
        var lines = text.Split('\n');
        var first = true;
        var blankBefore = false;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                blankBefore = true;
                continue;
            }

            var gap = first ? blockGap : blankBefore ? AnnouncementGap.Paragraph : AnnouncementGap.None;
            pieces.Add(new AnnouncementPiece(AnnouncementPieceKind.Line, gap, line, string.Empty, 0f, -1,
                string.Empty));
            first = false;
            blankBefore = false;
        }
    }

    private static float FirstImageAspect(AnnouncementPiece[] pieces)
    {
        for (var pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
        {
            if (pieces[pieceIndex].Kind == AnnouncementPieceKind.Image)
            {
                return pieces[pieceIndex].Aspect;
            }
        }

        return 0f;
    }

    private static AnnouncementGap GapBefore(List<AnnouncementPiece> pieces) =>
        pieces.Count == 0 ? AnnouncementGap.None : AnnouncementGap.Block;

    private static void AppendSeparated(StringBuilder builder, string text, string separator)
    {
        if (builder.Length > 0)
        {
            builder.Append(separator);
        }

        builder.Append(text);
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
}

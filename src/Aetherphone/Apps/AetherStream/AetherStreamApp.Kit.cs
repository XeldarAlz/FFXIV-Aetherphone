using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float PadX = SocialChrome.CellPadX;
    private const float MediaRowHeight = 62f;
    private const float MediaThumbHeight = 44f;
    private const float ThumbAspect = 16f / 9f;
    private const float ButtonHeight = 46f;
    private const float SmallButtonHeight = 36f;
    private const int MaxSheetActions = 8;

    private static readonly Vector4 WhiteInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 StageBacking = new(0f, 0f, 0f, 0.45f);
    private static readonly TextStyle ButtonStyle = TextStyles.Headline;
    private static readonly TextStyle SmallButtonStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;

    private static void PassiveProgress(ImDrawListPtr drawList, Rect track, float fraction, Vector4 accent,
        Vector4 rail, float alpha)
    {
        var radius = track.Height * 0.5f;
        drawList.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(Palette.WithAlpha(rail, alpha)), radius);
        var fill = Math.Clamp(fraction, 0f, 1f);
        if (fill <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(track.Min, new Vector2(track.Min.X + track.Width * fill, track.Max.Y),
            ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), radius);
    }

    private void CancelPendingJoin()
    {
        if (watchAlong.IsJoining)
        {
            watchAlong.Leave();
        }
    }

    private enum SheetPurpose : byte
    {
        None,
        Player,
        Member,
        QueueRow,
        HistoryRow,
        PlaylistLink,
    }

    private struct TextCache
    {
        private string? template;
        private string? argument;
        private int number;
        private string? text;

        public string Format(string format, string value)
        {
            if (text is null || !string.Equals(template, format, StringComparison.Ordinal)
                || !string.Equals(argument, value, StringComparison.Ordinal))
            {
                template = format;
                argument = value;
                text = string.Format(Loc.Culture, format, value);
            }

            return text;
        }

        public string Format(string format, int value)
        {
            if (text is null || number != value || !string.Equals(template, format, StringComparison.Ordinal))
            {
                template = format;
                number = value;
                text = string.Format(Loc.Culture, format, value);
            }

            return text;
        }

        public string Format(string format, int first, int second)
        {
            var packed = first * 4096 + second;
            if (text is null || number != packed || !string.Equals(template, format, StringComparison.Ordinal))
            {
                template = format;
                number = packed;
                text = string.Format(Loc.Culture, format, first, second);
            }

            return text;
        }

        public string Clock(string format, int seconds)
        {
            if (text is null || number != seconds || !string.Equals(template, format, StringComparison.Ordinal))
            {
                template = format;
                number = seconds;
                text = string.Format(Loc.Culture, format, TimeText.MinutesSeconds(seconds));
            }

            return text;
        }

        public string Upper(string value)
        {
            if (text is null || !string.Equals(template, value, StringComparison.Ordinal))
            {
                template = value;
                text = Loc.Culture.TextInfo.ToUpper(value);
            }

            return text;
        }
    }

    private readonly ActionSheet actions = new();
    private readonly ActionSheet.Item[] actionItems = new ActionSheet.Item[MaxSheetActions];
    private readonly int[] actionCodes = new int[MaxSheetActions];
    private int actionCount;
    private SheetPurpose actionPurpose;
    private string actionTitle = string.Empty;
    private WatchAlongParticipant? actionMember;
    private VideoQueueEntry? actionEntry;
    private VideoHistoryRecord? actionHistory;
    private string actionLink = string.Empty;

    private Vector2 blockOrigin;
    private Vector2 blockSize;
    private int announcedImportStamp;
    private string? pendingLocalFile;
    private string? pendingLocateFile;

    private static void Gap(float pixels) => ImGui.Dummy(new Vector2(0f, pixels * UiScale.Current));

    private Rect BeginBlock(float height)
    {
        var pad = PadX * UiScale.Current;
        blockOrigin = ImGui.GetCursorScreenPos();
        blockSize = new Vector2(ScrollLayout.StableContentWidth(), height);
        return new Rect(new Vector2(blockOrigin.X + pad, blockOrigin.Y),
            new Vector2(blockOrigin.X + blockSize.X - pad, blockOrigin.Y + height));
    }

    private void EndBlock()
    {
        ImGui.SetCursorScreenPos(blockOrigin);
        ImGui.Dummy(blockSize);
    }

    private static void SectionLabel(string label) => SocialChrome.DrawSectionLabel(label, Ink, SectionStyle);

    private bool PrimaryButton(Rect rect, string label, bool enabled = true) =>
        SocialPill.Accent(ImGui.GetWindowDrawList(), rect, label, Ink, ButtonStyle, rect.Height * 0.5f, enabled)
        && enabled;

    private static bool QuietButton(Rect rect, string label) =>
        SocialPill.Flat(ImGui.GetWindowDrawList(), rect, label, Ink.ButtonFill, Ink.ButtonHover, default,
            Ink.TitleInk, ButtonStyle, rect.Height * 0.5f);

    private static bool DangerButton(Rect rect, string label) =>
        SocialPill.Flat(ImGui.GetWindowDrawList(), rect, label, Palette.WithAlpha(Ink.Danger, 0.12f),
            Palette.WithAlpha(Ink.Danger, 0.20f), default, Ink.Danger, ButtonStyle, rect.Height * 0.5f);

    private static bool SmallButton(Rect rect, string label, bool accent)
    {
        var drawList = ImGui.GetWindowDrawList();
        return accent
            ? SocialPill.Accent(drawList, rect, label, Ink, SmallButtonStyle, rect.Height * 0.5f)
            : SocialPill.Flat(drawList, rect, label, Ink.ButtonFill, Ink.ButtonHover, default, Ink.TitleInk,
                SmallButtonStyle, rect.Height * 0.5f);
    }

    private static bool TextLink(Vector2 center, string label, Vector4 ink)
    {
        var scale = UiScale.Current;
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var half = new Vector2(size.X * 0.5f + 10f * scale, size.Y * 0.5f + 7f * scale);
        var hovered = UiInteract.Hover(center - half, center + half);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), center, label, hovered ? Ink.TitleInk : ink,
            TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - half, center + half, hovered);
    }

    private void DrawThumb(ImDrawListPtr drawList, Rect rect, string? url, string? thumbnailUrl, float rounding)
    {
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Ink.ThumbFill));
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, url, thumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, rect.Min, rect.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        AppSkin.Icon(drawList, rect.Center, IconGlyph.Of(FontAwesomeIcon.Play), Ink.FaintInk, 0.7f);
    }

    private FeedCellScope BeginMediaRow(ImDrawListPtr drawList, string? url, string? thumbnailUrl, string title,
        string subtitle, float trailingReserve, bool interactive, bool highlighted = false)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, MediaRowHeight * scale, ui.HoverWash, interactive);
        var row = cell.Bounds;
        if (highlighted)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(Ink.AccentWash));
        }

        var thumbHeight = MediaThumbHeight * scale;
        var thumbMin = new Vector2(row.Min.X + PadX * scale, row.Center.Y - thumbHeight * 0.5f);
        var thumb = new Rect(thumbMin, thumbMin + new Vector2(thumbHeight * ThumbAspect, thumbHeight));
        DrawThumb(drawList, thumb, url, thumbnailUrl, Metrics.Radius.Sm * scale);

        var textLeft = thumb.Max.X + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, row.Max.X - (PadX + trailingReserve) * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - titleHeight * 0.5f),
                Typography.FitText(title, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
                TextStyles.BodyEmphasized);
            return cell;
        }

        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(title, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(subtitle, textWidth, TextStyles.Footnote), Ink.MutedInk, TextStyles.Footnote);
        return cell;
    }

    private void EndMediaRow(ImDrawListPtr drawList, in FeedCellScope cell) =>
        FeedCell.End(drawList, cell, Ink.Hairline);

    private static void TintedCard(ImDrawListPtr drawList, Rect card, Vector4 tint)
    {
        var rounding = Metrics.Radius.Card * UiScale.Current;
        Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.10f)));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.30f)),
            1f);
    }

    private static void GlassCard(ImDrawListPtr drawList, Rect card)
    {
        var rounding = Metrics.Radius.Card * UiScale.Current;
        Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(Ink.FieldFill));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(Ink.ChipStroke), 1f);
    }

    private void BeginActions(SheetPurpose purpose, string title)
    {
        actionPurpose = purpose;
        actionTitle = title;
        actionCount = 0;
    }

    private void AddAction(int code, string label, string glyph = "", bool danger = false, bool selected = false,
        bool checkable = false)
    {
        if (actionCount >= MaxSheetActions)
        {
            return;
        }

        actionCodes[actionCount] = code;
        actionItems[actionCount] = new ActionSheet.Item(label, glyph, danger, selected, checkable);
        actionCount++;
    }

    private void DrawActions(Rect bounds)
    {
        var picked = actions.Draw(bounds, ActionSheetStyle.From(ui), actionItems.AsSpan(0, actionCount),
            Loc.T(L.Common.Cancel), false, actionTitle);
        if (picked < 0 || picked >= actionCount)
        {
            return;
        }

        var code = actionCodes[picked];
        switch (actionPurpose)
        {
            case SheetPurpose.Player:
                HandlePlayerAction(code);
                return;
            case SheetPurpose.Member:
                HandleMemberAction(code);
                return;
            case SheetPurpose.QueueRow:
                HandleQueueRowAction(code);
                return;
            case SheetPurpose.HistoryRow:
                HandleHistoryRowAction(code);
                return;
            case SheetPurpose.PlaylistLink:
                HandlePlaylistLinkAction(code);
                return;
        }
    }

    private void ConsumePickedFiles()
    {
        if (Interlocked.Exchange(ref pendingLocalFile, null) is { } localPath)
        {
            SubmitInput(localPath);
        }

        if (Interlocked.Exchange(ref pendingLocateFile, null) is { } locatedPath)
        {
            watchAlong.LocateLocalMedia(locatedPath);
        }
    }

    private void AnnounceImports()
    {
        var result = queue.LastImport;
        if (result.Stamp == announcedImportStamp)
        {
            return;
        }

        announcedImportStamp = result.Stamp;
        if (result.Failed)
        {
            ShellToast.Show(Loc.T(L.AetherStream.PlaylistImportFailed));
            return;
        }

        ShellToast.Show(string.Format(Loc.Culture,
            Loc.T(result.Truncated ? L.AetherStream.PlaylistImportedCapped : L.AetherStream.PlaylistImported),
            result.Added));
    }
}

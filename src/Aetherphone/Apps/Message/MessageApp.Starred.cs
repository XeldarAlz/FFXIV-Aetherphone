using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float StarredRowHeight = 68f;
    private const float StarredRowTitleTop = 12f;
    private const float UnstarRadius = 14f;
    private const float UnstarGlyph = 18f;

    private readonly List<StarredMessage> starredRows = new();

    private void DrawStarred(Rect area, string? conversationId)
    {
        var scale = UiScale.Current;
        starredRows.Clear();
        var starred = configuration.MessageStarredMessages;
        var subtitle = string.Empty;
        for (var index = starred.Count - 1; index >= 0; index--)
        {
            if (conversationId is null || starred[index].ConversationId == conversationId)
            {
                starredRows.Add(starred[index]);
                if (subtitle.Length == 0 && conversationId is not null)
                {
                    subtitle = starred[index].ConversationTitle;
                }
            }
        }

        DrawScreenHeader(area, Loc.T(L.Message.StarredTitle), subtitle: subtitle);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (starredRows.Count == 0)
        {
            EmptyState.Draw(body, ui, PhoneIcons.Star, Loc.T(L.Message.NoStarred), string.Empty);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            for (var index = 0; index < starredRows.Count; index++)
            {
                DrawStarredRow(drawList, starredRows[index], conversationId is null);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawStarredRow(ImDrawListPtr drawList, StarredMessage entry, bool showConversation)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, StarredRowHeight * scale, ui.HoverWash);
        var pad = CellPadX * scale;
        var textLeft = cell.Bounds.Min.X + pad;
        var textRight = cell.Bounds.Max.X - pad;
        var title = showConversation
            ? string.Concat(entry.SenderName, " · ", entry.ConversationTitle)
            : entry.SenderName;
        var timeLabel = TimeText.DayLabel(entry.CreatedAtUnix);
        var timeSize = Typography.Measure(timeLabel, RowMetaStyle);
        var lineTop = cell.Bounds.Min.Y + StarredRowTitleTop * scale;
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textRight - timeSize.X, lineTop + (titleHeight - timeSize.Y) * 0.5f),
            timeLabel, ink.MutedInk, RowMetaStyle);
        var titleRight = textRight - timeSize.X - TimeGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, lineTop),
            Typography.FitText(title, MathF.Max(1f, titleRight - textLeft), RowTitleStyle), ink.TitleInk, RowTitleStyle);

        var subTop = lineTop + titleHeight + RowLineGap * scale;
        var subHeight = Typography.LineHeight(RowSubStyle);
        var subCenterY = subTop + subHeight * 0.5f;
        var unstarCenter = new Vector2(textRight - UnstarRadius * scale, subCenterY);
        var unstarExtent = new Vector2(UnstarRadius * scale, UnstarRadius * scale);
        var overUnstar = UiInteract.Hover(unstarCenter - unstarExtent, unstarCenter + unstarExtent);
        PhoneIcon.Draw(drawList, unstarCenter, PhoneIcons.StarFilled, overUnstar ? ink.AccentLink : TintGold,
            UnstarGlyph * scale);
        HoverTooltip.Show(new Rect(unstarCenter - unstarExtent, unstarCenter + unstarExtent),
            Loc.T(L.Message.UnstarAction), HoverLabelSide.Above);
        var previewLeft = textLeft;
        var kindGlyph = entry.Kind switch
        {
            1 => PhoneIcons.Camera,
            3 => PhoneIcons.Microphone,
            ChatText.LocationKind => PhoneIcons.MapPin,
            ChatText.MusterKind => PhoneIcons.Compass,
            _ => string.Empty,
        };
        if (kindGlyph.Length > 0)
        {
            PhoneIcon.Draw(drawList, new Vector2(previewLeft + PreviewGlyph * 0.5f * scale, subCenterY), kindGlyph,
                ink.MutedInk, PreviewGlyph * scale);
            previewLeft += PreviewGlyph * scale + PreviewGlyphGap * scale;
        }

        var previewRight = unstarCenter.X - UnstarRadius * scale - RowTrailingGap * scale;
        Typography.Draw(drawList, new Vector2(previewLeft, subTop),
            Typography.FitText(entry.Preview, MathF.Max(1f, previewRight - previewLeft), RowSubStyle), ink.MutedInk,
            RowSubStyle);
        if (UiInteract.Click(unstarCenter - unstarExtent, unstarCenter + unstarExtent, overUnstar))
        {
            configuration.MessageStarredMessages.Remove(entry);
            configuration.Save();
            threadView.InvalidateTranscript();
        }
        else if (cell.Tapped && !overUnstar)
        {
            router.Push(MessageRoute.Thread(entry.ConversationId));
            threadView.RequestScrollTo(entry.MessageId);
        }

        DrawRowHairline(drawList, cell, textLeft);
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private void DrawStarred(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.StarredTitle), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var starred = configuration.MessageStarredMessages;
        if (starred.Count == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Star, Loc.T(L.Message.NoStarred), string.Empty);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = starred.Count - 1; index >= 0; index--)
            {
                DrawStarredRow(starred[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawStarredRow(StarredMessage entry, float scale)
    {
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, rowMax, 14f * scale);
        var pad = 14f * scale;
        var title = string.Concat(entry.SenderName, "  ·  ", entry.ConversationTitle);
        var timeLabel = TimeText.DayLabel(entry.CreatedAtUnix);
        var timeSize = Typography.Measure(timeLabel, TextStyles.Caption1);
        Typography.Draw(new Vector2(origin.X + width - pad - timeSize.X, origin.Y + 11f * scale), timeLabel,
            ui.MutedInk, TextStyles.Caption1);
        var textWidth = width - pad * 2f - timeSize.X - 10f * scale;
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + 10f * scale),
            Typography.FitText(title, textWidth, 0.88f, FontWeight.SemiBold), theme.TextStrong, 0.88f,
            FontWeight.SemiBold);
        var previewLeft = origin.X + pad;
        if (entry.Kind is 1 or 3 or ChatText.LocationKind or ChatText.MusterKind)
        {
            var glyph = entry.Kind switch
            {
                3 => FontAwesomeIcon.Microphone,
                ChatText.LocationKind => FontAwesomeIcon.MapMarkerAlt,
                ChatText.MusterKind => FontAwesomeIcon.Bullhorn,
                _ => FontAwesomeIcon.Camera,
            };
            AppSkin.Icon(new Vector2(previewLeft + 6f * scale, origin.Y + 38f * scale), glyph.ToIconString(),
                ui.MutedInk, 0.62f);
            previewLeft += 16f * scale;
        }

        var unstarRadius = 12f * scale;
        var unstarCenter = new Vector2(origin.X + width - pad - unstarRadius + 4f * scale,
            origin.Y + rowHeight - 16f * scale);
        Typography.Draw(new Vector2(previewLeft, origin.Y + 31f * scale),
            Typography.FitText(entry.Preview, unstarCenter.X - unstarRadius - 8f * scale - previewLeft, 0.82f,
                FontWeight.Regular), ui.MutedInk, 0.82f);
        var unstarHit = new Vector2(unstarRadius, unstarRadius);
        var overUnstar = UiInteract.Hover(unstarCenter - unstarHit, unstarCenter + unstarHit);
        var unstarClicked = ui.IconButton(unstarCenter, unstarRadius, FontAwesomeIcon.Star.ToIconString(),
            ui.Accent, AppSkin.Transparent, 0.8f, Loc.T(L.Message.UnstarAction));
        if (unstarClicked)
        {
            configuration.MessageStarredMessages.Remove(entry);
            configuration.Save();
        }
        else if (!overUnstar && UiInteract.HoverClick(origin, rowMax))
        {
            router.Push(MessageRoute.Thread(entry.ConversationId));
            threadView.RequestScrollTo(entry.MessageId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }
}

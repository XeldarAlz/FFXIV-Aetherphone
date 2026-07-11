using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Apps.DirectMessages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private string? reactorsFor;
    private volatile ReactorDto[]? reactors;

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
        if (entry.Kind is 1 or 3)
        {
            AppSkin.Icon(new Vector2(previewLeft + 6f * scale, origin.Y + 38f * scale),
                (entry.Kind == 3 ? FontAwesomeIcon.Microphone : FontAwesomeIcon.Camera).ToIconString(),
                ui.MutedInk, 0.62f);
            previewLeft += 16f * scale;
        }

        var unstarRadius = 12f * scale;
        var unstarCenter = new Vector2(origin.X + width - pad - unstarRadius + 4f * scale,
            origin.Y + rowHeight - 16f * scale);
        Typography.Draw(new Vector2(previewLeft, origin.Y + 31f * scale),
            Typography.FitText(entry.Preview, unstarCenter.X - unstarRadius - 8f * scale - previewLeft, 0.82f,
                FontWeight.Regular), ui.MutedInk, 0.82f);
        var unstarClicked = ui.IconButton(unstarCenter, unstarRadius, FontAwesomeIcon.Star.ToIconString(),
            ui.Accent, AppSkin.Transparent, 0.8f, Loc.T(L.Message.UnstarAction));
        if (unstarClicked)
        {
            configuration.MessageStarredMessages.Remove(entry);
            configuration.Save();
        }
        else if (UiInteract.HoverClick(origin, rowMax))
        {
            router.Push(MessageRoute.Thread(entry.ConversationId));
            transcript.RequestScrollTo(entry.MessageId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawReactions(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.ReactionsTitle), back);
        if (reactorsFor != messageId)
        {
            reactorsFor = messageId;
            reactors = null;
            store.LoadReactions(messageId, result => reactors = result);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = reactors;
        if (snapshot is null)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                Loc.T(L.Common.Loading), ui.MutedInk);
            return;
        }

        if (snapshot.Length == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.ThumbsUp, Loc.T(L.Message.ReactionsTitle), string.Empty);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawReactorRow(messageId, snapshot[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawReactorRow(string messageId, ReactorDto reactor, float scale)
    {
        var mine = reactor.UserId == store.MyUserId;
        var rowHeight = 54f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, rowMax, 14f * scale);
        var pad = 12f * scale;
        var radius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = mine
            ? Loc.T(L.Message.You)
            : reactor.DisplayName.Length > 0 ? reactor.DisplayName : reactor.Handle;
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, reactor.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        if (mine)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 10f * scale), label, theme.TextStrong, 1f,
                FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), Loc.T(L.Message.TapToRemove),
                ui.MutedInk, TextStyles.Footnote);
        }
        else
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale), label,
                theme.TextStrong, 1f, FontWeight.SemiBold);
        }

        var tokenColor = ReactionArt.Color(reactor.Token);
        AppSkin.Icon(new Vector2(origin.X + width - pad - 10f * scale, origin.Y + rowHeight * 0.5f),
            ReactionArt.Glyph(reactor.Token), tokenColor, 1f);
        if (mine && UiInteract.HoverClick(origin, rowMax))
        {
            store.SetReaction(messageId, string.Empty);
            var snapshot = reactors;
            if (snapshot is not null)
            {
                var next = new List<ReactorDto>(snapshot.Length);
                for (var index = 0; index < snapshot.Length; index++)
                {
                    if (snapshot[index].UserId != store.MyUserId)
                    {
                        next.Add(snapshot[index]);
                    }
                }

                reactors = next.ToArray();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }
}

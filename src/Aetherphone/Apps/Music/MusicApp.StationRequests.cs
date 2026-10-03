using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float RequestRowHeight = 58f;
    private const float RequestAvatarRadius = 15f;
    private const float RequestActionHeight = 28f;
    private const float RequestFieldHeight = 40f;
    private const float RequestCardPad = 14f;
    private const float RequestToggleRowHeight = 50f;

    private readonly RadioFittedText requestToggleFit = new();
    private readonly RadioFittedText requestNoticeTitleFit = new();
    private readonly RadioFittedText requestNoticeSubtitleFit = new();
    private readonly RadioFittedText ownRequestStatusFit = new();
    private readonly RadioFittedText ownRequestTextFit = new();
    private RadioRequestEntry[] requestBuffer = Array.Empty<RadioRequestEntry>();
    private int[] requestOrder = Array.Empty<int>();
    private string[] requestBylines = Array.Empty<string>();
    private RadioFittedText[] requestTitles = Array.Empty<RadioFittedText>();
    private RadioFittedText[] requestBylineFits = Array.Empty<RadioFittedText>();
    private int requestCount;
    private int requestsVersion = -1;
    private LanguageInfo? requestsLanguage;
    private string requestDraft = string.Empty;

    private void SyncRequests()
    {
        if (requestsVersion == room.Version && ReferenceEquals(requestsLanguage, Loc.Current))
        {
            return;
        }

        requestsVersion = room.Version;
        requestsLanguage = Loc.Current;
        requestCount = room.RequestCount;
        if (requestBuffer.Length < requestCount)
        {
            requestBuffer = new RadioRequestEntry[requestCount];
            requestOrder = new int[requestCount];
            requestBylines = new string[requestCount];
            requestTitles = GrowFits(requestTitles, requestCount);
            requestBylineFits = GrowFits(requestBylineFits, requestCount);
        }

        for (var index = 0; index < requestCount; index++)
        {
            var entry = room.RequestAt(index);
            requestBuffer[index] = entry;
            requestBylines[index] = Loc.T(L.Music.Live.RequestedBy, entry.DisplayName);
        }

        RadioLiveRules.OrderRequests(requestBuffer, requestCount, requestOrder);
    }

    private static RadioFittedText[] GrowFits(RadioFittedText[] existing, int length)
    {
        var grown = new RadioFittedText[length];
        for (var index = 0; index < grown.Length; index++)
        {
            grown[index] = index < existing.Length ? existing[index] : new RadioFittedText();
        }

        return grown;
    }

    private RadioRequestEntry? OwnRequest()
    {
        for (var index = 0; index < requestCount; index++)
        {
            if (requestBuffer[index].IsMine)
            {
                return requestBuffer[index];
            }
        }

        return null;
    }

    private void DrawStationRequests(Rect panel, float scale)
    {
        SyncRequests();
        ImGui.PushID("radio.requests");
        using (AppSurface.Begin(panel))
        {
            if (room.IsDj)
            {
                DrawRequestsToggle(scale);
            }
            else
            {
                DrawRequestComposer(scale);
            }

            DrawRequestList(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ImGui.PopID();
    }

    private void DrawRequestsToggle(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = RequestToggleRowHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        var pad = RequestCardPad * scale;
        var toggleSize = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
        var toggleMin = new Vector2(max.X - pad - toggleSize.X, (min.Y + max.Y - toggleSize.Y) * 0.5f);
        var label = requestToggleFit.Fit(Loc.T(L.Music.Live.RequestsToggle), toggleMin.X - pad - min.X - pad,
            TextStyles.Body);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(min.X + pad, (min.Y + max.Y - labelSize.Y) * 0.5f), label, ui.TitleInk,
            TextStyles.Body);
        var enabled = room.IsAttached;
        var wanted = Toggle.Draw("radio.requests.open", new Rect(toggleMin, toggleMin + toggleSize), room.RequestsOpen,
            theme, enabled ? 1f : 0.5f, enabled);
        if (wanted != room.RequestsOpen)
        {
            room.SetRequestsOpen(wanted);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawRequestComposer(float scale)
    {
        var lockState = ChatLock();
        if (lockState != RadioComposerLock.None)
        {
            DrawRequestNotice(scale, FontAwesomeIcon.Lock, LockText(lockState), string.Empty);
            return;
        }

        if (!room.RequestsOpen)
        {
            DrawRequestNotice(scale, FontAwesomeIcon.Pause, Loc.T(L.Music.Live.RequestsClosedTitle),
                Loc.T(L.Music.Live.RequestsClosedSub));
            return;
        }

        if (OwnRequest() is { } own)
        {
            DrawOwnRequest(scale, own);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = RequestFieldHeight * scale;
        var buttonLabel = Loc.T(L.Music.Live.RequestSend);
        var buttonWidth = AppSkin.PillWidthFor(buttonLabel, RequestActionHeight * scale + Metrics.Space.Xs * scale);
        var fieldRect = new Rect(origin, new Vector2(origin.X + width - buttonWidth - Metrics.Space.Sm * scale,
            origin.Y + height));
        var submitted = SubmitField.Draw(fieldRect, "##radioRequest", Loc.T(L.Music.Live.RequestHint),
            ref requestDraft, theme, RadioRoomSession.MaxRequestLength, FontAwesomeIcon.Music);
        var canRequest = requestDraft.AsSpan().Trim().Length > 0 && room.CanRequest();
        var buttonHeight = RequestActionHeight * scale + Metrics.Space.Xs * scale;
        var buttonMin = new Vector2(fieldRect.Max.X + Metrics.Space.Sm * scale, origin.Y + (height - buttonHeight) * 0.5f);
        var tapped = AppSkin.PillButton(new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight)),
            buttonLabel, true, canRequest, theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        if ((submitted || tapped) && canRequest && room.Request(requestDraft))
        {
            requestDraft = string.Empty;
            UiFeedback.Play(UiSound.MessageSent);
        }
    }

    private void DrawRequestNotice(float scale, FontAwesomeIcon icon, string title, string subtitle)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = RequestCardPad * scale;
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var height = pad * 2f + titleHeight + subtitleHeight;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        var glyphCenter = new Vector2(min.X + pad + 8f * scale, min.Y + height * 0.5f);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(icon), ui.MutedInk, 0.8f);
        var textLeft = glyphCenter.X + Metrics.Space.Lg * scale;
        var textWidth = max.X - pad - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + pad), requestNoticeTitleFit.Fit(title, textWidth,
            TextStyles.SubheadlineEmphasized), ui.TitleInk, TextStyles.SubheadlineEmphasized);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + pad + titleHeight),
                requestNoticeSubtitleFit.Fit(subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawOwnRequest(float scale, RadioRequestEntry own)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = RequestCardPad * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Footnote);
        var bodyHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var height = pad * 2f + titleHeight + bodyHeight;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        var accepted = own.State == RadioRequestState.Accepted;
        var withdrawLabel = Loc.T(L.Music.Live.RequestWithdraw);
        var buttonHeight = RequestActionHeight * scale;
        var buttonWidth = AppSkin.PillWidthFor(withdrawLabel, buttonHeight);
        var buttonMin = new Vector2(max.X - pad - buttonWidth, min.Y + (height - buttonHeight) * 0.5f);
        var textLeft = min.X + pad;
        var textWidth = buttonMin.X - Metrics.Space.Sm * scale - textLeft;
        var status = accepted ? Loc.T(L.Music.Live.RequestAcceptedYours) : Loc.T(L.Music.Live.RequestYours);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + pad), ownRequestStatusFit.Fit(status, textWidth,
            TextStyles.Footnote), accepted ? ui.Accent : ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + pad + titleHeight),
            ownRequestTextFit.Fit(own.Text, textWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var withdraw = !accepted && ui.GhostButton(new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight)),
            withdrawLabel);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        if (withdraw)
        {
            room.SkipRequest(own.RequestId);
        }
    }

    private void DrawRequestList(float scale)
    {
        if (requestCount == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var height = 120f * scale;
            EmptyState.Draw(new Rect(origin, origin + new Vector2(width, height)), ui, FontAwesomeIcon.Music,
                Loc.T(L.Music.Live.RequestsEmpty), Loc.T(L.Music.Live.RequestsEmptySub));
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        var acceptedHeading = false;
        var pendingHeading = false;
        for (var orderIndex = 0; orderIndex < requestCount; orderIndex++)
        {
            var bufferIndex = requestOrder[orderIndex];
            var entry = requestBuffer[bufferIndex];
            if (entry.State == RadioRequestState.Accepted && !acceptedHeading)
            {
                acceptedHeading = true;
                SectionHeader.Draw(ui, Loc.T(L.Music.Live.AcceptedSection), false, 0f);
            }
            else if (entry.State == RadioRequestState.Pending && !pendingHeading)
            {
                pendingHeading = true;
                SectionHeader.Draw(ui, Loc.T(L.Music.Live.PendingSection), false, 0f);
            }

            ImGui.PushID(unchecked((int)entry.RequestId));
            DrawRequestRow(entry, bufferIndex, scale);
            ImGui.PopID();
        }
    }

    private void DrawRequestRow(RadioRequestEntry entry, int bufferIndex, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = RequestRowHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = RequestAvatarRadius * scale;
        var avatarCenter = new Vector2(min.X + avatarRadius, min.Y + height * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, entry.DisplayName, string.Empty,
            entry.AvatarUrl, images, lodestone, 0.8f, 24);

        var actionsLeft = max.X;
        var overChild = false;
        var accept = false;
        var skip = false;
        if (room.IsDj)
        {
            var buttonHeight = RequestActionHeight * scale;
            var buttonTop = min.Y + (height - buttonHeight) * 0.5f;
            var skipLabel = Loc.T(L.Music.Live.RequestSkip);
            var primaryLabel = entry.State == RadioRequestState.Accepted
                ? Loc.T(L.Music.Live.RequestPlayed)
                : Loc.T(L.Music.Live.RequestAccept);
            var skipWidth = AppSkin.PillWidthFor(skipLabel, buttonHeight);
            var primaryWidth = AppSkin.PillWidthFor(primaryLabel, buttonHeight);
            var skipMin = new Vector2(max.X - skipWidth, buttonTop);
            var primaryMin = new Vector2(skipMin.X - Metrics.Space.Xs * scale - primaryWidth, buttonTop);
            var skipRect = new Rect(skipMin, skipMin + new Vector2(skipWidth, buttonHeight));
            var primaryRect = new Rect(primaryMin, primaryMin + new Vector2(primaryWidth, buttonHeight));
            overChild = UiInteract.Hover(skipRect.Min, skipRect.Max) || UiInteract.Hover(primaryRect.Min, primaryRect.Max);
            accept = ui.PillButton(primaryRect, primaryLabel, true);
            skip = ui.GhostButton(skipRect, skipLabel);
            actionsLeft = primaryMin.X - Metrics.Space.Sm * scale;
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered && !overChild)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(ui.HoverWash), Metrics.Radius.Md * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var isLink = RadioLiveRules.IsLink(entry.Text);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var glyphReserve = isLink ? Metrics.Space.Lg * scale : 0f;
        var textWidth = MathF.Max(1f, actionsLeft - textLeft - glyphReserve);
        var titleY = min.Y + height * 0.5f - Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        if (isLink)
        {
            AppSkin.Icon(drawList, new Vector2(textLeft + 6f * scale,
                    titleY + Typography.LineHeight(TextStyles.SubheadlineEmphasized) * 0.5f),
                IconGlyph.Of(FontAwesomeIcon.Link), ui.Accent, 0.6f);
        }

        Typography.Draw(drawList, new Vector2(textLeft + glyphReserve, titleY),
            requestTitles[bufferIndex].Fit(entry.Text, textWidth, TextStyles.SubheadlineEmphasized),
            entry.IsMine ? ui.Accent : ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + height * 0.5f + Metrics.Space.Xxs * 0.5f * scale),
            requestBylineFits[bufferIndex].Fit(requestBylines[bufferIndex], actionsLeft - textLeft, TextStyles.Caption1), ui.MutedInk,
            TextStyles.Caption1);
        FeedCell.Hairline(drawList, textLeft, max.X, max.Y, ui.Hairline);
        ImGui.Dummy(new Vector2(width, height));

        if (accept)
        {
            if (entry.State == RadioRequestState.Accepted)
            {
                room.MarkPlayed(entry.RequestId);
            }
            else
            {
                room.AcceptRequest(entry.RequestId);
            }

            return;
        }

        if (skip)
        {
            room.SkipRequest(entry.RequestId);
            return;
        }

        if (!overChild && UiInteract.Click(min, max, hovered))
        {
            OpenSearchFor(entry.Text.Trim());
        }
    }
}

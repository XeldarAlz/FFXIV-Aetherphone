using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Dev;

internal sealed partial class DevApp
{
    private readonly PhotoZoomView imageZoom = new();

    private const float OlderLoadThreshold = 48f;
    private const int OlderSettleFrames = 2;
    private const float OlderRestoreTimeout = 20f;

    private void DrawChat(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var delta = ImGui.GetIO().DeltaTime;
        store.MarkChatSeen();
        var composerHeight = 52f * scale;
        var listRect = new Rect(area.Min, new Vector2(area.Max.X, area.Max.Y - composerHeight));
        var snapshot = store.Messages;
        SyncChatEntrances(snapshot, delta);
        if (store.LoadingOlder)
        {
            olderSpinnerPhase += delta;
        }

        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    store.LoadingChat && !store.ChatLoaded ? "Loading" : "No messages yet",
                    AppPalettes.Dev.MutedInk);
            }
            else
            {
                SyncChatFollow();
                MaybeLoadOlderChat(snapshot.Length);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawChatMessage(snapshot, index);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                if (followChatBottom)
                {
                    ImGui.SetScrollHereY(1f);
                }

                ApplyOlderChatRestore(snapshot.Length, delta);
                if (store.LoadingOlder)
                {
                    DrawOlderChatLoading(listRect);
                }
            }
        }

        DrawChatComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max));
    }

    private void MaybeLoadOlderChat(int count)
    {
        if (olderAnchorFromBottom >= 0f || !store.HasMoreOlder || store.LoadingOlder || followChatBottom)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        if (ImGui.GetScrollMaxY() <= 0f || ImGui.GetScrollY() > OlderLoadThreshold * scale)
        {
            return;
        }

        olderAnchorFromBottom = ImGui.GetScrollMaxY() - ImGui.GetScrollY();
        olderBaselineCount = count;
        olderSettleFrames = 0;
        olderElapsed = 0f;
        store.LoadOlder();
    }

    private void ApplyOlderChatRestore(int count, float delta)
    {
        if (olderAnchorFromBottom < 0f)
        {
            return;
        }

        ImGui.SetScrollY(MathF.Max(0f, ImGui.GetScrollMaxY() - olderAnchorFromBottom));
        olderElapsed += delta;
        if (count > olderBaselineCount)
        {
            if (++olderSettleFrames >= OlderSettleFrames)
            {
                olderAnchorFromBottom = -1f;
            }
        }
        else if (olderElapsed >= OlderRestoreTimeout)
        {
            olderAnchorFromBottom = -1f;
        }
    }

    private void DrawOlderChatLoading(Rect listRect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var dotRadius = 2.6f * scale;
        var dotGap = 6f * scale;
        var baseX = listRect.Center.X - (dotRadius * 2f + dotGap);
        var baseY = listRect.Min.Y + 12f * scale;
        for (var dot = 0; dot < 3; dot++)
        {
            var wave = MathF.Max(0f, MathF.Sin(olderSpinnerPhase * 6f - dot * 0.9f));
            var alpha = 0.30f + 0.55f * wave;
            var center = new Vector2(baseX + dot * (dotRadius * 2f + dotGap), baseY);
            drawList.AddCircleFilled(center, dotRadius,
                ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.Dev.MutedInk, alpha)), 16);
        }
    }

    private void DrawChatMessage(DevChatMessageDto[] snapshot, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var message = snapshot[index];
        var mine = IsMine(message);
        if (!mine && (index == 0 ||
                      !string.Equals(snapshot[index - 1].SenderId, message.SenderId, StringComparison.Ordinal)))
        {
            var sender = string.IsNullOrEmpty(message.SenderDisplayName)
                ? message.SenderHandle
                : message.SenderDisplayName;
            var labelOrigin = ImGui.GetCursorPos();
            ImGui.SetCursorPos(new Vector2(labelOrigin.X + 4f * scale, labelOrigin.Y));
            var labelScreen = ImGui.GetCursorScreenPos();
            Typography.Draw(labelScreen, sender, Palette.Mix(Accent, theme.TextStrong, 0.4f), 0.74f,
                FontWeight.SemiBold);
            ImGui.SetCursorPos(new Vector2(labelOrigin.X, labelOrigin.Y + 16f * scale));
        }

        if (message.Kind == 1)
        {
            DrawImageBubble(message, index, mine);
        }
        else
        {
            DrawTextBubble(message, index, mine);
        }
    }

    private bool IsMine(DevChatMessageDto message) =>
        session.CurrentUser is { } me && string.Equals(me.Id, message.SenderId, StringComparison.Ordinal);

    private void DrawTextBubble(DevChatMessageDto message, int index, bool mine)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 12f * scale;
        var paddingY = 8f * scale;
        var wrap = available * 0.74f - paddingX * 2f;
        var textSize = ImGui.CalcTextSize(message.Body, false, wrap);
        var bubbleWidth = textSize.X + paddingX * 2f;
        var bubbleHeight = textSize.Y + paddingY * 2f;
        var start = ImGui.GetCursorPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        var entrance = ChatEntranceProgress(index);
        if (entrance < 1f)
        {
            DrawBubbleEntering(message.Body, scale, start, offsetX, bubbleWidth, bubbleHeight, paddingX, paddingY,
                wrap, mine, fill, ink, entrance);
        }
        else
        {
            ImGui.SetCursorPos(new Vector2(start.X + offsetX, start.Y));
            var bubbleScreen = ImGui.GetCursorScreenPos();
            Squircle.Fill(drawList, bubbleScreen, bubbleScreen + new Vector2(bubbleWidth, bubbleHeight), 14f * scale,
                ImGui.GetColorU32(fill));
            HandleOwnBubbleContext(message, mine, bubbleScreen, bubbleScreen + new Vector2(bubbleWidth, bubbleHeight));
            ImGui.SetCursorPos(new Vector2(start.X + offsetX + paddingX, start.Y + paddingY));
            ImGui.PushTextWrapPos(start.X + offsetX + paddingX + wrap);
            using (ImRaii.PushColor(ImGuiCol.Text, ink))
            {
                Typography.Plain(message.Body);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorPos(new Vector2(start.X, start.Y + bubbleHeight + 6f * scale));
    }

    private static void DrawBubbleEntering(string text, float scale, Vector2 start, float offsetX, float bubbleWidth,
        float bubbleHeight, float paddingX, float paddingY, float wrap, bool mine, Vector4 fill, Vector4 ink,
        float entrance)
    {
        var pop = 0.80f + 0.20f * Easing.EaseOutQuint(entrance);
        var alpha = MathF.Min(entrance * 1.8f, 1f);
        var rise = new Vector2(0f, (1f - Easing.EaseOutCubic(entrance)) * 10f * scale);
        ImGui.SetCursorPos(start);
        var screenStart = ImGui.GetCursorScreenPos();
        var fillMin = screenStart + new Vector2(offsetX, 0f);
        var fillMax = fillMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(mine ? fillMax.X : fillMin.X, fillMax.Y);
        var scaledMin = anchor + (fillMin - anchor) * pop + rise;
        var scaledMax = anchor + (fillMax - anchor) * pop + rise;
        Squircle.Fill(ImGui.GetWindowDrawList(), scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        var textLocal = new Vector2(start.X + offsetX + paddingX, start.Y + paddingY);
        var anchorLocal = new Vector2(mine ? start.X + offsetX + bubbleWidth : start.X + offsetX,
            start.Y + bubbleHeight);
        var scaledTextLocal = anchorLocal + (textLocal - anchorLocal) * pop + rise;
        ImGui.SetWindowFontScale(pop);
        ImGui.SetCursorPos(scaledTextLocal);
        ImGui.PushTextWrapPos(scaledTextLocal.X + wrap * pop);
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(ink, ink.W * alpha)))
        {
            Typography.Plain(text);
        }

        ImGui.PopTextWrapPos();
        ImGui.SetWindowFontScale(1f);
    }

    private void DrawImageBubble(DevChatMessageDto message, int index, bool mine)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var padding = 5f * scale;
        var aspect = message.MediaWidth > 0 && message.MediaHeight > 0
            ? (float)message.MediaHeight / message.MediaWidth
            : 1f;
        var imageWidth = available * 0.62f;
        var imageHeight = imageWidth * aspect;
        var maxHeight = 280f * scale;
        if (imageHeight > maxHeight)
        {
            imageHeight = maxHeight;
            imageWidth = imageHeight / aspect;
        }

        var bubbleWidth = imageWidth + padding * 2f;
        var bubbleHeight = imageHeight + padding * 2f;
        var start = ImGui.GetCursorPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var entrance = ChatEntranceProgress(index);
        var pop = entrance < 1f ? 0.80f + 0.20f * Easing.EaseOutQuint(entrance) : 1f;
        var alpha = entrance < 1f ? MathF.Min(entrance * 1.8f, 1f) : 1f;
        var rise = new Vector2(0f, entrance < 1f ? (1f - Easing.EaseOutCubic(entrance)) * 10f * scale : 0f);
        ImGui.SetCursorPos(start);
        var screen = ImGui.GetCursorScreenPos();
        var bubbleMin = screen + new Vector2(offsetX, 0f);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y);
        var scaledMin = anchor + (bubbleMin - anchor) * pop + rise;
        var scaledMax = anchor + (bubbleMax - anchor) * pop + rise;
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        HandleOwnBubbleContext(message, mine, scaledMin, scaledMax);
        var imageMin = scaledMin + new Vector2(padding * pop, padding * pop);
        var imageMax = imageMin + new Vector2(imageWidth * pop, imageHeight * pop);
        var rounding = 10f * scale * pop;
        var texture = images.Get(store.MediaUrl(message.Id));
        if (texture is null)
        {
            Squircle.Fill(drawList, imageMin, imageMax, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * alpha)));
            AppSkin.Icon((imageMin + imageMax) * 0.5f, FontAwesomeIcon.Image.ToIconString(),
                Palette.WithAlpha(AppPalettes.Dev.MutedInk, alpha), 1.2f);
        }
        else
        {
            drawList.AddImageRounded(texture.Handle, imageMin, imageMax, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
            if (entrance >= 1f && ImGui.IsMouseHoveringRect(imageMin, imageMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    router.Push(DevRoute.ImageView(message.Id));
                }
            }
        }

        ImGui.SetCursorPos(new Vector2(start.X, start.Y + bubbleHeight + 6f * scale));
    }

    private void HandleOwnBubbleContext(DevChatMessageDto message, bool mine, Vector2 min, Vector2 max)
    {
        if (!mine || !ImGui.IsMouseHoveringRect(min, max) || !ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            return;
        }

        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = "Delete this message for everyone?",
            ConfirmLabel = "Delete",
            CancelLabel = "Cancel",
            BusyLabel = "Deleting",
            FailedMessage = "Delete failed.",
            ConfirmAsync = done => store.DeleteMessage(message.Id, done),
        });
    }

    private void SyncChatEntrances(DevChatMessageDto[] snapshot, float delta)
    {
        var count = snapshot.Length;
        var lastId = count > 0 ? snapshot[count - 1].Id : null;
        if (!entrancePrimed)
        {
            entranceSettled = count;
            entrancePrimed = count > 0 || !store.LoadingChat;
            entranceLastId = lastId;
            return;
        }

        if (count > entranceSettled && lastId == entranceLastId)
        {
            chatEntrances.Clear();
            entranceSettled = count;
            entranceLastId = lastId;
            return;
        }

        entranceLastId = lastId;
        if (count < entranceSettled)
        {
            entranceSettled = count;
        }

        while (entranceSettled < count)
        {
            chatEntrances.Add(new BubbleEntrance { Line = entranceSettled, Elapsed = 0f });
            entranceSettled++;
        }

        for (var index = chatEntrances.Count - 1; index >= 0; index--)
        {
            var entrance = chatEntrances[index];
            entrance.Elapsed += delta;
            if (entrance.Elapsed >= TransitionTiming.BubbleSeconds || entrance.Line >= count)
            {
                chatEntrances.RemoveAt(index);
            }
            else
            {
                chatEntrances[index] = entrance;
            }
        }
    }

    private float ChatEntranceProgress(int line)
    {
        for (var index = 0; index < chatEntrances.Count; index++)
        {
            if (chatEntrances[index].Line == line)
            {
                return chatEntrances[index].Elapsed / TransitionTiming.BubbleSeconds;
            }
        }

        return 1f;
    }

    private void SyncChatFollow()
    {
        var scale = ImGuiHelpers.GlobalScale;
        followChatBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
        if (snapChatToBottom)
        {
            followChatBottom = true;
            snapChatToBottom = false;
        }
    }

    private void DrawChatComposer(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var buttonRadius = 16f * scale;
        var pictureCenter = new Vector2(area.Min.X + 12f * scale + buttonRadius, area.Center.Y);
        var pictureMin = pictureCenter - new Vector2(buttonRadius, buttonRadius);
        var pictureMax = pictureCenter + new Vector2(buttonRadius, buttonRadius);
        var pictureHovered = ImGui.IsMouseHoveringRect(pictureMin, pictureMax);
        drawList.AddCircleFilled(pictureCenter, buttonRadius,
            ImGui.GetColorU32(pictureHovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent), 24);
        AppSkin.Icon(pictureCenter, FontAwesomeIcon.Image.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.85f);
        HoverTooltip.Show(new Rect(pictureMin, pictureMax), "Send picture", HoverLabelSide.Above);
        if (pictureHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                chatPickerLoaded = false;
                router.Push(DevRoute.ChatImage);
            }
        }

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(pictureMax.X + 10f * scale, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        if (chatFocus)
        {
            ImGui.SetKeyboardFocusHere();
            chatFocus = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##devMessage", "Message", ref messageDraft, MessageMax,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = messageDraft.Trim().Length > 0 && !store.Sending;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(canSend ? Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.85f);
        var sendHitRadius = 16f * scale;
        var sendRect = new Rect(sendCenter - new Vector2(sendHitRadius, sendHitRadius),
            sendCenter + new Vector2(sendHitRadius, sendHitRadius));
        HoverTooltip.Show(sendRect, "Send", HoverLabelSide.Above);
        if (ImGui.IsMouseHoveringRect(sendRect.Min, sendRect.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canSend)
            {
                submitted = true;
            }
        }

        if (submitted && canSend)
        {
            var pending = messageDraft;
            store.SendMessage(pending, ok =>
            {
                if (!ok && messageDraft.Length == 0)
                {
                    messageDraft = pending;
                }
            });
            messageDraft = string.Empty;
            snapChatToBottom = true;
            chatFocus = true;
        }
    }

    private void DrawChatImagePicker(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, "Send Picture", back);
        if (!chatPickerLoaded)
        {
            chatPickerLoaded = true;
            chatPickerPaths = library.List();
            chatPendingPickedPath = null;
        }

        var picked = Interlocked.Exchange(ref chatPendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            SendChatImage(picked);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, "Import from PC", true))
        {
            LaunchChatImageDialog();
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (chatPickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale), "No photos yet",
                    AppPalettes.Dev.MutedInk);
                return;
            }

            const int columns = 3;
            var gap = 6f * scale;
            var cell = (ScrollLayout.StableContentWidth() - gap * (columns - 1)) / columns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < chatPickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("devpick", new Vector2(cell, cell));
                        DrawPickerThumbnail(chatPickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                            scale);
                        if (clicked)
                        {
                            SendChatImage(chatPickerPaths[index]);
                        }
                    }

                    if (index % columns != columns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void SendChatImage(string path)
    {
        store.SendImageMessage(path, _ => { });
        snapChatToBottom = true;
        chatPickerLoaded = false;
        router.Pop();
    }

    private void LaunchChatImageDialog()
    {
        NativeFileDialog.PickImage("Send Picture", path => Interlocked.Exchange(ref chatPendingPickedPath, path));
    }

    private static void DrawPickerThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            return;
        }

        var size = texture.Size;
        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        if (size.X > 0f && size.Y > 0f)
        {
            var aspect = size.X / size.Y;
            if (aspect > 1f)
            {
                var inset = (1f - 1f / aspect) * 0.5f;
                uv0 = new Vector2(inset, 0f);
                uv1 = new Vector2(1f - inset, 1f);
            }
            else if (aspect < 1f)
            {
                var inset = (1f - aspect) * 0.5f;
                uv0 = new Vector2(0f, inset);
                uv1 = new Vector2(1f, 1f - inset);
            }
        }

        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawImageViewer(Rect area, string messageId)
    {
        if (imageViewId != messageId)
        {
            imageViewId = messageId;
            imageSaveOutcome = 0;
            imageZoom.Reset();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.94f)));
        var headerHeight = AppHeader.Height * scale;
        var footerHeight = 60f * scale;
        var fitMin = new Vector2(area.Min.X + 8f * scale, area.Min.Y + headerHeight);
        var fitMax = new Vector2(area.Max.X - 8f * scale, area.Max.Y - footerHeight);
        var url = store.MediaUrl(messageId);
        var texture = images.Get(url);
        if (texture is null)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, (fitMin.Y + fitMax.Y) * 0.5f), "Loading",
                AppPalettes.Dev.MutedInk);
        }
        else
        {
            imageZoom.Draw(new Rect(fitMin, fitMax), texture, theme, 10f * scale);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var saved = imageSaveOutcome == 1;
        var label = saved ? "Saved to gallery" : "Save to gallery";
        var buttonWidth = MathF.Min(240f * scale, area.Width - 32f * scale);
        var buttonHeight = 42f * scale;
        var buttonTop = area.Max.Y - footerHeight + (footerHeight - buttonHeight) * 0.5f;
        var buttonRect = new Rect(new Vector2(area.Center.X - buttonWidth * 0.5f, buttonTop),
            new Vector2(area.Center.X + buttonWidth * 0.5f, buttonTop + buttonHeight));
        if (ui.PillButton(buttonRect, label, !saved) && !saved && !imageSaveBusy && texture is not null)
        {
            SaveChatImage(url);
        }
    }

    private void SaveChatImage(string? url)
    {
        if (string.IsNullOrEmpty(url) || imageSaveBusy)
        {
            return;
        }

        imageSaveBusy = true;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var bytes = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                if (bytes is not null)
                {
                    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                    var pixels = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixels);
                    library.Save(pixels, image.Width, image.Height);
                    succeeded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Dev] save image failed: {exception.Message}");
            }
            finally
            {
                imageSaveOutcome = succeeded ? 1 : 2;
                imageSaveBusy = false;
            }
        });
    }
}

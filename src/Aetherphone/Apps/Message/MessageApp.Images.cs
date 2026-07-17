using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private string? imageViewId;
    private volatile int imageSaveOutcome;
    private volatile bool imageSaveBusy;
    private string[] chatPickerPaths = Array.Empty<string>();
    private string? chatPickerConversationId;
    private string? chatPendingPickedPath;
    private readonly PhotoZoomView imageZoom = new();

    private void DrawChatImagePicker(Rect area, string conversationId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Common.ChangePhoto), back);
        if (chatPickerConversationId != conversationId)
        {
            chatPickerConversationId = conversationId;
            chatPickerPaths = library.List();
            chatPendingPickedPath = null;
        }

        var picked = Interlocked.Exchange(ref chatPendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            SendChatImage(conversationId, picked);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Common.ImportFromPc), true))
        {
            LaunchChatImageDialog();
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (chatPickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Common.NoPhotos), ui.MutedInk);
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
                        var clicked = ImGui.InvisibleButton("msgpick", new Vector2(cell, cell));
                        DrawPickerThumbnail(chatPickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                            scale);
                        if (clicked)
                        {
                            SendChatImage(conversationId, chatPickerPaths[index]);
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

    private void SendChatImage(string conversationId, string path)
    {
        store.SendImageMessage(conversationId, path, string.Empty, _ => { });
        transcript.RequestSnapToBottom();
        chatPickerConversationId = null;
        router.Pop();
    }

    private void LaunchChatImageDialog()
    {
        NativeFileDialog.PickImage(Loc.T(L.Common.ChangePhoto), path => Interlocked.Exchange(ref chatPendingPickedPath, path));
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

        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
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
        var texture = ResolveThreadImage(messageId);
        if (texture is null)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, (fitMin.Y + fitMax.Y) * 0.5f), Loc.T(L.Common.Loading),
                ui.MutedInk);
        }
        else
        {
            imageZoom.Draw(new Rect(fitMin, fitMax), texture, theme, 10f * scale);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var saved = imageSaveOutcome == 1;
        var label = saved ? Loc.T(L.Common.SavedToGallery) : Loc.T(L.Common.SaveToGallery);
        var buttonWidth = MathF.Min(240f * scale, area.Width - 32f * scale);
        var buttonHeight = 42f * scale;
        var buttonTop = area.Max.Y - footerHeight + (footerHeight - buttonHeight) * 0.5f;
        var buttonRect = new Rect(new Vector2(area.Center.X - buttonWidth * 0.5f, buttonTop),
            new Vector2(area.Center.X + buttonWidth * 0.5f, buttonTop + buttonHeight));
        if (ui.PillButton(buttonRect, label, !saved) && !saved && !imageSaveBusy && texture is not null)
        {
            SaveChatImage(messageId);
        }
    }

    private void SaveChatImage(string messageId)
    {
        var url = store.DmMediaUrl(messageId);
        var message = store.FindMessage(messageId);
        if (string.IsNullOrEmpty(url) || imageSaveBusy || message is null)
        {
            return;
        }

        var encrypted = message.EncVersion == EnvelopeCodec.VersionEnvelope;
        imageSaveBusy = true;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var raw = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                var bytes = encrypted && raw is not null
                    ? store.DecryptMedia(message, raw)
                    : raw;
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
                AepLog.Warning($"[Message] save image failed: {exception.Message}");
            }
            finally
            {
                imageSaveOutcome = succeeded ? 1 : 2;
                imageSaveBusy = false;
            }
        });
    }
}

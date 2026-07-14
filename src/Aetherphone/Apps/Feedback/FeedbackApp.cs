using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackApp : IPhoneApp
{
    private const int MaxFeedbackLength = 1000;
    private const int MaxAttachments = 5;
    private const int PickerColumns = 3;
    private const long CooldownSeconds = 60;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 AddTileStroke = new(1f, 1f, 1f, 0.18f);

    public string Id => "feedback";
    public string DisplayName => Loc.T(L.Apps.Feedback);
    public string Glyph => "Fb";
    public int BadgeCount => 0;

    private readonly FeedbackStore store;
    private readonly PhotoLibrary library;
    private readonly AppSkin ui = new(AppPalettes.Feedback);
    private readonly List<string> attachments = new();

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private string draft = string.Empty;
    private volatile int composeOutcome;
    private bool sent;
    private bool picking;
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;

    public FeedbackApp(AethernetSession session, AethernetClient client, PhotoLibrary library)
    {
        store = new FeedbackStore(session, client);
        this.library = library;
    }

    public void OnOpened()
    {
        composeOutcome = 0;
        sent = false;
        picking = false;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        ui.Body(content);

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (picked is not null)
        {
            AddAttachment(picked);
        }

        if (picking)
        {
            DrawPicker(content);
            return;
        }

        DrawScreen(content);
    }

    private void DrawScreen(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            attachments.Clear();
            sent = true;
            Plugin.Cfg.LastFeedbackSentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Plugin.Cfg.Save();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var headerContext = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(headerContext, Loc.T(L.Feedback.SendFeedback), navigation.Back);

        if (!sent)
        {
            var canSend = !string.IsNullOrWhiteSpace(draft) && !store.Posting && CooldownRemaining() == 0;
            var sendLabel = store.Posting ? Loc.T(L.Feedback.Sending) : Loc.T(L.Feedback.Send);
            ReportSendAnchor(area, sendLabel, scale);
            if (ui.HeaderAction(area, sendLabel, canSend))
            {
                AskSend();
            }
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            if (sent)
            {
                DrawThankYou(area);
            }
            else
            {
                DrawFeedbackCard(area);
            }
        }
    }

    private void DrawFeedbackCard(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var footerHeight = 40f * scale;

        var pad = 14f * scale;
        var cardMin = new Vector2(origin.X + 4f * scale, origin.Y + 4f * scale);
        var cardMax = new Vector2(origin.X + width - 4f * scale, area.Max.Y - footerHeight);
        ui.Card(drawList, cardMin, cardMax, 18f * scale);

        var inputX = cardMin.X + pad;
        var inputTop = cardMin.Y + pad;
        var inputWidth = width - pad * 2f;

        var gap = 6f * scale;
        var tile = (inputWidth - gap * MaxAttachments) / (MaxAttachments + 1);
        var stripHeight = tile + 10f * scale;
        var inputHeight = cardMax.Y - inputTop - pad - stripHeight;
        UiAnchors.Report("feedback.input",
            new Rect(new Vector2(inputX, inputTop), new Vector2(inputX + inputWidth, inputTop + inputHeight)));

        ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
        ImGui.SetNextItemWidth(inputWidth);

        var wrapWidth = inputWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Feedback.TitleInk))
        using (Plugin.Fonts.Push(1.15f))
        {
            SoftWrapField.Multiline("##feedbackBody", ref draft, MaxFeedbackLength,
                new Vector2(inputWidth, inputHeight), wrapWidth);
        }

        if (draft.Length == 0)
        {
            var placeholderPos = new Vector2(inputX + 4f * scale, inputTop + 2f * scale);
            var wrapRight = inputX + inputWidth - 4f * scale - ImGui.GetWindowPos().X;
            using (Plugin.Fonts.Push(1.15f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Feedback.MutedInk))
            {
                ImGui.SetCursorScreenPos(placeholderPos);
                ImGui.PushTextWrapPos(wrapRight);
                Typography.Plain(Loc.T(L.Feedback.Placeholder));
                ImGui.PopTextWrapPos();
            }
        }

        DrawAttachmentStrip(drawList, inputX, cardMax.Y - pad - tile, tile, gap, scale);

        var remaining = MaxFeedbackLength - draft.Length;
        var counterColor = remaining < 40
            ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
            : AppPalettes.Feedback.MutedInk;
        var counter = remaining.ToString(Loc.Culture);
        var counterSize = Typography.Measure(counter, 0.9f, FontWeight.Medium);
        Typography.Draw(new Vector2(area.Max.X - 4f * scale - counterSize.X,
            area.Max.Y - footerHeight * 0.5f - counterSize.Y * 0.5f), counter, counterColor, 0.9f, FontWeight.Medium);

        var cooldown = CooldownRemaining();
        if (cooldown > 0)
        {
            var notice = Loc.T(L.Feedback.Cooldown, FormatCooldown(cooldown));
            Typography.Draw(new Vector2(origin.X + 2f * scale,
                area.Max.Y - footerHeight * 0.5f - Typography.Measure(notice, 0.85f).Y * 0.5f), notice,
                AppPalettes.Feedback.MutedInk, 0.85f);
        }
    }

    private void DrawAttachmentStrip(ImDrawListPtr drawList, float x, float y, float tile, float gap, float scale)
    {
        var rounding = 10f * scale;
        var removeIndex = -1;
        for (var index = 0; index < attachments.Count; index++)
        {
            var min = new Vector2(x + (tile + gap) * index, y);
            var max = min + new Vector2(tile, tile);
            if (DrawAttachmentThumb(drawList, attachments[index], min, max, rounding, scale))
            {
                removeIndex = index;
            }
        }

        if (attachments.Count < MaxAttachments)
        {
            var min = new Vector2(x + (tile + gap) * attachments.Count, y);
            var max = min + new Vector2(tile, tile);
            if (DrawAddTile(drawList, min, max, rounding, scale))
            {
                OpenPicker();
            }
        }

        if (removeIndex >= 0)
        {
            attachments.RemoveAt(removeIndex);
        }
    }

    private bool DrawAttachmentThumb(ImDrawListPtr drawList, string path, Vector2 min, Vector2 max, float rounding,
        float scale)
    {
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        var badgeRadius = 8.5f * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - 2f * scale, min.Y + badgeRadius + 2f * scale);
        var badgeHovered = ImGui.IsMouseHoveringRect(badgeCenter - new Vector2(badgeRadius, badgeRadius),
            badgeCenter + new Vector2(badgeRadius, badgeRadius));
        drawList.AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, badgeHovered ? 0.9f : 0.62f)), 20);
        AppSkin.Icon(badgeCenter, FontAwesomeIcon.Times.ToIconString(), White, 0.6f);
        if (!badgeHovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool DrawAddTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale)
    {
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(hovered ? ui.HoverTint : AppPalettes.Feedback.FieldSurface));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(AddTileStroke), 1f);
        AppSkin.Icon((min + max) * 0.5f, FontAwesomeIcon.Plus.ToIconString(), AppPalettes.Feedback.BodyInk, 0.9f);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawPicker(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Feedback.AddPhotos), () => picking = false);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Feedback.ImportFromPc), true))
        {
            LaunchFileDialog();
        }

        var gridTop = importRect.Max.Y + 12f * scale;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Feedback.NoGallery), AppPalettes.Feedback.MutedInk);
                return;
            }

            var gap = 6f * scale;
            var cell = (ScrollLayout.StableContentWidth() - gap * (PickerColumns - 1)) / PickerColumns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < pickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("pick", new Vector2(cell, cell));
                        DrawLocalThumbnail(pickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                        if (clicked)
                        {
                            AddAttachment(pickerPaths[index]);
                        }
                    }

                    if (index % PickerColumns != PickerColumns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void OpenPicker()
    {
        pickerPaths = library.List();
        picking = true;
    }

    private void AddAttachment(string path)
    {
        picking = false;
        if (string.IsNullOrEmpty(path) || attachments.Count >= MaxAttachments)
        {
            return;
        }

        for (var index = 0; index < attachments.Count; index++)
        {
            if (string.Equals(attachments[index], path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        attachments.Add(path);
    }

    private void LaunchFileDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Loc.T(L.Feedback.AddPhotos)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingPickedPath, task.Result);
            }
        });
    }

    private void DrawThankYou(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardMin = new Vector2(origin.X + 4f * scale, origin.Y + 4f * scale);
        var cardMax = new Vector2(origin.X + width - 4f * scale, area.Max.Y - 8f * scale);
        ui.Card(drawList, cardMin, cardMax, 18f * scale);

        var centerX = (cardMin.X + cardMax.X) * 0.5f;
        var badgeCenter = new Vector2(centerX, cardMin.Y + 78f * scale);
        var badgeRadius = 34f * scale;
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(AppPalettes.Feedback.Accent), 48);
        var check = badgeRadius;
        var checkColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        drawList.AddLine(badgeCenter + new Vector2(-0.42f * check, 0.02f * check),
            badgeCenter + new Vector2(-0.10f * check, 0.34f * check), checkColor, 4f * scale);
        drawList.AddLine(badgeCenter + new Vector2(-0.10f * check, 0.34f * check),
            badgeCenter + new Vector2(0.44f * check, -0.30f * check), checkColor, 4f * scale);

        var titleY = badgeCenter.Y + badgeRadius + 26f * scale;
        Typography.DrawCentered(new Vector2(centerX, titleY), Loc.T(L.Feedback.Sent), theme.TextStrong, 1.35f,
            FontWeight.SemiBold);
        Typography.DrawCentered(new Vector2(centerX, titleY + 34f * scale), Loc.T(L.Feedback.ThankYou),
            AppPalettes.Feedback.BodyInk, 1.05f, FontWeight.Medium);
        Typography.DrawCentered(new Vector2(centerX, titleY + 60f * scale), Loc.T(L.Feedback.SentMessage),
            AppPalettes.Feedback.MutedInk, 0.9f);

        var cooldown = CooldownRemaining();
        var actionY = cardMax.Y - 36f * scale;
        if (cooldown > 0)
        {
            var notice = Loc.T(L.Feedback.Cooldown, FormatCooldown(cooldown));
            Typography.DrawCentered(new Vector2(centerX, actionY), notice, AppPalettes.Feedback.MutedInk, 0.95f,
                FontWeight.Medium);
            return;
        }

        var label = Loc.T(L.Feedback.SendMore);
        var buttonWidth = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 44f * scale;
        var buttonHeight = 34f * scale;
        var rect = new Rect(new Vector2(centerX - buttonWidth * 0.5f, actionY - buttonHeight * 0.5f),
            new Vector2(centerX + buttonWidth * 0.5f, actionY + buttonHeight * 0.5f));
        if (ui.PillButton(rect, label, true))
        {
            sent = false;
        }
    }

    private static void ReportSendAnchor(Rect area, string label, float scale)
    {
        if (!UiAnchors.Recording)
        {
            return;
        }

        var height = 28f * scale;
        var width = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 26f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        UiAnchors.Report("feedback.send", new Rect(min, max));
    }

    private void AskSend()
    {
        if (string.IsNullOrWhiteSpace(draft) || store.Posting || CooldownRemaining() > 0)
        {
            return;
        }

        var pending = draft;
        var pendingImages = attachments.ToArray();
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Feedback.ConfirmMessage),
            ConfirmLabel = Loc.T(L.Feedback.Send),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Feedback.Sending),
            FailedMessage = Loc.T(L.Feedback.ErrorMessage),
            Danger = false,
            ConfirmAsync = done => store.Compose(pending, pendingImages, ok =>
            {
                if (ok)
                {
                    composeOutcome = 1;
                }

                done(ok);
            }),
        });
    }


    private static int CooldownRemaining()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = CooldownSeconds - (now - Plugin.Cfg.LastFeedbackSentUnix);
        return remaining > 0 ? (int)remaining : 0;
    }

    private static string FormatCooldown(int seconds)
    {
        if (seconds >= 60)
        {
            return TimeText.MinutesSeconds(seconds);
        }

        return string.Format(Loc.Culture, "{0}s", seconds);
    }

    public void Dispose()
    {
        store.Dispose();
    }
}

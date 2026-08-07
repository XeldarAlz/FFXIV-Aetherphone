using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetComposeResult
{
    Open,
    Closed,
    Posted,
}

internal sealed class VelvetPostComposer
{
    public const int MaxPostTags = 8;

    private readonly VelvetStore store;
    private readonly StoryPresenter stories;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly MentionPopup mentionPopup = new();
    private readonly MentionAutocomplete captionMentions;
    private readonly EmojiComposer captionEmoji = new();
    private readonly PhotoComposeSession session;
    private readonly Action openTags;
    private readonly List<string> tags = new();
    private bool storyMode;
    private PostAspect aspect = PostAspect.Square;
    private readonly string[] aspectLabels = new string[PostAspects.All.Length];
    private volatile int outcome;
    private bool closeRequested;
    private string caption = string.Empty;
    private string status = string.Empty;
    private int audience = VelvetPostAudience.Connections;

    public VelvetPostComposer(VelvetStore store, StoryPresenter stories, PhotoLibrary library,
        RemoteImageCache images, LodestoneService lodestone, WallpaperImageCache wallpaperImages, Action openTags)
    {
        this.store = store;
        this.stories = stories;
        this.images = images;
        this.lodestone = lodestone;
        this.openTags = openTags;
        captionMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        session = new PhotoComposeSession(library, wallpaperImages);
    }

    public int TagCount => tags.Count;

    public bool HasTag(string token) => tags.Contains(token);

    public void ToggleTag(string token)
    {
        if (tags.Remove(token))
        {
            return;
        }

        if (tags.Count < MaxPostTags)
        {
            tags.Add(token);
        }
    }

    public void ClearTags() => tags.Clear();

    private static PhotoComposeStyle Style => new(AppPalettes.Velvet.Accent, AppPalettes.Velvet.MutedInk,
        new Vector4(1f, 1f, 1f, 0.10f), AppPalettes.Velvet.Accent, AppPalettes.Velvet.MutedInk, false);

    private const float AspectPickerReserve = 42f;

    private float Aspect => storyMode
        ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight
        : PostAspects.Ratio(aspect);

    private string Title => storyMode ? Loc.T(L.Story.NewStory) : Loc.T(L.Velvet.NewPost);

    private bool Posting => storyMode ? stories.Posting : store.Posting;

    public void OpenWith(string photoPath)
    {
        Open();
        session.TakePicked(photoPath);
        session.BeginCropSequence();
    }

    public void Open(bool story = false)
    {
        storyMode = story;
        aspect = PostAspect.Square;
        outcome = 0;
        closeRequested = false;
        caption = string.Empty;
        status = string.Empty;
        audience = VelvetPostAudience.Connections;
        tags.Clear();
        captionEmoji.Close();
        session.Open(story);
    }

    public VelvetComposeResult Draw(Rect area, AppSkin ui, in PhoneContext context)
    {
        if (outcome == 1)
        {
            outcome = 0;
            return storyMode ? VelvetComposeResult.Closed : VelvetComposeResult.Posted;
        }

        if (outcome == 2)
        {
            outcome = 0;
            status = Loc.T(L.Account.CannotReach);
        }

        if (closeRequested)
        {
            closeRequested = false;
            return VelvetComposeResult.Closed;
        }

        session.ConsumePendingImport();
        switch (session.Stage)
        {
            case PhotoComposeStage.Crop:
                DrawCrop(area, ui, context);
                break;
            case PhotoComposeStage.Caption:
                DrawCaption(area, ui, context);
                break;
            default:
                DrawPick(area, ui, context);
                break;
        }

        return VelvetComposeResult.Open;
    }

    private void DrawPick(Rect area, AppSkin ui, in PhoneContext context)
    {
        var scale = UiScale.Current;
        var showNext = !storyMode;
        var nextLabel = Loc.T(L.Common.Next);
        var nextReserve = showNext
            ? Typography.Measure(nextLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale + 20f * scale
            : 0f;
        AppHeader.Draw(context, string.Empty, () => closeRequested = true);
        AppHeader.DrawTitleWithReserve(area, "velvet.compose.pick.title", Title, nextReserve, context.Theme.TextStrong,
            scale);
        if (showNext && ui.HeaderAction(area, nextLabel, session.HasSelection))
        {
            session.BeginCropSequence();
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Velvet.ImportFromPc), true))
        {
            session.LaunchImportDialog(Title);
        }

        var noticeHeight = session.Notice.Length > 0 ? 20f * scale : 0f;
        if (noticeHeight > 0f)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, importRect.Max.Y + 8f * scale), session.Notice,
                AppPalettes.Velvet.MutedInk, TextStyles.Footnote);
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale + noticeHeight), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (session.PickerCount == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Velvet.NoPhotos), AppPalettes.Velvet.MutedInk);
                return;
            }

            session.DrawPickGrid(gridRect, scale, Style, true);
        }
    }

    private void DrawCrop(Rect area, AppSkin ui, in PhoneContext context)
    {
        var scale = UiScale.Current;
        var title = session.SelectedCount > 1
            ? Loc.T(L.Common.PhotoStep, session.CropIndex + 1, session.SelectedCount)
            : Loc.T(L.Velvet.MoveAndScale);
        var nextLabel = Loc.T(L.Common.Next);
        var nextReserve = Typography.Measure(nextLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale + 20f * scale;
        AppHeader.Draw(context, string.Empty, session.CropBack);
        AppHeader.DrawTitleWithReserve(area, "velvet.compose.crop.title", title, nextReserve, context.Theme.TextStrong,
            scale);
        if (ui.HeaderAction(area, nextLabel, true))
        {
            session.CropAdvance();
        }

        var reserve = storyMode ? 0f : AspectPickerReserve;
        session.DrawCropCanvas(area, scale, Aspect, Style, Loc.T(L.Velvet.GestureHint), reserve);
        if (!storyMode)
        {
            DrawAspectPicker(area, scale);
        }
    }

    private void DrawAspectPicker(Rect area, float scale)
    {
        var width = MathF.Min(area.Width - 32f * scale, 260f * scale);
        var rowTop = area.Max.Y - (96f + AspectPickerReserve - 8f) * scale;
        var row = new Rect(new Vector2(area.Center.X - width * 0.5f, rowTop),
            new Vector2(area.Center.X + width * 0.5f, rowTop + 28f * scale));
        for (var index = 0; index < PostAspects.All.Length; index++)
        {
            aspectLabels[index] = Loc.T(AspectLabels.For(PostAspects.All[index]));
        }

        var picked = SegmentStrip.Draw("velvet.compose.aspect", row, aspectLabels,
            Array.IndexOf(PostAspects.All, aspect), VelvetTheme.Palette);
        if (picked >= 0 && picked < PostAspects.All.Length)
        {
            aspect = PostAspects.All[picked];
        }
    }

    private void DrawCaption(Rect area, AppSkin ui, in PhoneContext context)
    {
        var scale = UiScale.Current;
        var busy = Posting;
        var actionLabel = busy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Share);
        var actionReserve = Typography.Measure(actionLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale + 20f * scale;
        AppHeader.Draw(context, string.Empty, () => session.LoadCropStage(session.SelectedCount - 1));
        AppHeader.DrawTitleWithReserve(area, "velvet.compose.caption.title", Title, actionReserve,
            context.Theme.TextStrong, scale);
        if (ui.HeaderAction(area, actionLabel, !busy))
        {
            Commit();
        }

        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var captionHeight = 34f * scale;
        var captionY = area.Max.Y - 20f * scale - captionHeight;
        var audienceHeight = storyMode ? 0f : 30f * scale;
        var audienceGap = storyMode ? 0f : 10f * scale;
        var audienceTop = captionY - audienceGap - audienceHeight;
        var tagsHeight = storyMode ? 0f : 32f * scale;
        var tagsGap = storyMode ? 0f : 8f * scale;
        var tagsTop = audienceTop - tagsGap - tagsHeight;
        var stripHeight = session.SelectedCount > 1 ? 52f * scale : 0f;
        var statusHeight = status.Length > 0 ? 20f * scale : 0f;
        var previewRegion = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, tagsTop - 12f * scale - stripHeight - statusHeight));
        DrawCaptionPreview(previewRegion, scale);
        if (statusHeight > 0f)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, tagsTop - 12f * scale), status, context.Theme.Danger,
                TextStyles.Footnote);
        }

        if (!storyMode)
        {
            DrawTagsRow(new Rect(new Vector2(area.Min.X + 16f * scale, tagsTop),
                new Vector2(area.Max.X - 16f * scale, tagsTop + tagsHeight)), scale);
        }

        if (stripHeight > 0f)
        {
            var strip = new Rect(new Vector2(area.Min.X + 16f * scale, previewRegion.Max.Y + 6f * scale),
                new Vector2(area.Max.X - 16f * scale, previewRegion.Max.Y + stripHeight));
            session.DrawCaptionStrip(strip, scale, Style);
        }

        if (!storyMode)
        {
            var audienceRect = new Rect(new Vector2(area.Min.X + 16f * scale, audienceTop),
                new Vector2(area.Max.X - 16f * scale, audienceTop + audienceHeight));
            var pickedAudience = VSegmented.Draw("velvetAudience", audienceRect,
                new[] { Loc.T(L.Velvet.AudienceConnections), Loc.T(L.Velvet.AudiencePublic) }, audience, scale);
            if (pickedAudience >= 0)
            {
                audience = pickedAudience;
            }
        }

        var captionRect = new Rect(new Vector2(area.Min.X + 16f * scale, captionY),
            new Vector2(area.Max.X - 16f * scale, captionY + captionHeight));
        Squircle.Fill(drawList, captionRect.Min, captionRect.Max, 9f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        var emojiRadius = 13f * scale;
        var emojiCenter = new Vector2(captionRect.Min.X + 10f * scale + emojiRadius, captionRect.Center.Y);
        captionEmoji.DrawToggle(ui, emojiCenter, emojiRadius, AppPalettes.Velvet.Accent, AppPalettes.Velvet.MutedInk,
            Loc.T(L.Common.Emoji));
        var textLeft = emojiCenter.X + emojiRadius + 6f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, captionRect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(captionRect.Max.X - textLeft - 12f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.TitleInk))
        {
            MentionField.SingleLineWithHint("##velvetCaption", Loc.T(L.Velvet.CaptionHint), ref caption, 500,
                captionMentions);
        }

        var pickedMention = mentionPopup.Draw(captionMentions, area, context.Theme, images, lodestone);
        if (pickedMention >= 0)
        {
            captionMentions.Pick(pickedMention);
        }

        mentionPopup.Gate(captionMentions);

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, captionRect.Min.Y - panelHeight),
                new Vector2(area.Max.X, captionRect.Min.Y)), ui, ref caption, 500);
        }
    }

    private void DrawTagsRow(Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, 9f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.16f : 0.10f)));
        AppSkin.Icon(new Vector2(rect.Min.X + 18f * scale, rect.Center.Y), FontAwesomeIcon.Hashtag.ToIconString(),
            tags.Count > 0 ? AppPalettes.Velvet.Accent : AppPalettes.Velvet.MutedInk, 0.78f);

        var textLeft = rect.Min.X + 34f * scale;
        var textWidth = rect.Max.X - textLeft - 14f * scale;
        var label = tags.Count == 0 ? Loc.T(L.Velvet.PostTagsEmpty) : string.Join(", ", tags);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 8f * scale),
            Typography.FitText(label, textWidth, TextStyles.Subheadline),
            tags.Count == 0 ? AppPalettes.Velvet.MutedInk : AppPalettes.Velvet.TitleInk, TextStyles.Subheadline);

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            openTags();
        }
    }

    private void DrawCaptionPreview(Rect region, float scale)
    {
        var aspect = Aspect;
        var preview = ImageFit.CenteredRect(region, aspect);
        if (preview.Width <= 0f)
        {
            return;
        }

        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();
        if (!session.TryGetPreviewUv(aspect, out var texture, out var uv0, out var uv1))
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
            return;
        }

        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (UiInteract.HoverClick(preview.Min, preview.Max))
        {
            session.LoadCropStage(session.ClampedPreviewIndex);
        }
    }

    private void Commit()
    {
        if (!session.HasSelection || Posting)
        {
            return;
        }

        status = string.Empty;
        if (storyMode)
        {
            stories.CreateStory(session.FirstSelected, session.CropAt(0), caption, ok => outcome = ok ? 1 : 2);
            return;
        }

        store.CreatePost(session.SelectedArray(), session.CropsArray(), aspect, caption, tags.ToArray(),
            audience, ok => outcome = ok ? 1 : 2);
    }
}

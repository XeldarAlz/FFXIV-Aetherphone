using System.Numerics;
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
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetPostComposer
{
    private readonly VelvetStore store;
    private readonly StoryPresenter stories;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly MentionPopup mentionPopup = new();
    private readonly MentionAutocomplete captionMentions;
    private readonly PhotoComposeSession session;
    private bool storyMode;
    private volatile int outcome;
    private bool closeRequested;
    private string caption = string.Empty;
    private string status = string.Empty;

    public VelvetPostComposer(VelvetStore store, StoryPresenter stories, PhotoLibrary library,
        RemoteImageCache images, LodestoneService lodestone, WallpaperImageCache wallpaperImages)
    {
        this.store = store;
        this.stories = stories;
        this.images = images;
        this.lodestone = lodestone;
        captionMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        session = new PhotoComposeSession(library, wallpaperImages);
    }

    private static PhotoComposeStyle Style => new(AppPalettes.Velvet.Accent, AppPalettes.Velvet.MutedInk,
        new Vector4(1f, 1f, 1f, 0.10f), AppPalettes.Velvet.Accent, AppPalettes.Velvet.MutedInk, false);

    private float Aspect => storyMode ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight : 1f;

    private string Title => storyMode ? Loc.T(L.Story.NewStory) : Loc.T(L.Velvet.NewPost);

    private bool Posting => storyMode ? stories.Posting : store.Posting;

    public void Open(bool story = false)
    {
        storyMode = story;
        outcome = 0;
        closeRequested = false;
        caption = string.Empty;
        status = string.Empty;
        session.Open(story);
    }

    public bool Draw(Rect area, AppSkin ui, in PhoneContext context)
    {
        if (outcome == 1)
        {
            outcome = 0;
            return true;
        }

        if (outcome == 2)
        {
            outcome = 0;
            status = Loc.T(L.Account.CannotReach);
        }

        if (closeRequested)
        {
            closeRequested = false;
            return true;
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

        return false;
    }

    private void DrawPick(Rect area, AppSkin ui, in PhoneContext context)
    {
        AppHeader.Draw(context, Title, () => closeRequested = true);
        if (!storyMode && ui.HeaderAction(area, Loc.T(L.Common.Next), session.HasSelection))
        {
            session.BeginCropSequence();
        }

        var scale = ImGuiHelpers.GlobalScale;
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
        var title = session.SelectedCount > 1
            ? Loc.T(L.Common.PhotoStep, session.CropIndex + 1, session.SelectedCount)
            : Loc.T(L.Velvet.MoveAndScale);
        AppHeader.Draw(context, title, session.CropBack);
        if (ui.HeaderAction(area, Loc.T(L.Common.Next), true))
        {
            session.CropAdvance();
        }

        session.DrawCropCanvas(area, ImGuiHelpers.GlobalScale, Aspect, Style, Loc.T(L.Velvet.GestureHint));
    }

    private void DrawCaption(Rect area, AppSkin ui, in PhoneContext context)
    {
        AppHeader.Draw(context, Title, () => session.LoadCropStage(session.SelectedCount - 1));

        var busy = Posting;
        if (ui.HeaderAction(area, busy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Share), !busy))
        {
            Commit();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var captionHeight = 34f * scale;
        var captionY = area.Max.Y - 20f * scale - captionHeight;
        var stripHeight = session.SelectedCount > 1 ? 52f * scale : 0f;
        var statusHeight = status.Length > 0 ? 20f * scale : 0f;
        var previewRegion = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, captionY - 12f * scale - stripHeight - statusHeight));
        DrawCaptionPreview(previewRegion, scale);
        if (statusHeight > 0f)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, captionY - 12f * scale), status, context.Theme.Danger,
                TextStyles.Footnote);
        }

        if (stripHeight > 0f)
        {
            var strip = new Rect(new Vector2(area.Min.X + 16f * scale, previewRegion.Max.Y + 6f * scale),
                new Vector2(area.Max.X - 16f * scale, previewRegion.Max.Y + stripHeight));
            session.DrawCaptionStrip(strip, scale, Style);
        }

        var captionRect = new Rect(new Vector2(area.Min.X + 16f * scale, captionY),
            new Vector2(area.Max.X - 16f * scale, captionY + captionHeight));
        Squircle.Fill(drawList, captionRect.Min, captionRect.Max, 9f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(captionRect.Min.X + 12f * scale,
            captionRect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(captionRect.Width - 24f * scale);
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

        store.CreatePost(session.SelectedArray(), session.CropsArray(), caption, Array.Empty<string>(),
            ok => outcome = ok ? 1 : 2);
    }
}

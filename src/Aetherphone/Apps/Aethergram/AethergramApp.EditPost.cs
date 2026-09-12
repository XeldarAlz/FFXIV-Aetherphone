using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float EditPostDotsHeight = 18f;

    private readonly MentionAutocomplete editPostMentions;
    private readonly Action<ImDrawListPtr, Vector2, Vector2, float, string?> editPostPreviewPage;
    private PostDto? editPost;
    private string editPostCaption = string.Empty;
    private bool editPostSensitive;
    private string editPostStatus = string.Empty;
    private volatile bool editPostBusy;
    private volatile int editPostOutcome;

    private void OpenEditPost(PostDto post)
    {
        editPost = post;
        editPostCaption = post.Text;
        editPostSensitive = post.Sensitive;
        editPostStatus = string.Empty;
        editPostOutcome = 0;
        captionFocus = true;
        composeTagMode = false;
        composeStatus = string.Empty;
        composeTags.Clear();
        if (post.PhotoTags is { } tags)
        {
            for (var index = 0; index < tags.Length; index++)
            {
                composeTags.Add(tags[index]);
            }
        }

        editPostMentions.Close();
        captionEmoji.Close();
        personPicker.Close();
        router.Push(AethergramRoute.EditPost(post.Id));
    }

    private void DrawEditPost(Rect area)
    {
        if (editPost is not { } post)
        {
            back();
            return;
        }

        if (editPostOutcome == 1)
        {
            editPostOutcome = 0;
            translation.Forget(new TranslationKey(TranslationSurface.Post, post.Id));
            back();
            return;
        }

        if (editPostOutcome == 2)
        {
            editPostOutcome = 0;
            editPostStatus = Loc.T(L.Aethergram.EditPostFailed);
        }

        personPicker.Gate();
        var scale = UiScale.Current;
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        if (composeTagMode)
        {
            DrawEditPostTagging(area, post, photos, scale);
            return;
        }

        if (DrawEditHeader(area, Loc.T(L.Aethergram.EditPost), !editPostBusy, editPostBusy))
        {
            SaveEditPost(post);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var statusHeight = editPostStatus.Length > 0
            ? Typography.MeasureWrappedBlock(editPostStatus, TextStyles.Footnote, right - left).Y
              + ComposeStatusGap * scale
            : 0f;
        var cardsBottom = area.Max.Y - margin - statusHeight;
        var optionsCard = new Rect(new Vector2(left, cardsBottom - ComposeRowHeight * 2f * scale),
            new Vector2(right, cardsBottom));
        var captionBottom = optionsCard.Min.Y - ComposeCardGap * scale;
        var captionHeight = (ComposeCardPad * 2f + ComposeCaptionFieldHeight + ComposeMetaGap + ComposeMetaRowHeight)
                            * scale;
        var captionCard = new Rect(new Vector2(left, captionBottom - captionHeight), new Vector2(right, captionBottom));
        var dotsBlock = photos.Length > 1 ? EditPostDotsHeight * scale : 0f;
        var previewHalfWidth = area.Width * ComposePreviewWidthFraction * 0.5f;
        var previewRegion = new Rect(new Vector2(area.Center.X - previewHalfWidth, top + ComposePreviewGap * scale),
            new Vector2(area.Center.X + previewHalfWidth, captionCard.Min.Y - ComposePreviewGap * scale - dotsBlock));
        var preview = ImageFit.CenteredRect(previewRegion, PostAspects.DisplayRatio(post.MediaWidth, post.MediaHeight));
        var page = DrawEditPostPreview(preview, post, photos, scale, Loc.T(L.PhotoTag.TagPeople), out var tapped);
        if (tapped)
        {
            composeTagMode = true;
        }

        DrawEditPostDots(preview, photos.Length, page, dotsBlock);
        DrawCaptionCard(captionCard, area, scale, "##aethergramEditCaption", ref editPostCaption, editPostMentions);
        if (DrawComposeOptionsCard(optionsCard, scale, ref editPostSensitive))
        {
            composeTagMode = true;
        }

        if (statusHeight > 0f)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, cardsBottom + ComposeStatusGap * scale),
                editPostStatus, Ink.Danger, TextStyles.Footnote, right - left);
        }

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            var panelBottom = area.Max.Y - margin;
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, panelBottom - panelHeight),
                new Vector2(area.Max.X, panelBottom)), ui, ref editPostCaption, MaxCaptionLength);
        }
    }

    private void DrawEditPostTagging(Rect area, PostDto post, string[] photos, float scale)
    {
        if (DrawComposeHeader(area, Loc.T(L.PhotoTag.TagPeople), false, composeExitTagMode, Loc.T(L.Aethergram.Done),
                true))
        {
            composeTagMode = false;
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var hintHeight = Typography.LineHeight(TextStyles.Footnote) + ComposeTagHintGap * scale;
        var dotsBlock = photos.Length > 1 ? EditPostDotsHeight * scale : 0f;
        var previewRegion = new Rect(new Vector2(left, top + ComposePreviewGap * scale),
            new Vector2(right, area.Max.Y - margin - hintHeight - dotsBlock));
        var preview = ImageFit.CenteredRect(previewRegion, PostAspects.DisplayRatio(post.MediaWidth, post.MediaHeight));
        var page = DrawEditPostPreview(preview, post, photos, scale, string.Empty, out var tapped);
        if (tapped)
        {
            PlaceTagAt(preview, page);
        }

        DrawEditPostDots(preview, photos.Length, page, dotsBlock);
        DrawTaggingFooter(area, preview.Max.Y + dotsBlock, margin, hintHeight, scale);
    }

    private int DrawEditPostPreview(Rect preview, PostDto post, string[] photos, float scale, string tooltip,
        out bool tapped)
    {
        tapped = false;
        if (preview.Width <= 0f || preview.Height <= 0f)
        {
            return 0;
        }

        var rounding = ComposePreviewRounding * scale;
        var drawList = ImGui.GetWindowDrawList();
        Elevation.Card(drawList, preview.Min, preview.Max, rounding, scale);
        var result = carousel.Draw(drawList, preview, post.Id, photos, rounding, editPostPreviewPage);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        if (DrawComposeTags(drawList, preview, result.Index, scale))
        {
            carousel.CancelTap();
            return result.Index;
        }

        if (tooltip.Length > 0)
        {
            HoverTooltip.Show(preview, tooltip, HoverLabelSide.Below);
        }

        if (!result.InputConsumed && UiInteract.Hover(preview.Min, preview.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        tapped = result.Tapped;
        return result.Index;
    }

    private void DrawEditPostPreviewPage(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, string? url)
    {
        DrawGramImage(drawList, new Rect(min, max), url, rounding);
    }

    private void DrawEditPostDots(Rect preview, int count, int page, float dotsBlock)
    {
        if (dotsBlock <= 0f)
        {
            return;
        }

        PhotoCarousel.DrawDots(ImGui.GetWindowDrawList(),
            new Vector2(preview.Center.X, preview.Max.Y + dotsBlock * 0.5f), count, page, preview.Width, Ink.MutedInk);
    }

    private void SaveEditPost(PostDto post)
    {
        if (editPostBusy)
        {
            return;
        }

        var trimmed = editPostCaption.Trim();
        var tags = ComposeTagInputs() ?? Array.Empty<PhotoTagInput>();
        if (string.Equals(trimmed, post.Text, StringComparison.Ordinal)
            && editPostSensitive == post.Sensitive
            && PhotoTagEdits.Unchanged(post.PhotoTags, tags))
        {
            back();
            return;
        }

        editPostBusy = true;
        editPostStatus = string.Empty;
        store.EditPost(post.Id, trimmed, tags, editPostSensitive, ok =>
        {
            editPostBusy = false;
            editPostOutcome = ok ? 1 : 2;
        });
    }
}

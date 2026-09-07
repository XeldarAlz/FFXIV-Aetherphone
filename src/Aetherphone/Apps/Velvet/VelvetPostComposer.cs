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

    private const int CaptionLimit = 500;
    private const int HeaderActionSlots = 2;
    private const float AspectPickerReserve = 42f;
    private const float BodySide = 16f;
    private const float BodyBottom = 16f;
    private const float PreviewGap = 10f;
    private const float CardPad = 14f;
    private const float CaptionFieldHeight = 62f;
    private const float CaptionMetaGap = 4f;
    private const float CaptionMetaHeight = 20f;
    private const float CardBlockGap = 12f;
    private const float TagRowHeight = 44f;
    private const float AudienceGap = 10f;
    private const float AudienceTileHeight = 54f;
    private const float StripHeight = 52f;
    private const float RowTile = 26f;
    private const float RowGlyph = 15f;
    private const float ActionHeight = 28f;
    private const float PreviewShadow = 0.9f;
    private const int CounterWarning = 50;

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
    private string tagsLabel = string.Empty;
    private bool storyMode;
    private readonly string[] aspectLabels = new string[PostAspects.All.Length];
    private volatile int outcome;
    private bool closeRequested;
    private string caption = string.Empty;
    private string counterText = string.Empty;
    private int counterLength = -1;
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
        if (!tags.Remove(token) && tags.Count < MaxPostTags)
        {
            tags.Add(token);
        }

        tagsLabel = BuildTagsLabel();
    }

    private string BuildTagsLabel()
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < tags.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(VelvetTokenLabels.Of(tags[index]));
        }

        return builder.ToString();
    }

    public void ClearTags()
    {
        tags.Clear();
        tagsLabel = string.Empty;
    }

    private static PhotoComposeStyle Style => new(VelvetTheme.Rose, VelvetTheme.MutedInk, VelvetTheme.PlumWell,
        VelvetTheme.Rose, VelvetTheme.MutedInk, false);

    private float CropAspect => storyMode
        ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight
        : PostAspects.Ratio(session.CurrentAspect);

    private float ContainerAspect => storyMode
        ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight
        : PostAspects.Ratio(session.ContainerAspect);

    private float PreviewAspect => storyMode
        ? ContainerAspect
        : PostAspects.Ratio(session.AspectAt(session.ClampedPreviewIndex));

    private bool CropAllowsReveal => !storyMode && PostAspects.RevealsWholeImage(session.CurrentAspect);

    private bool PreviewAllowsReveal =>
        !storyMode && PostAspects.RevealsWholeImage(session.AspectAt(session.ClampedPreviewIndex));

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
        outcome = 0;
        closeRequested = false;
        caption = string.Empty;
        counterLength = -1;
        status = string.Empty;
        audience = VelvetPostAudience.Connections;
        ClearTags();
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
                DrawCrop(area, ui);
                break;
            case PhotoComposeStage.Caption:
                DrawCaption(area, ui, context);
                break;
            default:
                DrawPick(area, ui);
                break;
        }

        return VelvetComposeResult.Open;
    }

    private static bool HeaderAction(Rect area, string label, bool enabled, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = ActionHeight * scale;
        var width = AppSkin.PillWidthFor(label, height) + 6f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + VHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        var hovered = enabled && UiInteract.Hover(min, max);
        AccentPill.Paint(drawList, min, max, height * 0.5f, hovered, VelvetTheme.Rose, VelvetTheme.RoseDeep,
            VelvetTheme.RoseShadow, enabled ? 1f : 0.45f);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label,
            enabled ? VelvetTheme.OnAccent : VelvetTheme.Alpha(VelvetTheme.OnAccent, 0.6f), 0.9f,
            FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(min, max, hovered);
    }

    private void DrawPick(Rect area, AppSkin ui)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Title, HeaderActionSlots))
        {
            closeRequested = true;
            return;
        }

        if (!storyMode && HeaderAction(area, Loc.T(L.Common.Next), session.HasSelection, scale))
        {
            session.BeginCropSequence();
        }

        var top = area.Min.Y + VHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + BodySide * scale, top + 8f * scale),
            new Vector2(area.Max.X - BodySide * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Velvet.ImportFromPc), true))
        {
            session.LaunchImportDialog(Title);
        }

        var noticeHeight = session.Notice.Length > 0 ? 20f * scale : 0f;
        if (noticeHeight > 0f)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(),
                new Vector2(area.Center.X, importRect.Max.Y + 14f * scale), session.Notice, VelvetTheme.MutedInk,
                TextStyles.Footnote);
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale + noticeHeight), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (session.PickerCount == 0)
            {
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale), Loc.T(L.Velvet.NoPhotos),
                    VelvetTheme.MutedInk, TextStyles.Body);
                return;
            }

            session.DrawPickGrid(gridRect, scale, Style, true);
        }
    }

    private void DrawCrop(Rect area, AppSkin ui)
    {
        var scale = UiScale.Current;
        var title = session.SelectedCount > 1
            ? Loc.T(L.Common.PhotoStep, session.CropIndex + 1, session.SelectedCount)
            : Loc.T(L.Velvet.MoveAndScale);
        if (VHeader.Push(area, title, HeaderActionSlots))
        {
            session.CropBack();
            return;
        }

        if (HeaderAction(area, Loc.T(L.Common.Next), true, scale))
        {
            session.CropAdvance();
        }

        var reserve = storyMode ? 0f : AspectPickerReserve;
        session.DrawCropCanvas(area, scale, CropAspect, Style, Loc.T(L.Velvet.GestureHint), reserve, CropAllowsReveal);
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

        var current = session.CurrentAspect;
        var picked = SegmentStrip.Draw("velvet.compose.aspect", row, aspectLabels,
            Array.IndexOf(PostAspects.All, current), VelvetTheme.Palette);
        if (picked >= 0 && picked < PostAspects.All.Length)
        {
            session.SetAspect(session.CropIndex, PostAspects.All[picked]);
        }
    }

    private float CardHeight(float scale)
    {
        var content = CaptionFieldHeight + CaptionMetaGap + CaptionMetaHeight;
        if (!storyMode)
        {
            content += CardBlockGap + TagRowHeight + AudienceGap + AudienceTileHeight;
        }

        return (content + CardPad * 2f) * scale;
    }

    private void DrawCaption(Rect area, AppSkin ui, in PhoneContext context)
    {
        var scale = UiScale.Current;
        var busy = Posting;
        if (VHeader.Push(area, Title, HeaderActionSlots))
        {
            session.LoadCropStage(session.SelectedCount - 1);
            return;
        }

        if (HeaderAction(area, busy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Share), !busy, scale))
        {
            Commit();
        }

        var drawList = ImGui.GetWindowDrawList();
        var side = BodySide * scale;
        var left = area.Min.X + side;
        var right = area.Max.X - side;
        var cardMax = new Vector2(right, area.Max.Y - BodyBottom * scale);
        var cardMin = new Vector2(left, cardMax.Y - CardHeight(scale));
        var statusHeight = status.Length > 0
            ? Typography.MeasureWrappedBlock(status, TextStyles.Footnote, right - left).Y + 8f * scale
            : 0f;
        var stripHeight = session.SelectedCount > 1 ? StripHeight * scale : 0f;
        var previewTop = area.Min.Y + VHeader.Height * scale + PreviewGap * scale;
        var previewBottom = cardMin.Y - PreviewGap * scale - statusHeight - stripHeight;
        DrawCaptionPreview(new Rect(new Vector2(left, previewTop), new Vector2(right, previewBottom)), scale);
        if (stripHeight > 0f)
        {
            session.DrawCaptionStrip(new Rect(new Vector2(left, previewBottom),
                new Vector2(right, previewBottom + stripHeight)), scale, Style);
        }

        if (statusHeight > 0f)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, cardMin.Y - statusHeight), status,
                VelvetTheme.Danger, TextStyles.Footnote, right - left);
        }

        VCard.Paint(drawList, cardMin, cardMax, scale);
        var pad = CardPad * scale;
        var contentLeft = cardMin.X + pad;
        var contentRight = cardMax.X - pad;
        var cursorY = cardMin.Y + pad;
        var field = new Rect(new Vector2(contentLeft, cursorY),
            new Vector2(contentRight, cursorY + CaptionFieldHeight * scale));
        DrawCaptionField(field, scale);
        cursorY = field.Max.Y + CaptionMetaGap * scale;
        DrawCaptionMeta(new Rect(new Vector2(contentLeft, cursorY),
            new Vector2(contentRight, cursorY + CaptionMetaHeight * scale)), ui, scale);
        if (!storyMode)
        {
            cursorY += (CaptionMetaHeight + CardBlockGap) * scale;
            FeedCell.Hairline(drawList, contentLeft, contentRight, cursorY, VelvetTheme.Hairline);
            var tagRow = new Rect(new Vector2(contentLeft, cursorY),
                new Vector2(contentRight, cursorY + TagRowHeight * scale));
            DrawTagRow(tagRow, scale);
            cursorY = tagRow.Max.Y + AudienceGap * scale;
            DrawAudienceTiles(new Rect(new Vector2(contentLeft, cursorY),
                new Vector2(contentRight, cursorY + AudienceTileHeight * scale)), scale);
        }

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, cardMin.Y - panelHeight),
                new Vector2(area.Max.X, cardMin.Y)), ui, ref caption, CaptionLimit);
        }

        var picked = mentionPopup.Draw(captionMentions, area, context.Theme, images, lodestone);
        if (picked >= 0)
        {
            captionMentions.Pick(picked);
        }

        mentionPopup.Gate(captionMentions);
    }

    private void DrawCaptionField(Rect field, float scale)
    {
        var padding = ImGui.GetStyle().FramePadding;
        ImGui.SetCursorScreenPos(field.Min);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, VelvetTheme.TitleInk))
        {
            SoftWrapField.Multiline("##velvetCaption", ref caption, CaptionLimit, field.Size,
                field.Width - padding.X * 2f - 4f * scale, captionMentions);
        }

        if (caption.Length > 0)
        {
            return;
        }

        Typography.Draw(ImGui.GetWindowDrawList(), field.Min + padding,
            Typography.FitText(Loc.T(L.Velvet.CaptionHint), field.Width - padding.X * 2f, TextStyles.Body),
            VelvetTheme.MutedInk, TextStyles.Body);
    }

    private void DrawCaptionMeta(Rect row, AppSkin ui, float scale)
    {
        var radius = 12f * scale;
        captionEmoji.DrawToggle(ui, new Vector2(row.Min.X + radius, row.Center.Y), radius, VelvetTheme.Rose,
            VelvetTheme.MutedInk, Loc.T(L.Common.Emoji));
        SyncCounter();
        var size = Typography.Measure(counterText, TextStyles.Footnote);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f),
            counterText, caption.Length >= CaptionLimit - CounterWarning ? VelvetTheme.Danger : VelvetTheme.Faint,
            TextStyles.Footnote);
    }

    private void SyncCounter()
    {
        if (counterLength == caption.Length)
        {
            return;
        }

        counterLength = caption.Length;
        counterText = counterLength.ToString(Loc.Culture) + "/" + CaptionLimit.ToString(Loc.Culture);
    }

    private void DrawTagRow(Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, Metrics.Radius.Sm * scale, VelvetTheme.HoverWash.Packed());
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = RowTile * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tile * 0.5f);
        VCard.Tile(drawList, tileMin, new Vector2(tileMin.X + tile, tileMin.Y + tile), PhoneIcons.Hash,
            VelvetTheme.Rose, RowGlyph * scale, scale);
        var chevronCenter = new Vector2(row.Max.X - 9f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, VelvetTheme.MutedInk, VIcon.Row * scale);
        var labelLeft = tileMin.X + tile + 12f * scale;
        var labelRight = chevronCenter.X - 12f * scale;
        var empty = tags.Count == 0;
        var label = empty ? Loc.T(L.Velvet.PostTagsEmpty) : Loc.T(L.Velvet.PostTagsTitle);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - labelSize.Y * 0.5f), label,
            empty ? VelvetTheme.MutedInk : VelvetTheme.TitleInk, TextStyles.Body);
        if (!empty)
        {
            var valueWidth = MathF.Max(1f, labelRight - labelLeft - labelSize.X - 10f * scale);
            var value = Typography.FitText(tagsLabel, valueWidth, TextStyles.Subheadline);
            var valueSize = Typography.Measure(value, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(labelRight - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
                VelvetTheme.RoseInk, TextStyles.Subheadline);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            openTags();
        }
    }

    private void DrawAudienceTiles(Rect row, float scale)
    {
        var gap = 10f * scale;
        var half = (row.Width - gap) * 0.5f;
        DrawAudienceTile(new Rect(row.Min, new Vector2(row.Min.X + half, row.Max.Y)), VelvetPostAudience.Connections,
            PhoneIcons.Users, Loc.T(L.Velvet.AudienceConnections), scale);
        DrawAudienceTile(new Rect(new Vector2(row.Max.X - half, row.Min.Y), row.Max), VelvetPostAudience.Public,
            PhoneIcons.World, Loc.T(L.Velvet.AudiencePublic), scale);
    }

    private void DrawAudienceTile(Rect tile, int value, string glyph, string label, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var active = audience == value;
        var hovered = UiInteract.Hover(tile.Min, tile.Max);
        var rounding = Metrics.Radius.Md * scale;
        if (active)
        {
            AccentPill.Paint(drawList, tile.Min, tile.Max, rounding, hovered, VelvetTheme.Rose, VelvetTheme.RoseDeep,
                VelvetTheme.RoseShadow);
        }
        else
        {
            Squircle.Fill(drawList, tile.Min, tile.Max, rounding,
                (hovered ? VelvetTheme.Alpha(VelvetTheme.TitleInk, 0.08f) : VelvetTheme.PlumWell).Packed());
            Squircle.Stroke(drawList, tile.Min, tile.Max, rounding, VelvetTheme.CardStroke.Packed(),
                Metrics.Stroke.Hairline * scale);
        }

        var ink = active ? VelvetTheme.OnAccent : VelvetTheme.MutedInk;
        PhoneIcon.Draw(drawList, new Vector2(tile.Center.X, tile.Min.Y + 18f * scale), glyph, ink, VIcon.Field * scale);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Marquee.DrawCenteredAuto(drawList, new MarqueeId("velvet.compose.audience.", label), label, tile.Center.X,
            tile.Max.Y - 10f * scale - labelHeight, tile.Width - 12f * scale, TextStyles.FootnoteEmphasized, ink);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(tile.Min, tile.Max, hovered))
        {
            audience = value;
        }
    }

    private void DrawCaptionPreview(Rect region, float scale)
    {
        var preview = ImageFit.CenteredRect(region, ContainerAspect);
        if (preview.Width <= 0f || preview.Height <= 0f)
        {
            return;
        }

        var rounding = Metrics.Radius.Lg * scale;
        var drawList = ImGui.GetWindowDrawList();
        Elevation.Card(drawList, preview.Min, preview.Max, rounding, scale, PreviewShadow);
        if (!session.TryGetPreviewUv(PreviewAspect, PreviewAllowsReveal, out var texture, out var uv0, out var uv1))
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, VelvetTheme.PlumWell.Packed());
            Typography.DrawCentered(drawList, preview.Center, Loc.T(L.Common.Loading), VelvetTheme.MutedInk,
                TextStyles.Body);
            return;
        }

        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
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

        store.CreatePost(session.SelectedArray(), session.CropsArray(), session.AspectsArray(), caption,
            tags.ToArray(), audience, ok => outcome = ok ? 1 : 2);
    }
}

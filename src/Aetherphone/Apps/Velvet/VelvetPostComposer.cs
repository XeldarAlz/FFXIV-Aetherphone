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
    private const float BodySide = 16f;
    private const float BodyBottom = 16f;
    private const float PreviewGap = 12f;
    private const float PreviewWidthFraction = 0.62f;
    private const float CardPad = 14f;
    private const float CardGap = 12f;
    private const float CaptionFieldHeight = 62f;
    private const float CaptionMetaGap = 6f;
    private const float CaptionMetaHeight = 34f;
    private const float EmojiRadius = 17f;
    private const float EmojiWellAlpha = 0.08f;
    private const float EmojiWellLitAlpha = 0.14f;
    private const float TagRowHeight = 44f;
    private const float AudienceGap = 10f;
    private const float AudienceTileHeight = 54f;
    private const float StripGap = 10f;
    private const float RowTile = 26f;
    private const float RowGlyph = 15f;
    private const float ActionHeight = 28f;
    private const float ShareHeight = 46f;
    private const float PaneFraction = 0.5f;
    private const float GridGap = 6f;
    private const float NoticeHeight = 22f;
    private const float StatusGap = 8f;
    private const float PreviewShadow = 0.9f;
    private const int CounterWarning = 50;
    private const int CircleSegments = 24;

    private static readonly TextStyle ActionStyle = new(0.9f, FontWeight.SemiBold);

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
        VelvetTheme.MutedInk, false);

    private static PhotoEditPanelStyle EditStyle =>
        PhotoEditPanelStyle.ForComposer(Style, VelvetTheme.TitleInk, VelvetTheme.PlumWell);

    private static float StoryAspect => (float)StoryStore.StoryWidth / StoryStore.StoryHeight;

    private float Aspect => storyMode ? StoryAspect : PostAspects.Ratio(session.Aspect);

    private bool AllowsReveal => !storyMode && PostAspects.RevealsWholeImage(session.Aspect);

    private string Title => storyMode ? Loc.T(L.Story.NewStory) : Loc.T(L.Velvet.NewPost);

    private bool Posting => storyMode ? stories.Posting : store.Posting;

    public void OpenWith(string photoPath)
    {
        Open();
        session.TakePicked(photoPath);
        session.BeginEdit();
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
            case PhotoComposeStage.Edit:
                DrawEdit(area);
                break;
            case PhotoComposeStage.Caption:
                DrawCaption(area, ui, context);
                break;
            default:
                DrawPick(area);
                break;
        }

        return VelvetComposeResult.Open;
    }

    private static bool RosePill(Rect rect, string label, bool enabled, in TextStyle style)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        AccentPill.Paint(drawList, rect.Min, rect.Max, rect.Height * 0.5f, hovered, VelvetTheme.Rose,
            VelvetTheme.RoseDeep, VelvetTheme.RoseShadow, enabled ? 1f : 0.45f);
        Typography.DrawCentered(drawList, rect.Center, label,
            enabled ? VelvetTheme.OnAccent : VelvetTheme.Alpha(VelvetTheme.OnAccent, 0.6f), style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static bool HeaderAction(Rect area, string label, bool enabled, float scale)
    {
        var height = ActionHeight * scale;
        var width = AppSkin.PillWidthFor(label, height) + 6f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + VHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        return RosePill(new Rect(min, max), label, enabled, ActionStyle);
    }

    private void DrawPick(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Title, HeaderActionSlots))
        {
            closeRequested = true;
            return;
        }

        if (HeaderAction(area, Loc.T(L.Common.Next), session.HasSelection && !Posting, scale))
        {
            session.BeginEdit();
        }

        var top = area.Min.Y + VHeader.Height * scale;
        var paneHeight = MathF.Min(area.Width, (area.Max.Y - top) * PaneFraction);
        var pane = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + paneHeight));
        session.DrawPickPane(pane, scale, Style, Aspect, AllowsReveal, !storyMode, !Posting);
        var gridTop = pane.Max.Y + GridGap * scale;
        if (session.Notice.Length > 0)
        {
            var notice = Typography.FitText(session.Notice, area.Width - BodySide * 2f * scale, TextStyles.Footnote);
            Typography.DrawCentered(ImGui.GetWindowDrawList(),
                new Vector2(area.Center.X, gridTop + NoticeHeight * 0.5f * scale), notice, VelvetTheme.MutedInk,
                TextStyles.Footnote);
            gridTop += NoticeHeight * scale;
        }

        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            session.DrawPickGrid(gridRect, scale, Style, true, Loc.T(L.Velvet.ImportFromPc), Title);
        }
    }

    private void DrawEdit(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Social.ComposeEditTitle), HeaderActionSlots))
        {
            session.EditBack();
            return;
        }

        if (HeaderAction(area, Loc.T(L.Common.Next), !Posting, scale))
        {
            session.EditAdvance();
        }

        session.DrawEditCanvas(area, scale, Aspect, Style, AllowsReveal, !Posting);
        session.DrawComposerFooter(area, scale, EditStyle, !Posting);
    }

    private void DrawCaption(Rect area, AppSkin ui, in PhoneContext context)
    {
        var scale = UiScale.Current;
        var busy = Posting;
        if (VHeader.Push(area, Title))
        {
            session.CaptionBack();
            return;
        }

        var side = BodySide * scale;
        var left = area.Min.X + side;
        var right = area.Max.X - side;
        var shareRect = new Rect(new Vector2(left, area.Max.Y - (BodyBottom + ShareHeight) * scale),
            new Vector2(right, area.Max.Y - BodyBottom * scale));
        var statusHeight = status.Length > 0
            ? Typography.MeasureWrappedBlock(status, TextStyles.Footnote, right - left).Y + StatusGap * scale
            : 0f;
        var cardsBottom = shareRect.Min.Y - CardGap * scale - statusHeight;
        var optionsHeight = storyMode ? 0f : (CardPad * 2f + TagRowHeight + AudienceGap + AudienceTileHeight) * scale;
        var optionsCard = new Rect(new Vector2(left, cardsBottom - optionsHeight), new Vector2(right, cardsBottom));
        var captionBottom = storyMode ? cardsBottom : optionsCard.Min.Y - CardGap * scale;
        var captionHeight = (CardPad * 2f + CaptionFieldHeight + CaptionMetaGap + CaptionMetaHeight) * scale;
        var captionCard = new Rect(new Vector2(left, captionBottom - captionHeight), new Vector2(right, captionBottom));
        var stripHeight = session.SelectedCount > 1 ? PhotoComposeSession.StripHeight * scale : 0f;
        var stripBlock = stripHeight > 0f ? stripHeight + StripGap * scale : 0f;
        var previewTop = area.Min.Y + (VHeader.Height + PreviewGap) * scale;
        var halfWidth = area.Width * PreviewWidthFraction * 0.5f;
        var previewRegion = new Rect(new Vector2(area.Center.X - halfWidth, previewTop),
            new Vector2(area.Center.X + halfWidth, captionCard.Min.Y - PreviewGap * scale - stripBlock));
        var preview = ImageFit.CenteredRect(previewRegion, Aspect);
        if (DrawCaptionPreview(preview, scale))
        {
            session.OpenEdit(session.ClampedPreviewIndex);
            return;
        }

        if (stripHeight > 0f)
        {
            var stripTop = preview.Max.Y + StripGap * scale;
            var tapped = session.DrawPhotoStrip(new Rect(new Vector2(left, stripTop), new Vector2(right, stripTop + stripHeight)),
                scale, Style, session.ClampedPreviewIndex);
            if (tapped >= 0)
            {
                session.PreviewIndex = tapped;
            }
        }

        DrawCaptionCard(captionCard, ui, scale);
        if (!storyMode)
        {
            DrawOptionsCard(optionsCard, scale);
        }

        if (statusHeight > 0f)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, shareRect.Min.Y - statusHeight), status,
                VelvetTheme.Danger, TextStyles.Footnote, right - left);
        }

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            var panelBottom = shareRect.Min.Y - CardGap * scale;
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, panelBottom - panelHeight),
                new Vector2(area.Max.X, panelBottom)), ui, ref caption, CaptionLimit);
        }

        var picked = mentionPopup.Draw(captionMentions, area, context.Theme, images, lodestone);
        if (picked >= 0)
        {
            captionMentions.Pick(picked);
        }

        mentionPopup.Gate(captionMentions);
        if (RosePill(shareRect, busy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Share), !busy, TextStyles.Headline))
        {
            Commit();
        }
    }

    private void DrawCaptionCard(Rect card, AppSkin ui, float scale)
    {
        VCard.Paint(ImGui.GetWindowDrawList(), card.Min, card.Max, scale);
        var pad = CardPad * scale;
        var field = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Max.X - pad, card.Min.Y + pad + CaptionFieldHeight * scale));
        DrawCaptionField(field, scale);
        var metaTop = field.Max.Y + CaptionMetaGap * scale;
        DrawCaptionMeta(new Rect(new Vector2(field.Min.X, metaTop),
            new Vector2(field.Max.X, metaTop + CaptionMetaHeight * scale)), ui, scale);
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
        var drawList = ImGui.GetWindowDrawList();
        var radius = EmojiRadius * scale;
        var center = new Vector2(row.Min.X + radius, row.Center.Y);
        var extent = new Vector2(radius, radius);
        var lit = captionEmoji.Open || UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            VelvetTheme.Alpha(VelvetTheme.TitleInk, lit ? EmojiWellLitAlpha : EmojiWellAlpha).Packed(), CircleSegments);
        captionEmoji.DrawToggle(ui, center, radius, VelvetTheme.Rose, VelvetTheme.TitleInk, Loc.T(L.Common.Emoji));
        SyncCounter();
        var size = Typography.Measure(counterText, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), counterText,
            caption.Length >= CaptionLimit - CounterWarning ? VelvetTheme.Danger : VelvetTheme.Faint,
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

    private void DrawOptionsCard(Rect card, float scale)
    {
        VCard.Paint(ImGui.GetWindowDrawList(), card.Min, card.Max, scale);
        var pad = CardPad * scale;
        var contentLeft = card.Min.X + pad;
        var contentRight = card.Max.X - pad;
        var tagRow = new Rect(new Vector2(contentLeft, card.Min.Y + pad),
            new Vector2(contentRight, card.Min.Y + pad + TagRowHeight * scale));
        DrawTagRow(tagRow, scale);
        var tilesTop = tagRow.Max.Y + AudienceGap * scale;
        DrawAudienceTiles(new Rect(new Vector2(contentLeft, tilesTop),
            new Vector2(contentRight, tilesTop + AudienceTileHeight * scale)), scale);
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

    private bool DrawCaptionPreview(Rect preview, float scale)
    {
        if (preview.Width <= 0f || preview.Height <= 0f)
        {
            return false;
        }

        var rounding = Metrics.Radius.Lg * scale;
        var drawList = ImGui.GetWindowDrawList();
        Elevation.Card(drawList, preview.Min, preview.Max, rounding, scale, PreviewShadow);
        if (!session.TryGetPreviewUv(Aspect, AllowsReveal, out var texture, out var uv0, out var uv1))
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, VelvetTheme.PlumWell.Packed());
            Typography.DrawCentered(drawList, preview.Center, Loc.T(L.Common.Loading), VelvetTheme.MutedInk,
                TextStyles.Body);
            return false;
        }

        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        var hovered = UiInteract.Hover(preview.Min, preview.Max);
        HoverTooltip.Show(preview, Loc.T(L.Social.ComposeTapToEdit), HoverLabelSide.Below);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(preview.Min, preview.Max, hovered);
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
            stories.CreateStory(session.FirstSelected, session.CropAt(0), session.EditAt(0), caption,
                ok => outcome = ok ? 1 : 2);
            return;
        }

        store.CreatePost(session.SelectedArray(), session.CropsArray(), session.AspectsArray(), session.EditsArray(),
            caption, tags.ToArray(), audience, ok => outcome = ok ? 1 : 2);
    }

    public void Dispose()
    {
        session.Dispose();
    }
}

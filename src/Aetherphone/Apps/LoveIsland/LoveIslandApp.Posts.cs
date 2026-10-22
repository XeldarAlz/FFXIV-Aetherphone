using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed class PostBody
{
    public string[] Blocks = Array.Empty<string>();
    public bool[] OpensParagraph = Array.Empty<bool>();
    private string source = string.Empty;

    public void Sync(string body)
    {
        if (ReferenceEquals(body, source))
        {
            return;
        }

        source = body;
        var lines = body.Split('\n');
        var blocks = new List<string>(lines.Length);
        var opens = new List<bool>(lines.Length);
        var blankBefore = false;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                blankBefore = true;
                continue;
            }

            opens.Add(blocks.Count > 0 && blankBefore);
            blocks.Add(line);
            blankBefore = false;
        }

        Blocks = blocks.ToArray();
        OpensParagraph = opens.ToArray();
    }
}

internal sealed partial class LoveIslandApp
{
    private const float PostCoverAspect = 0.5f;
    private const float ParagraphGap = 12f;
    private const int PostRowBodyLines = 2;
    private const int PostFromListDepth = 2;
    private const string PostOpenLinkId = "loveisland.post.link";

    private readonly Dictionary<string, PostBody> postBodies = new(StringComparer.Ordinal);
    private long postsVisitSeenUnix = long.MaxValue;

    private void DrawPostsList(Rect area)
    {
        var frame = BeginPushed(area, out var headerContext, out var body);
        var title = Texts.T(CampaignTextKeys.Announcements);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            var posts = store.Posts;
            var cursorY = origin.Y + Metrics.Space.Sm * scale;
            if (posts.Length == 0)
            {
                cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY),
                    store.LoadedOnce ? Loc.T(L.LoveIsland.NothingYet) : Loc.T(L.Common.Loading), ui.MutedInk,
                    TextStyles.Body, inner);
            }

            for (var pass = 0; pass < 2; pass++)
            {
                var pinnedPass = pass == 0;
                for (var index = 0; index < posts.Length; index++)
                {
                    if (posts[index].Pinned != pinnedPass)
                    {
                        continue;
                    }

                    cursorY = DrawPostRow(drawList, posts[index], left, cursorY, inner, scale)
                              + LoveIslandKit.CardGap * scale;
                }
            }

            Reserve(origin, width, cursorY);
            if (store.LoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, ui.MutedInk);
            }
            else if (store.HasMorePosts && InfiniteScroll.ReachedBottom())
            {
                store.LoadMorePosts();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }

        EndPushed(in frame, in headerContext, "loveisland.nav.posts", title, Texts.T(CampaignTextKeys.TabTonight));
    }

    private float DrawPostRow(ImDrawListPtr drawList, CampaignPostDto post, float left, float top, float width,
        float scale)
    {
        var pad = LoveIslandKit.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var hasCover = post.ImageUrls is { Length: > 0 };
        var coverHeight = hasCover ? width * PostCoverAspect : 0f;
        var titleLines = ClampFor(titleClamps, post.Id).Get(post.Title, LoveIslandKit.CardTitleStyle, inner,
            DefaultTitleLines, fontKey);
        var bodyLines = ClampFor(bodyClamps, post.Id).Get(post.Body, LoveIslandKit.CardBodyStyle, inner,
            PostRowBodyLines, fontKey);
        var metaHeight = MathF.Max(Typography.LineHeight(TextStyles.Footnote), LoveIslandKit.PillHeight(scale));
        var height = coverHeight + pad * 2f + metaHeight + LoveIslandKit.EyebrowGap * scale
                     + titleLines.Length * Typography.LineHeight(LoveIslandKit.CardTitleStyle)
                     + (bodyLines.Length > 0
                         ? LoveIslandKit.TitleGap * scale + bodyLines.Length * Typography.LineHeight(LoveIslandKit.CardBodyStyle)
                         : 0f);
        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(rest.Min, rest.Max))
        {
            return rest.Max.Y;
        }

        var tapped = LoveIslandKit.PressableCard(drawList, ui, rest, post.Id, scale, true, out _);
        if (hasCover)
        {
            var radius = LoveIslandKit.CardRadius * scale;
            var cover = new Rect(rest.Min, new Vector2(rest.Max.X, rest.Min.Y + coverHeight));
            drawList.PushClipRect(cover.Min, cover.Max, true);
            LoveIslandKit.CoverImage(drawList, images, post.ImageUrls[0],
                new Rect(cover.Min, new Vector2(cover.Max.X, cover.Max.Y + radius)), radius, ui.Accent);
            drawList.PopClipRect();
        }

        var cursorY = rest.Min.Y + coverHeight + pad;
        var metaLeft = rest.Min.X + pad;
        if (post.PublishedAtUnix > postsVisitSeenUnix)
        {
            metaLeft += LoveIslandKit.LivePill(drawList, new Vector2(metaLeft, cursorY), Loc.T(L.LoveIsland.NewBadge),
                ui.Accent, scale) + Metrics.Space.Xs * scale;
        }

        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var metaY = cursorY + (metaHeight - footHeight) * 0.5f;
        if (post.Pinned)
        {
            var pinned = Texts.T(CampaignTextKeys.Pinned);
            Typography.Draw(drawList, new Vector2(metaLeft, metaY), pinned, ui.Accent, TextStyles.FootnoteEmphasized);
            metaLeft += Typography.Measure(pinned, TextStyles.FootnoteEmphasized).X + Metrics.Space.Sm * scale;
        }

        Typography.Draw(drawList, new Vector2(metaLeft, metaY),
            Typography.FitText(postLabels.Stamp(post), MathF.Max(1f, rest.Max.X - pad - metaLeft), TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        cursorY += metaHeight + LoveIslandKit.EyebrowGap * scale;
        var textLeft = rest.Min.X + pad;
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], ui.TitleInk,
                LoveIslandKit.CardTitleStyle);
            cursorY += Typography.LineHeight(LoveIslandKit.CardTitleStyle);
        }

        if (bodyLines.Length > 0)
        {
            cursorY += LoveIslandKit.TitleGap * scale;
            for (var lineIndex = 0; lineIndex < bodyLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), bodyLines[lineIndex], ui.MutedInk,
                    LoveIslandKit.CardBodyStyle);
                cursorY += Typography.LineHeight(LoveIslandKit.CardBodyStyle);
            }
        }

        if (tapped)
        {
            OpenPost(post.Id);
        }

        return rest.Max.Y;
    }

    private void DrawPostDetail(Rect area, string postId, int depth)
    {
        var post = Ledger.FindPost(postId);
        var frame = BeginPushedInline(area, out var headerContext, out var body);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            if (post is null)
            {
                var missing = store.LoadedOnce ? Loc.T(L.LoveIsland.PostGone) : Loc.T(L.Common.Loading);
                var bottom = origin.Y + Metrics.Space.Xl * scale + Typography.DrawWrappedLeft(
                    new Vector2(left, origin.Y + Metrics.Space.Xl * scale), missing, ui.MutedInk, TextStyles.Body, inner);
                Reserve(origin, width, bottom);
            }
            else
            {
                var cursorY = DrawPostArticle(drawList, post, origin, width, left, inner, scale);
                Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
            }
        }

        EndPushed(in frame, in headerContext, "loveisland.nav.post", post?.Title ?? string.Empty,
            Texts.T(depth > PostFromListDepth ? CampaignTextKeys.Announcements : CampaignTextKeys.TabTonight));
    }

    private float DrawPostArticle(ImDrawListPtr drawList, CampaignPostDto post, Vector2 origin, float width,
        float left, float inner, float scale)
    {
        var cursorY = origin.Y;
        var urls = post.ImageUrls ?? Array.Empty<string>();
        if (urls.Length > 0)
        {
            var cover = new Rect(origin, new Vector2(origin.X + width, origin.Y + width * LoveIslandKit.HeroAspect));
            LoveIslandKit.CoverImage(drawList, images, urls[0], cover, 0f, ui.Accent);
            if (urls.Length > 1)
            {
                MultiPhotoBadge.Draw(drawList, new Vector2(cover.Max.X - LoveIslandKit.PadX * scale,
                    cover.Min.Y + LoveIslandKit.PadX * scale), scale);
            }

            var hovered = UiInteract.Hover(cover.Min, cover.Max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cover.Min, cover.Max, hovered))
            {
                OpenPhotos(urls, 0);
            }

            cursorY = cover.Max.Y;
        }

        cursorY += Metrics.Space.Lg * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), post.Title, ui.TitleInk, TextStyles.Title1,
            inner);
        cursorY += Metrics.Space.Xs * scale;
        var meta = postLabels.Stamp(post);
        Typography.Draw(drawList, new Vector2(left, cursorY), Typography.FitText(meta, inner, TextStyles.Subheadline),
            ui.MutedInk, TextStyles.Subheadline);
        if (post.Pinned)
        {
            var metaWidth = Typography.Measure(meta, TextStyles.Subheadline).X;
            Typography.Draw(drawList, new Vector2(left + metaWidth + Metrics.Space.Sm * scale, cursorY),
                Texts.T(CampaignTextKeys.Pinned), ui.Accent, TextStyles.SubheadlineEmphasized);
        }

        cursorY += Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Lg * scale;
        FeedCell.Hairline(drawList, left, left + inner, cursorY, ui.Hairline);
        cursorY += Metrics.Space.Lg * scale;
        cursorY = DrawPostBlocks(drawList, post, left, cursorY, inner, scale);
        if (string.IsNullOrEmpty(post.LinkUrl))
        {
            return cursorY;
        }

        cursorY += Metrics.Space.Xl * scale;
        if (DrawWideButton(drawList, left, cursorY, inner, Loc.T(L.Common.OpenLinkConfirm), PostOpenLinkId, true,
                out var bottom))
        {
            UrlActions.AskThenOpen(post.LinkUrl!);
        }

        return bottom;
    }

    private float DrawPostBlocks(ImDrawListPtr drawList, CampaignPostDto post, float left, float top, float width,
        float scale)
    {
        if (!postBodies.TryGetValue(post.Id, out var body))
        {
            body = new PostBody();
            postBodies[post.Id] = body;
        }

        body.Sync(post.Body ?? string.Empty);
        var cursorY = top;
        for (var blockIndex = 0; blockIndex < body.Blocks.Length; blockIndex++)
        {
            if (body.OpensParagraph[blockIndex])
            {
                cursorY += ParagraphGap * scale;
            }

            var block = body.Blocks[blockIndex];
            RichTextLayout? layout;
            using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
            {
                layout = LinkText.LayoutFor(block, width);
            }

            if (layout is null)
            {
                cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), block, ui.BodyInk, TextStyles.Body,
                    width);
                continue;
            }

            using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
            {
                LinkText.Draw(drawList, layout, new Vector2(left, cursorY), 1f, ui.BodyInk, ui.Accent, 1f, true);
            }

            cursorY += layout.Size.Y;
        }

        return cursorY;
    }
}

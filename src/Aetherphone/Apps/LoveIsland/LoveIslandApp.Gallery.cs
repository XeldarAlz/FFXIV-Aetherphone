using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const int GridColumns = 3;
    private const float GridGap = 2f;
    private const float GridBadgeInset = 12f;
    private const float GridBadgeSize = 16f;
    private const string GalleryPostButtonId = "loveisland.gallery.post";

    private static readonly Vector4 ThumbFill = new(1f, 1f, 1f, 0.06f);

    private readonly CampaignTextSlot gallerySub = new();
    private CachedText galleryCountText;

    private void DrawGalleryTab(Rect area)
    {
        gallery.EnsureFresh(Ledger.Hashtag);
        var frame = BeginRoot(area, out var context);
        using (var surface = AppSurface.BeginEdgeToEdge(frame.Body))
        {
            ConsumeScrollTop(surface);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            var posts = gallery.Posts;
            var count = posts.Length;
            var countText = galleryCountText.IsCurrent(count)
                ? galleryCountText.Value
                : galleryCountText.Store(count, NumberText.Group(count));
            var sub = gallerySub.Get(Texts, CampaignTextKeys.GallerySub, new CampaignTextArgs(N: countText));
            var cursorY = DrawPageTitle(drawList, left, origin.Y, inner, labels.HashtagTitle, sub);
            if (posts.Length == 0)
            {
                cursorY = DrawGalleryEmpty(left, cursorY, inner);
            }
            else
            {
                cursorY = DrawGrid(drawList, posts, origin.X, cursorY, width, scale);
            }

            cursorY += Metrics.Space.Lg * scale;
            if (DrawWideButton(drawList, left, cursorY, inner, labels.GalleryCta, GalleryPostButtonId, true,
                    out var buttonBottom))
            {
                PostWithHashtag();
            }

            Reserve(origin, width, buttonBottom);
            if (gallery.LoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, ui.MutedInk);
            }
            else if (gallery.HasMore && InfiniteScroll.ReachedBottom())
            {
                gallery.LoadMore();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }

        EndRoot(in frame, in context, "loveisland.nav.gallery");
    }

    private float DrawGalleryEmpty(float left, float top, float width)
    {
        if (gallery.Loading && !gallery.LoadedOnce)
        {
            var scale = UiScale.Current;
            LoadingPulse.Draw(new Vector2(left + width * 0.5f, top + LoadingOffset * 0.5f * scale),
                LoadingRadius * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading), 1f, 0.80f,
                ImGui.GetWindowDrawList());
            return top + (LoadingOffset * 0.5f + LoadingRadius * 4f) * scale;
        }

        return top + Typography.DrawWrappedLeft(new Vector2(left, top),
            Texts.T(CampaignTextKeys.ShareBody), ui.MutedInk, TextStyles.Body, width);
    }

    private float DrawGrid(ImDrawListPtr drawList, PostDto[] posts, float left, float top, float width, float scale)
    {
        var gap = GridGap * scale;
        var side = (width - gap * (GridColumns - 1)) / GridColumns;
        var rows = (posts.Length + GridColumns - 1) / GridColumns;
        for (var index = 0; index < posts.Length; index++)
        {
            var column = index % GridColumns;
            var row = index / GridColumns;
            var min = new Vector2(left + column * (side + gap), top + row * (side + gap));
            var max = min + new Vector2(side, side);
            if (!ImGui.IsRectVisible(min, max))
            {
                continue;
            }

            DrawGridTile(drawList, posts[index], min, max, scale);
        }

        if (posts.Length > 0)
        {
            UiAnchors.Report("loveisland.gallery.first",
                new Rect(new Vector2(left, top), new Vector2(left + side, top + side)));
        }

        return top + rows * side + Math.Max(0, rows - 1) * gap;
    }

    private void DrawGridTile(ImDrawListPtr drawList, PostDto post, Vector2 min, Vector2 max, float scale)
    {
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
        if (veiled)
        {
            SensitiveVeil.Draw(drawList, min, max, 0f);
        }
        else
        {
            var texture = images.Sized(photos.Length > 0 ? photos[0] : null,
                MathF.Max(max.X - min.X, max.Y - min.Y) * LoveIslandKit.ThumbOversample);
            if (texture is null)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(ThumbFill));
            }
            else
            {
                var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
                drawList.AddImage(texture.Handle, min, max, uv0, uv1);
                if (photos.Length > 1)
                {
                    PhoneIcon.Draw(drawList, new Vector2(max.X - GridBadgeInset * scale, min.Y + GridBadgeInset * scale),
                        PhoneIcons.Copy, LoveIslandKit.White, GridBadgeSize * scale);
                }
            }
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        if (veiled)
        {
            SensitiveReveals.Reveal(post.Id);
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        socialLauncher.Request(AethergramAppId, new SocialDeepLink(SocialLinkKind.Post, post.Id));
        navigation.Open(AethergramAppId);
    }
}

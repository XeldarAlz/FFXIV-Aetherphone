using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const int GridColumns = 3;
    private const int MosaicBlock = 6;
    private const float MosaicGap = 5f;
    private const float MosaicRadius = 12f;
    private const float GridBadgeInset = 12f;
    private const float GridBadgeSize = 16f;
    private const float TileAppearSmooth = 0.14f;
    private const float TileAppearScale = 0.90f;
    private const float TileHoverLift = 0.03f;
    private const long NewPostSeconds = 900L;
    private const float EmptyEmblem = 44f;
    private const float EmptyPad = 22f;
    private const double EmptyBreath = 3000.0;
    private const int StripSlots = 4;
    private const float StripGap = 12f;
    private const float StripTileGap = 6f;
    private const float StripRadius = 10f;
    private const string GalleryPostButtonId = "loveisland.gallery.post";

    private static readonly Vector4 ThumbFill = new(1f, 1f, 1f, 0.06f);

    private readonly CampaignTextSlot gallerySub = new();
    private readonly Dictionary<string, double> tileAppear = new(StringComparer.Ordinal);
    private CachedText galleryCountText;

    private void DrawGalleryTab(Rect area)
    {
        gallery.EnsureFresh(Ledger.Hashtag);
        var frame = BeginRoot(area, out var context);
        using (var surface = AppSurface.BeginEdgeToEdge(frame.Body))
        {
            PrepareRootSurface(surface);
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
            var cursorY = DrawPageTitle(drawList, left, origin.Y, inner, labels.HashtagTitle, sub, posts.Length > 0);
            if (posts.Length == 0)
            {
                cursorY = DrawGalleryEmpty(drawList, left, cursorY, inner);
            }
            else
            {
                cursorY = DrawGrid(drawList, posts, left, cursorY, inner, scale);
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

    private float DrawGalleryEmpty(ImDrawListPtr drawList, float left, float top, float width)
    {
        var scale = UiScale.Current;
        if (gallery.Loading && !gallery.LoadedOnce)
        {
            LoadingPulse.Draw(new Vector2(left + width * 0.5f, top + LoadingOffset * 0.5f * scale),
                LoadingRadius * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading), 1f, 0.80f, drawList);
            return top + (LoadingOffset * 0.5f + LoadingRadius * 4f) * scale;
        }

        var firstVertex = CardEntrance.Mark(drawList);
        var pad = EmptyPad * scale;
        var emblem = EmptyEmblem * scale;
        var body = Texts.T(CampaignTextKeys.ShareBody);
        var textWidth = MathF.Max(1f, width - pad * 2f);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Body, textWidth).Y;
        var height = pad * 2f + emblem * 2.2f + Metrics.Space.Lg * scale + bodyHeight;
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        LoveIslandKit.FeaturedCard(drawList, card.Min, card.Max, LoveIslandKit.CardRadius * scale, ui.Accent, scale);
        var center = new Vector2(card.Center.X, top + pad + emblem * 1.1f);
        var breath = EclipseFx.Breath(EmptyBreath);
        SoftGlow.Draw(drawList, center, emblem * (1.5f + 0.15f * breath),
            ui.Accent with { W = 0.55f + 0.2f * breath });
        drawList.AddCircleFilled(center, emblem, ImGui.GetColorU32(LoveIslandKit.Night with { W = 0.85f }), 48);
        drawList.AddCircle(center, emblem, ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.5f)), 48, 1.5f * scale);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Camera, EclipseFx.Rose(ui.Accent, 0.75f), emblem * 0.9f);
        DrawEmblemMotes(drawList, center, emblem * 1.35f, scale);
        Typography.DrawWrappedCentered(new Vector2(card.Center.X, center.Y + emblem * 1.1f + Metrics.Space.Lg * scale),
            body, ui.BodyInk, TextStyles.Body, textWidth);
        entrance.Settle(drawList, firstVertex, scale);
        return card.Max.Y;
    }

    private float DrawGrid(ImDrawListPtr drawList, PostDto[] posts, float left, float top, float width, float scale)
    {
        var gap = MosaicGap * scale;
        var cell = (width - gap * (GridColumns - 1)) / GridColumns;
        var bottom = top;
        for (var index = 0; index < posts.Length; index++)
        {
            var tile = MosaicTile(index, left, top, cell, gap);
            bottom = MathF.Max(bottom, tile.Max.Y);
            var firstVertex = CardEntrance.Mark(drawList);
            if (ImGui.IsRectVisible(tile.Min, tile.Max))
            {
                DrawGridTile(drawList, posts[index], tile, scale);
            }

            entrance.Settle(drawList, firstVertex, scale);
        }

        if (posts.Length > 0)
        {
            UiAnchors.Report("loveisland.gallery.first", MosaicTile(0, left, top, cell, gap));
        }

        return bottom;
    }

    private int PhotoStripCount()
    {
        var posts = gallery.Posts;
        var count = 0;
        for (var index = 0; index < posts.Length && count < StripSlots; index++)
        {
            if (StripEligible(posts[index]))
            {
                count++;
            }
        }

        return count;
    }

    private bool StripEligible(PostDto post) =>
        FirstPhoto(post) is not null
        && !SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);

    private static string? FirstPhoto(PostDto post)
    {
        if (post.MediaUrls is { Length: > 0 } urls)
        {
            return urls[0];
        }

        return string.IsNullOrEmpty(post.MediaUrl) ? null : post.MediaUrl;
    }

    private static float PhotoStripHeight(float width, float scale) =>
        (width - StripTileGap * scale * (StripSlots - 1)) / StripSlots;

    private void DrawPhotoStrip(ImDrawListPtr drawList, float left, float top, float width, int count, float scale)
    {
        var side = PhotoStripHeight(width, scale);
        var gap = StripTileGap * scale;
        var rounding = StripRadius * scale;
        var posts = gallery.Posts;
        var slot = 0;
        for (var index = 0; index < posts.Length && slot < count; index++)
        {
            var post = posts[index];
            if (!StripEligible(post))
            {
                continue;
            }

            var min = new Vector2(left + slot * (side + gap), top);
            var tile = new Rect(min, min + new Vector2(side, side));
            slot++;
            var texture = images.Sized(FirstPhoto(post), side * LoveIslandKit.ThumbOversample);
            if (texture is null)
            {
                Squircle.Fill(drawList, tile.Min, tile.Max, rounding, ImGui.GetColorU32(ThumbFill));
                EclipseFx.Glint(drawList, tile.Min, tile.Max, rounding, 0.10f, LoveIslandKit.GlintOffset(post.Id));
            }
            else
            {
                DrawTilePhoto(drawList, post, texture, tile, rounding, false, false, scale);
            }

            var hovered = UiInteract.Hover(tile.Min, tile.Max);
            Squircle.Stroke(drawList, tile.Min, tile.Max, rounding,
                ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.5f) with { W = hovered ? 0.85f : 0.15f }), scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(tile.Min, tile.Max, hovered))
            {
                UiFeedback.Play(UiSound.Tap);
                SelectTab(LoveIslandTab.Gallery);
            }
        }
    }

    private static Rect MosaicTile(int index, float left, float top, float cell, float gap)
    {
        var block = index / MosaicBlock;
        var within = index % MosaicBlock;
        var mirrored = (block & 1) == 1;
        var step = cell + gap;
        var blockTop = top + block * GridColumns * step;
        int column;
        int row;
        var span = 1;
        switch (within)
        {
            case 0:
                column = mirrored ? 1 : 0;
                row = 0;
                span = 2;
                break;
            case 1:
                column = mirrored ? 0 : 2;
                row = 0;
                break;
            case 2:
                column = mirrored ? 0 : 2;
                row = 1;
                break;
            default:
                column = within - 3;
                row = 2;
                break;
        }

        var min = new Vector2(left + column * step, blockTop + row * step);
        var side = span * cell + (span - 1) * gap;
        return new Rect(min, min + new Vector2(side, side));
    }

    private void DrawGridTile(ImDrawListPtr drawList, PostDto post, Rect rest, float scale)
    {
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var lift = HoverFx.Amount(post.Id, hovered);
        var half = rest.Size * 0.5f * (1f + TileHoverLift * lift);
        var tile = new Rect(rest.Center - half, rest.Center + half);
        var rounding = MosaicRadius * scale;
        var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
        if (lift > 0.01f)
        {
            EclipseFx.Bloom(drawList, tile.Min, tile.Max, rounding, 10f * scale, ui.Accent with { W = 0.45f * lift });
        }

        if (veiled)
        {
            SensitiveVeil.Draw(drawList, tile.Min, tile.Max, rounding);
        }
        else
        {
            var texture = images.Sized(FirstPhoto(post),
                MathF.Max(rest.Width, rest.Height) * LoveIslandKit.ThumbOversample);
            if (texture is null)
            {
                Squircle.Fill(drawList, tile.Min, tile.Max, rounding, ImGui.GetColorU32(ThumbFill));
                EclipseFx.Glint(drawList, tile.Min, tile.Max, rounding, 0.10f, LoveIslandKit.GlintOffset(post.Id));
            }
            else
            {
                DrawTilePhoto(drawList, post, texture, tile, rounding, post.MediaUrls is { Length: > 1 }, true, scale);
            }
        }

        Squircle.Stroke(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.5f) with { W = 0.12f + 0.75f * lift }), (1f + lift) * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(rest.Min, rest.Max, hovered))
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

    private void DrawTilePhoto(ImDrawListPtr drawList, PostDto post, IDalamudTextureWrap texture, Rect tile,
        float rounding, bool multiple, bool showNew, float scale)
    {
        if (!tileAppear.TryGetValue(post.Id, out var appearedAt))
        {
            appearedAt = ImGui.GetTime();
            tileAppear[post.Id] = appearedAt;
        }

        var appear = Spring.Settle((float)(ImGui.GetTime() - appearedAt), TileAppearSmooth);
        var firstVertex = CardEntrance.Mark(drawList);
        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, tile.Width, tile.Height);
        Squircle.FillImage(drawList, tile.Min, tile.Max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        if (multiple)
        {
            var badge = new Vector2(tile.Max.X - GridBadgeInset * scale, tile.Min.Y + GridBadgeInset * scale);
            PhoneIcon.Draw(drawList, badge, PhoneIcons.Copy, LoveIslandKit.White, GridBadgeSize * scale);
        }

        if (showNew && post.CreatedAtUnix >= nowUnix - NewPostSeconds)
        {
            var inset = Metrics.Space.Sm * scale;
            LoveIslandKit.LivePill(drawList, new Vector2(tile.Min.X + inset, tile.Min.Y + inset),
                Loc.T(L.LoveIsland.NewBadge), ui.Accent, scale);
        }

        if (appear >= 0.999f)
        {
            return;
        }

        VertexWarp.Scale(drawList, firstVertex, tile.Center, TileAppearScale + (1f - TileAppearScale) * appear);
        CardEntrance.Fade(drawList, firstVertex, appear);
    }
}

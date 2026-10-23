using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float BannerThumbUnits = 56f;
    private const float PickedMarkUnits = 22f;
    private const float PickedGlyphShare = 0.62f;
    private const int PickedMarkSegments = 32;
    private const float CropRubberLimit = 0.36f;
    private const float CropRubberGive = 0.62f;
    private const float CropOverflowEpsilon = 0.5f;
    private const uint CropOutsideTint = 0x4DFFFFFFu;
    private const string CropClaimId = "prism.bannerCrop";

    private static readonly GlamBannerDto AutomaticBanner = new();

    private GlamBannerDto? editBanner;
    private GlamDto? editBannerGlam;
    private GlamDto? cropGlam;
    private Spring cropFocusX;
    private Spring cropFocusY;
    private Vector2 cropTarget;
    private Vector2 cropFrom;
    private Vector2 cropPress;

    private GlamBannerDto? OwnBanner() => glams.Me?.BannerGlam;

    private static bool SameBanner(GlamBannerDto? banner, GlamBannerDto? other)
    {
        var id = banner?.GlamId ?? string.Empty;
        if (id != (other?.GlamId ?? string.Empty))
        {
            return false;
        }

        return id.Length == 0 || banner!.FocusX == other!.FocusX && banner.FocusY == other.FocusY;
    }

    private GlamDto? FindBanner(string glamId, FeedLane<GlamDto> lane, out bool missing)
    {
        missing = false;
        var items = lane.Items;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id == glamId)
            {
                return items[index];
            }
        }

        return lane.Loading ? null : glams.BannerGlam(glamId, out missing);
    }

    private float DrawBannerRow(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.Prism.Banner), width, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        var height = BannerThumbUnits * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var thumb = new Rect(row.Min, new Vector2(left + height / CoverShare, row.Max.Y));
        var hovered = HoverWash(drawList, row);
        var chosen = editBanner is { GlamId.Length: > 0 } banner ? banner : null;
        if (chosen is not null)
        {
            editBannerGlam ??= FindBanner(chosen.GlamId, glams.List, out _);
        }

        var glam = chosen is not null && editBannerGlam?.Id == chosen.GlamId ? editBannerGlam : null;
        if (glam is not null)
        {
            DrawLook(drawList, thumb, Cover(glam), false, focusY: chosen!.FocusY, focusX: chosen.FocusX);
        }
        else
        {
            PrismArt.Placeholder(drawList, thumb, false, scale);
            if (chosen is null)
            {
                PhoneIcon.Draw(drawList, thumb.Center, PhoneIcons.Sparkles, PrismArt.Muted,
                    PrismArt.IconUnits * scale);
            }
        }

        var glyph = PrismArt.IconUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - glyph * 0.5f, row.Center.Y), PhoneIcons.ChevronRight,
            PrismArt.Muted, glyph);
        var textLeft = thumb.Max.X + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, row.Max.X - glyph - Metrics.Space.Sm * scale - textLeft);
        var title = Typography.FitText(chosen is null ? Loc.T(L.Prism.BannerAutomatic) : glam?.Title ?? string.Empty,
            room, PrismType.Headline);
        var hint = chosen is null ? Loc.T(L.Prism.BannerAutomaticHint) : string.Empty;
        var titleHeight = Typography.LineHeight(PrismType.Headline);
        var hintHeight = hint.Length > 0 ? Typography.MeasureWrappedBlock(hint, PrismType.Caption, room).Y : 0f;
        var textTop = row.Center.Y - (titleHeight + hintHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), title, PrismArt.Fore, PrismType.Headline);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(textLeft, textTop + titleHeight), hint, PrismArt.Muted,
                PrismType.Caption, room);
        }
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenBannerPick();
        }

        return row.Max.Y + Metrics.Space.Md * scale;
    }

    private void OpenBannerPick()
    {
        if (glams.Me is { } me)
        {
            showSaved = false;
            glams.OpenProfile(me);
        }

        Push(PrismScreen.BannerPick);
    }

    private void DrawBannerPick(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        var top = DrawBackHeader(drawList, content, Loc.T(L.Prism.Banner), scale) + Metrics.Space.Sm * scale;
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = Metrics.Space.Xxs * scale;
        var left = origin.X + gap;
        var gridWidth = width - gap * 2f;
        var chosen = editBanner?.GlamId ?? string.Empty;
        if (DrawAutomaticTile(list, GridCell(left, origin.Y, gridWidth, 0, scale, ExploreColumns), chosen.Length == 0,
                scale))
        {
            editBanner = null;
            editBannerGlam = null;
            GoBack();
            return;
        }

        var lane = glams.List;
        var items = lane.Items;
        var shown = 1;
        for (var index = 0; index < items.Length; index++)
        {
            var glam = items[index];
            if (glam.Photos is not { Length: > 0 })
            {
                continue;
            }

            var cell = GridCell(left, origin.Y, gridWidth, shown++, scale, ExploreColumns);
            if (DrawGridTile(list, cell, Cover(glam), string.Empty, string.Empty, string.Empty, scale))
            {
                OpenBannerCrop(glam);
            }

            if (glam.Id == chosen)
            {
                DrawPickedMark(list, cell, scale);
            }
        }

        var bottom = origin.Y + GridHeight(gridWidth, shown, scale, ExploreColumns);
        if (items.Length == 0 && (lane.Loading || lane.Failed))
        {
            bottom = DrawLaneState(list, origin.X, width, bottom, lane, scale);
        }
        else if (items.Length == 0)
        {
            bottom = DrawEmpty(list, EmptyArea(origin.X, width, bottom, scale), PhoneIcons.Photo,
                Loc.T(L.Prism.FirstGlamTitle), Loc.T(L.Prism.FirstGlamHint), Loc.T(L.Prism.PostGlam),
                ButtonStyle.Solid, scale, out var post);
            if (post)
            {
                StartCompose();
            }
        }
        else if (lane.LoadingMore)
        {
            bottom = DrawLoadingRowBelow(origin.X, width, bottom);
        }

        ImGui.Dummy(new Vector2(width, bottom - origin.Y + PrismArt.BottomMargin(scale)));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }

    private static bool DrawAutomaticTile(ImDrawListPtr drawList, Rect cell, bool picked, float scale)
    {
        PrismArt.Placeholder(drawList, cell, false, scale);
        var hovered = HoverWash(drawList, cell);
        var glyph = PrismArt.EmptyIconUnits * scale;
        var line = Typography.LineHeight(PrismType.Caption);
        var gap = Metrics.Space.Xs * scale;
        var top = cell.Center.Y - (glyph + gap + line) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(cell.Center.X, top + glyph * 0.5f), PhoneIcons.Sparkles, PrismArt.Muted,
            glyph);
        var label = Typography.FitText(Loc.T(L.Prism.BannerAutomatic),
            MathF.Max(1f, cell.Width - Metrics.Space.Sm * 2f * scale), PrismType.Caption);
        Typography.DrawCentered(drawList, new Vector2(cell.Center.X, top + glyph + gap + line * 0.5f), label,
            PrismArt.Fore, PrismType.Caption);
        if (picked)
        {
            DrawPickedMark(drawList, cell, scale);
        }

        return UiInteract.Click(cell.Min, cell.Max, hovered);
    }

    private static void DrawPickedMark(ImDrawListPtr drawList, Rect cell, float scale)
    {
        var radius = PickedMarkUnits * 0.5f * scale;
        var inset = Metrics.Space.Sm * scale + radius;
        var center = new Vector2(cell.Max.X - inset, cell.Min.Y + inset);
        drawList.AddCircleFilled(center, radius, Palette.Pack(PrismArt.PhotoFore), PickedMarkSegments);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Check, PrismArt.Ink, radius * 2f * PickedGlyphShare);
    }

    private void OpenBannerCrop(GlamDto glam)
    {
        cropGlam = glam;
        cropTarget = editBanner is { } current && current.GlamId == glam.Id
            ? new Vector2(current.FocusX, current.FocusY)
            : new Vector2(ImageFit.CenterFocus, ProfileCoverFocus);
        cropFocusX.SnapTo(cropTarget.X);
        cropFocusY.SnapTo(cropTarget.Y);
        Push(PrismScreen.BannerCrop);
    }

    private void DrawBannerCrop(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        var top = DrawActionHeader(drawList, content, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back),
            Loc.T(L.Prism.Banner), PhoneIcons.Check, Loc.T(L.Prism.Done), cropGlam is not null, string.Empty,
            UiSound.Tap, scale, out var done) + Metrics.Space.Sm * scale;
        if (cropGlam is not { } glam)
        {
            return;
        }

        if (done)
        {
            editBanner = new GlamBannerDto(glam.Id, cropTarget.X, cropTarget.Y);
            editBannerGlam = glam;
            GoBack();
            GoBack();
            return;
        }

        var pad = Metrics.Space.Lg * scale;
        var hint = Loc.T(L.Prism.BannerDragHint);
        var hintWrap = PrismType.BalancedWidth(hint, PrismType.Body, content.Width - pad * 2f);
        var hintHeight = Typography.MeasureWrappedBlock(hint, PrismType.Body, hintWrap).Y;
        var area = new Rect(new Vector2(full.Min.X, top),
            new Vector2(full.Max.X, content.Max.Y - hintHeight - pad * 2f));
        var frameHeight = full.Width * CoverShare - PhotoInset(full, content);
        var frameTop = MathF.Max(area.Min.Y, area.Center.Y - frameHeight * 0.5f);
        var frame = new Rect(new Vector2(full.Min.X, frameTop), new Vector2(full.Max.X, frameTop + frameHeight));
        DrawCropPhoto(drawList, area, frame, Cover(glam), scale);
        DrawCoverIdentity(drawList, frame, glams.Me, ShowsOwnProfile() ? BestPlacing(glams.Profile) : null,
            frame.Max.X - pad, scale);
        Typography.DrawWrappedCentered(new Vector2(content.Center.X, area.Max.Y + pad), hint, PrismArt.Muted,
            PrismType.Body, hintWrap);
    }

    private void DrawCropPhoto(ImDrawListPtr drawList, Rect area, Rect frame, string url, float scale)
    {
        var texture = images.Get(url) ?? images.Nearest(url, MathF.Max(frame.Width, frame.Height));
        if (texture is null)
        {
            PrismArt.Placeholder(drawList, frame, images.Failed(url), scale);
            return;
        }

        var size = texture.Size;
        var drawn = size * MathF.Max(frame.Width / MathF.Max(1f, size.X), frame.Height / MathF.Max(1f, size.Y));
        var overflow = drawn - frame.Size;
        var active = ClaimCrop(area);
        if (active)
        {
            var travel = ImGui.GetMousePos() - cropPress;
            var x = Pan(cropFrom.X, travel.X, overflow.X, frame.Width);
            var y = Pan(cropFrom.Y, travel.Y, overflow.Y, frame.Height);
            cropFocusX.SnapTo(x);
            cropFocusY.SnapTo(y);
            cropTarget = new Vector2(Math.Clamp(x, 0f, 1f), Math.Clamp(y, 0f, 1f));
        }
        else
        {
            var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
            cropFocusX.Step(cropTarget.X, Motion.Release, delta);
            cropFocusY.Step(cropTarget.Y, Motion.Release, delta);
        }

        var origin = frame.Min - new Vector2(cropFocusX.Value * overflow.X, cropFocusY.Value * overflow.Y);
        drawList.PushClipRect(area.Min, area.Max, true);
        drawList.AddImage(texture.Handle, origin, origin + drawn, Vector2.Zero, Vector2.One, CropOutsideTint);
        drawList.PopClipRect();
        drawList.PushClipRect(frame.Min, frame.Max, true);
        drawList.AddImage(texture.Handle, origin, origin + drawn);
        drawList.PopClipRect();
    }

    private bool ClaimCrop(Rect frame)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(frame.Min);
        ImGui.InvisibleButton(CropClaimId, frame.Size);
        var active = ImGui.IsItemActive();
        if (ImGui.IsItemActivated())
        {
            cropPress = ImGui.GetMousePos();
            cropFrom = new Vector2(cropFocusX.Value, cropFocusY.Value);
        }

        if (active || UiInteract.Hover(frame.Min, frame.Max))
        {
            UiInteract.ReportGestureSurface();
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        }

        ImGui.SetCursorScreenPos(cursor);
        return active;
    }

    private static float Pan(float from, float travel, float overflow, float extent)
    {
        if (overflow <= CropOverflowEpsilon)
        {
            return from;
        }

        var raw = from - travel / overflow;
        if (raw is >= 0f and <= 1f)
        {
            return raw;
        }

        var excess = (raw < 0f ? -raw : raw - 1f) * overflow / extent;
        var give = CropRubberLimit * excess / (excess + CropRubberGive) * extent / overflow;
        return raw < 0f ? -give : 1f + give;
    }
}

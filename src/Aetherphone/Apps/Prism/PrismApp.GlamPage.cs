using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Report;
using Aetherphone.Core.Sharing;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Prism;

internal enum GlamMenuAction : byte
{
    Share,
    Wardrobe,
    Report,
    Delete,
}

internal sealed partial class PrismApp
{
    private const float MutedOnPhoto = 0.72f;
    private const float DetailTitleUnits = PrismType.TitleUnits;
    private const float DetailTitleMinUnits = PrismType.HeadingUnits;
    private const string Arrow = "→";
    private const string GlamMetaSeparator = " · ";
    private const string LikeSlotTemplate = "9.9K";
    private const string PillId = "prism.tryOn";
    private const string FollowId = "prism.follow";
    private const int AvatarSegments = 32;
    private const int GlamMenuSize = 3;
    private const float LikeBloomSeconds = 0.9f;
    private const float LikeHoldSeconds = 0.45f;
    private const float LikeStamp = 0.35f;
    private const float PostAvatarUnits = 24f;
    private const float GlamRowUnits = 56f;
    private const float PagerSlopUnits = 12f;
    private const float PagerSlopBias = 1.2f;
    private const float DotsShare = 0.3f;
    private const float DotPitchUnits = 12f;

    private readonly DoubleTapLike photoLike = new();
    private readonly DeferredTap photoTap = new();
    private readonly Pager photoPager = new();
    private readonly ActionSheet glamMenu = new();
    private readonly ActionSheet.Item[] glamMenuItems = new ActionSheet.Item[GlamMenuSize];
    private readonly GlamMenuAction[] glamMenuActions = new GlamMenuAction[GlamMenuSize];
    private readonly string[] pillLabels = new string[3];
    private readonly Dictionary<string, string> handleLines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tagLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> followed = new(StringComparer.Ordinal);
    private readonly ChipRail tagRail = new();
    private readonly Dictionary<uint, (string Name, uint Icon)> itemRows = new();
    private readonly HashSet<uint> ownedIds = new();
    private readonly Dictionary<int, string> dyeLines = new();
    private readonly Dictionary<GlamModDto, (string Line, bool Allowed, bool Installed)> modLines = new();
    private readonly Dictionary<GlamModDto, string> modBylines = new();
    private string? statsFor;
    private bool statsStorage;
    private CachedText triedText;
    private CachedText bodyText;
    private CachedText moreText;
    private CachedText getText;
    private CachedText metaText;
    private CachedText ownedCountText;
    private CachedText installedText;
    private int ownedPieces;
    private int totalPieces;
    private int missingMods;
    private int totalMods;
    private int glamMenuCount;
    private bool photoPressed;
    private Vector2 photoPressAt;
    private string likeBloomId = string.Empty;
    private double likeBloomAt;
    private bool likeBloomOnPhoto;
    private string tryOnKey = string.Empty;
    private double tryOnAt;
    private string linesLanguage = string.Empty;
    private string[]? tagRailFor;
    private string[] tagRailLabels = Array.Empty<string>();
    private bool[] tagRailActive = Array.Empty<bool>();

    private void OpenGlam(GlamDto glam)
    {
        Push(PrismScreen.GlamDetail);
        ShowGlam(glam);
    }

    private void ShowGlam(GlamDto glam)
    {
        glams.Open(glam);
        photoPager.SnapTo(0, 1);
        statsFor = null;
        photoTap.Cancel();
        ResetPieces();
    }

    private IDalamudTextureWrap? ViewerPage(int index) =>
        glams.Detail?.Photos is { } photos && index < photos.Length ? images.Get(photos[index].Url) : null;

    private static string WearKey(GlamDto glam) =>
        glam.Id.Length > 0 ? glam.Id : glam.Photos is { Length: > 0 } photos ? photos[0].Url : string.Empty;

    private bool GearItem(uint itemId, out string name, out uint iconId)
    {
        if (!itemRows.TryGetValue(itemId, out var row))
        {
            if (!gameData.TryGetItem(itemId, out var found, out var icon, out _))
            {
                name = string.Empty;
                iconId = 0;
                return false;
            }

            row = (found, icon);
            itemRows[itemId] = row;
        }

        name = row.Name;
        iconId = row.Icon;
        return true;
    }

    private string AuthorLine(GlamAuthorDto? author)
    {
        if (author is null || author.Handle.Length == 0)
        {
            return author?.DisplayName ?? string.Empty;
        }

        if (!handleLines.TryGetValue(author.Handle, out var line))
        {
            line = "@" + author.Handle;
            handleLines[author.Handle] = line;
        }

        return line;
    }

    private void DrawGlamDetail(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (glams.Detail is not { } glam)
        {
            GoBack();
            return;
        }

        if (glams.DetailPending || glams.DetailMissing)
        {
            DrawGlamLoading(drawList, content, glam, glams.DetailMissing, scale);
            return;
        }

        glamMenu.Gate();
        RefreshStats(glam);
        var full = SceneChrome.ScreenFrom(content, followedTheme!, scale);
        var key = WearKey(glam);
        using (BeginBody(new Rect(new Vector2(full.Min.X, PhotoTop(full, content)), new Vector2(full.Max.X, content.Max.Y))))
        {
            DrawGlamBody(ImGui.GetWindowDrawList(), full, content, glam, key, scale);
        }

        DrawGlamMenu(full, glam, key);
    }

    private void DrawGlamLoading(ImDrawListPtr drawList, Rect content, GlamDto glam, bool missing, float scale)
    {
        var top = DrawBackHeader(drawList, content, string.Empty, scale) + Metrics.Space.Lg * scale;
        top += Typography.DrawWrappedLeft(new Vector2(content.Min.X, top),
            Loc.T(missing ? L.Common.LoadFailedHint : L.Common.Loading), PrismArt.Muted, PrismType.Body,
            content.Width) + Metrics.Space.Sm * scale;
        var retry = Loc.T(L.Common.Retry);
        var rect = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Min.X + ButtonWidth(new ReadOnlySpan<string>(ref retry), content.Width, scale),
                top + Button.LargeHeight * scale));
        if (missing && DrawButton(drawList, rect, retry, ButtonStyle.Plain, scale))
        {
            glams.Open(glam);
        }
    }

    private void DrawGlamBody(ImDrawListPtr drawList, Rect full, Rect content, GlamDto glam, string key, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var photoTop = ImGui.GetWindowPos().Y - ImGui.GetScrollY();
        var photo = new Rect(new Vector2(full.Min.X, photoTop),
            new Vector2(full.Max.X, photoTop + full.Width / GlamStore.PhotoRatio));
        var gear = glam.Gear ?? Array.Empty<GlamGearDto>();
        DrawGlamPhoto(drawList, photo, PhotoInset(full, content), glam, gear, key, scale);
        var left = full.Min.X + Metrics.Space.Lg * scale;
        var right = full.Max.X - Metrics.Space.Lg * scale;
        var rowTop = photo.Max.Y + Metrics.Space.Sm * scale;
        var cursor = rowTop + (Metrics.Size.TapTarget + Metrics.Space.Sm) * scale;
        cursor = DrawGlamHeading(drawList, left, right, cursor, glam, scale);
        cursor = DrawPieceSection(drawList, left, right, cursor, gear, scale);
        cursor = DrawModSection(drawList, left, right, cursor, gear, scale);
        if (glam.WeekId.Length > 0 && totalPieces > 0)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.OutfitCaveat),
                PrismArt.Muted, PrismType.Caption, right - left);
        }

        cursor = DrawMoreFrom(drawList, full, cursor, glam, scale);
        DrawActionRow(drawList, full, rowTop, content.Min.Y, glam, key, scale);
        ImGui.Dummy(new Vector2(full.Width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private void RefreshStats(GlamDto glam)
    {
        var key = WearKey(glam);
        var storage = OwnedItems.StorageLoaded;
        if (statsFor == key && statsStorage == storage)
        {
            return;
        }

        statsFor = key;
        statsStorage = storage;
        modLines.Clear();
        modBylines.Clear();
        var gear = glam.Gear ?? Array.Empty<GlamGearDto>();
        ownedPieces = GlamourService.OwnedPieces(gear, out totalPieces, ownedIds);
        missingMods = glamour.MissingMods(gear, out totalMods);
        BuildPieces(gear);
    }

    private void DrawGlamPhoto(ImDrawListPtr drawList, Rect photo, float topInset, GlamDto glam, GlamGearDto[] gear,
        string key, float scale)
    {
        var photos = glam.Photos ?? Array.Empty<GlamPhotoDto>();
        var count = photos.Length;
        photoPager.Step(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds), Math.Max(1, count));
        if (!piecesOpen)
        {
            DrivePhotoPager(photo, count, scale);
        }

        var scroll = photoPager.Value;
        var first = Math.Clamp((int)MathF.Floor(scroll), 0, Math.Max(0, count - 1));
        var last = Math.Clamp((int)MathF.Ceiling(scroll), 0, Math.Max(0, count - 1));
        if (count == 0)
        {
            PrismArt.Placeholder(drawList, photo, true, scale);
        }

        drawList.PushClipRect(photo.Min, photo.Max, true);
        for (var index = first; index <= last && count > 0; index++)
        {
            var offset = new Vector2((index - scroll) * photo.Width, 0f);
            DrawLook(drawList, new Rect(photo.Min + offset, photo.Max + offset), photos[index].Url, false,
                native: true);
        }

        drawList.PopClipRect();
        if (!PrismArt.Light)
        {
            PrismArt.TopFog(drawList, photo, scale);
        }

        DrawPhotoTaps(photo, glam, count);
        DrawPublished(drawList, photo, glam, count > 0 ? photos[0].Url : string.Empty, scale);
        var dots = new Vector2(photo.Center.X, photo.Max.Y - (Metrics.Space.Md + ChipHeightUnits * 0.5f) * scale);
        PhotoCarousel.DrawDots(drawList, dots, count, Math.Clamp((int)MathF.Round(scroll), 0, Math.Max(0, count - 1)),
            photo.Width * DotsShare, PrismArt.PhotoFore);
        DrawDotTaps(dots, photo.Width, count, scale);
        DrawPhotoLike(drawList, photo, glam, scale);
        if (tryOnKey == key)
        {
            PrismArt.TryOnMoment(drawList, photo, PhoneIcons.Wand, Loc.T(L.Prism.OnYourCharacter),
                (float)(ImGui.GetTime() - tryOnAt), scale);
        }

        DrawPiecesLayer(drawList, photo, topInset, gear, scale);
        DrawPiecesChip(drawList, photo, scale);
        if (DrawPhotoButton(drawList, photo, topInset, 0, PhoneIcons.ChevronLeft, string.Empty, scale))
        {
            if (piecesOpen)
            {
                piecesOpen = false;
            }
            else
            {
                LeaveScreen();
            }
        }

        var glass = Metrics.Size.GlassButton * 0.5f * scale;
        var inset = Metrics.Space.Md * scale + glass;
        var buttonY = photo.Min.Y + topInset + Metrics.Space.Xs * scale + glass;
        var reach = Metrics.Size.TapTarget * 0.5f * scale;
        Tooltip(new Vector2(photo.Min.X + inset, buttonY), reach, Loc.T(L.Prism.Back), true);
        if (glam.Id.Length == 0)
        {
            return;
        }

        Tooltip(new Vector2(photo.Max.X - inset, buttonY), reach, Loc.T(L.Prism.More), true);
        if (DrawPhotoButton(drawList, photo, topInset, 1, PhoneIcons.Dots, string.Empty, scale))
        {
            OpenGlamMenu(glam, key);
        }
    }

    private void DrawDotTaps(Vector2 center, float width, int count, float scale)
    {
        if (count < 2 || piecesOpen)
        {
            return;
        }

        var cell = MathF.Min(Metrics.Size.TapTarget * scale, width * 0.5f / count);
        var half = new Vector2(cell * count, Metrics.Size.TapTarget * scale) * 0.5f;
        var strip = new Rect(center - half, center + half);
        var hovered = UiInteract.HoverOverlay(strip);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(strip.Min, strip.Max, hovered))
        {
            var nearest = (ImGui.GetMousePos().X - center.X) /
                          MathF.Min(DotPitchUnits * scale, width * DotsShare / (count - 1)) + (count - 1) * 0.5f;
            photoPager.AnimateTo((int)MathF.Round(nearest), count);
        }
    }

    private void DrivePhotoPager(Rect photo, int count, float scale)
    {
        if (count < 2)
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (photoPager.Dragging)
        {
            photoPager.Drag(mouse.X, photo.Width, count, ImGui.GetIO().DeltaTime);
            if (!down)
            {
                photoPager.Release(photo.Width, count);
            }

            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(photo.Min, photo.Max))
        {
            photoPressed = true;
            photoPressAt = mouse;
        }

        var move = mouse - photoPressAt;
        if (!down || !photoPressed || MathF.Abs(move.X) <= PagerSlopUnits * scale ||
            MathF.Abs(move.X) <= MathF.Abs(move.Y) * PagerSlopBias)
        {
            photoPressed &= down;
            return;
        }

        photoPressed = false;
        photoPager.Begin(photoPressAt.X);
        photoTap.Cancel();
        UiInteract.CancelPendingTap();
    }

    private void DrawPhotoTaps(Rect photo, GlamDto glam, int count)
    {
        var hovered = UiInteract.Hover(photo.Min, photo.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (piecesOpen)
        {
            if (UiInteract.Click(photo.Min, photo.Max, hovered))
            {
                piecesOpen = false;
            }

            return;
        }

        if (count == 0 || photoPager.Dragging)
        {
            return;
        }

        var page = Math.Clamp(photoPager.Page, 0, count - 1);
        var likeable = glam.Id.Length > 0;
        if (likeable && photoLike.Tapped(photo, glam.Id))
        {
            photoTap.Cancel();
            if (!glam.Liked)
            {
                glams.ToggleLike(glam);
            }

            UiFeedback.Play(UiSound.ToggleOn);
            likeBloomId = glam.Id;
            likeBloomAt = ImGui.GetTime();
            likeBloomOnPhoto = true;
        }
        else if (!likeable && UiInteract.Click(photo.Min, photo.Max, hovered))
        {
            photoViewer.Open(this, count, page, viewerPage);
        }
        else if (likeable && !photoLike.SwallowedTap(glam.Id) && UiInteract.Click(photo.Min, photo.Max, hovered))
        {
            photoTap.Arm(glam.Id);
        }

        if (photoTap.Ready(out var tapped) && tapped == glam.Id)
        {
            photoViewer.Open(this, count, page, viewerPage);
        }
    }

    private void DrawPublished(ImDrawListPtr drawList, Rect frame, GlamDto glam, string coverUrl, float scale)
    {
        if (postedId.Length == 0 || glam.Id != postedId)
        {
            return;
        }

        if (loadedAt.TryGetValue(coverUrl, out var seen))
        {
            var sweep = PrismMotion.At((float)(ImGui.GetTime() - seen), 0.1f, Motion.Sheet);
            if (sweep < PrismMotion.Settled)
            {
                drawList.PushClipRect(frame.Min, frame.Max, true);
                PrismArt.Refraction(drawList, frame, sweep);
                drawList.PopClipRect();
            }
        }

        var label = Loc.T(L.Prism.Published);
        var pixels = PrismArt.LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, pixels);
        var pad = Metrics.Space.Md * scale;
        var max = frame.Max - new Vector2(pad, pad);
        var min = max - new Vector2(size.X + pad * 2f, ChipHeightUnits * scale);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(TileFill), (max.Y - min.Y) * 0.5f);
        PrismType.Draw(drawList, new Vector2(min.X + pad, (min.Y + max.Y - PrismType.Snap(PrismFace.Label, pixels)) * 0.5f),
            label, PrismArt.PhotoFore, PrismFace.Label, pixels);
    }

    private void DrawPhotoLike(ImDrawListPtr drawList, Rect frame, GlamDto glam, float scale)
    {
        var age = (float)(ImGui.GetTime() - likeBloomAt);
        if (!likeBloomOnPhoto || glam.Id != likeBloomId || age >= LikeBloomSeconds)
        {
            return;
        }

        drawList.PushClipRect(frame.Min, frame.Max, true);
        var size = MathF.Min(frame.Width, frame.Height) * 0.22f;
        var mark = PrismMotion.Mark(drawList);
        PhoneIcon.Draw(drawList, frame.Center, PhoneIcons.HeartFilled, PrismArt.PhotoFore, size);
        PrismMotion.Grow(drawList, mark, frame.Center, 0.6f + 0.4f * Spring.Settle(age, Motion.Appear));
        if (age > LikeHoldSeconds)
        {
            PrismMotion.Shift(drawList, mark, Vector2.Zero, 1f - PrismMotion.At(age, LikeHoldSeconds, Motion.Release));
        }

        DrawLikeRing(drawList, frame.Center, size * 0.6f, age, scale);
        drawList.PopClipRect();
    }

    private static void DrawLikeRing(ImDrawListPtr drawList, Vector2 center, float baseRadius, float age, float scale)
    {
        var grow = PrismMotion.At(age, 0f, Motion.Sheet);
        if (grow >= PrismMotion.Settled)
        {
            return;
        }

        var mark = PrismMotion.Mark(drawList);
        PrismArt.SpectrumRing(drawList, center, baseRadius * (1f + 1.6f * grow), grow * MathF.PI * 0.5f,
            MathF.Max(1f, 1.5f * scale));
        PrismMotion.Shift(drawList, mark, Vector2.Zero, 1f - grow);
    }

    private void DrawActionRow(ImDrawListPtr drawList, Rect full, float naturalTop, float pinTop, GlamDto glam,
        string key, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var radius = tap * 0.5f;
        var pinned = naturalTop < pinTop;
        var top = pinned ? pinTop : naturalTop;
        var start = full.Min.X + Metrics.Space.Lg * scale;
        var right = full.Max.X - Metrics.Space.Lg * scale;
        var left = start;
        if (pinned)
        {
            var bar = new Rect(new Vector2(full.Min.X, ImGui.GetWindowPos().Y),
                new Vector2(full.Max.X, top + tap + Metrics.Space.Sm * scale));
            UiInteract.HoverOverlay(bar);
            drawList.AddRectFilled(bar.Min, bar.Max, ImGui.GetColorU32(PrismArt.Back));
            if (RoundButton.Icon(drawList, new Vector2(left + radius * 0.5f, top + radius), radius,
                    PhoneIcons.ChevronLeft, controls, ButtonStyle.Plain, overlay: true) && !UiInteract.InputBlocked)
            {
                LeaveScreen();
            }

            Tooltip(new Vector2(left + radius * 0.5f, top + radius), radius, Loc.T(L.Prism.Back), true);
            left += tap;
        }

        var slot = Typography.Measure(LikeSlotTemplate, PrismType.Body).X;
        var saveOffset = radius * 1.5f + Metrics.Space.Xs * 2f * scale + slot;
        if (glam.Id.Length > 0)
        {
            DrawLikeButton(drawList, new Vector2(left + radius * 0.5f, top + radius), radius, slot, glam, pinned, scale);
            var save = new Vector2(left + saveOffset, top + radius);
            if (RoundButton.Icon(drawList, save, radius, glam.Saved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark,
                    controls, ButtonStyle.Plain, overlay: pinned) &&
                !(pinned && UiInteract.InputBlocked))
            {
                glams.ToggleSave(glam);
            }

            Tooltip(save, radius, Loc.T(glam.Saved ? L.Prism.Saved : L.Prism.Save), pinned);
        }

        pillLabels[0] = Loc.T(L.Prism.TryOnGlam);
        pillLabels[1] = Loc.T(L.Prism.AddToWardrobe);
        pillLabels[2] = Loc.T(L.Prism.InWardrobe);
        var cap = right - start - tap - saveOffset - radius;
        var check = Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xs * scale;
        var width = MathF.Min(ButtonWidth(pillLabels, cap, scale) + check, cap);
        DrawTryOnPill(drawList, new Rect(new Vector2(right - width, top), new Vector2(right, top + tap)), glam, key,
            pinned, scale);
    }

    private void DrawLikeButton(ImDrawListPtr drawList, Vector2 center, float radius, float slot, GlamDto glam,
        bool overlay, float scale)
    {
        var liked = glam.Liked;
        var mark = PrismMotion.Mark(drawList);
        if (RoundButton.Icon(drawList, center, radius, liked ? PhoneIcons.HeartFilled : PhoneIcons.Heart, controls,
                ButtonStyle.Plain, overlay: overlay) &&
            !(overlay && UiInteract.InputBlocked))
        {
            glams.ToggleLike(glam);
            likeBloomId = liked ? string.Empty : glam.Id;
            likeBloomAt = ImGui.GetTime();
            likeBloomOnPhoto = false;
        }

        Tooltip(center, radius, Loc.T(L.Prism.Like), overlay);
        var age = (float)(ImGui.GetTime() - likeBloomAt);
        if (glam.Id == likeBloomId && !likeBloomOnPhoto && age < LikeBloomSeconds)
        {
            PrismMotion.Grow(drawList, mark, center, 1f + LikeStamp * (1f - PrismMotion.At(age, 0f, Motion.PageSettle)));
            DrawLikeRing(drawList, center, radius * 0.5f, age, scale);
        }

        if (glam.Likes <= 0)
        {
            return;
        }

        var count = NumberText.Compact(glam.Likes);
        var size = Typography.Measure(count, PrismType.Body);
        Typography.Draw(drawList,
            new Vector2(center.X + radius * 0.5f + Metrics.Space.Xs * scale, center.Y - size.Y * 0.5f),
            Typography.FitText(count, slot, PrismType.Body), PrismArt.Fore, PrismType.Body);
    }

    private void DrawTryOnPill(ImDrawListPtr drawList, Rect rect, GlamDto glam, string key, bool overlay, float scale)
    {
        wardrobe.Ensure();
        var wearing = glamour.HasGlamourer && Wearing(key);
        var settling = wearing && tryOnKey == key && ImGui.GetTime() - tryOnAt < PrismArt.TryOnSeconds;
        var saved = wearing && !settling && !wardrobe.Loading ? wardrobe.FindByGlam(key) : null;
        var label = !wearing || settling ? pillLabels[0] : saved is null ? pillLabels[1] : pillLabels[2];
        if (!DrawPill(drawList, rect, rect, label, saved is null ? ButtonStyle.Solid : ButtonStyle.Tinted,
                saved is null ? string.Empty : PhoneIcons.Check, overlay, PillId, scale) || settling ||
            wearing && wardrobe.Loading || overlay && UiInteract.InputBlocked)
        {
            return;
        }

        if (saved is not null)
        {
            Push(PrismScreen.WardrobeOutfit);
            outfitId = saved.Id;
            renaming = false;
        }
        else if (wearing)
        {
            AddToWardrobe(glam, key);
        }
        else
        {
            TryOnGlam(glam, key);
        }
    }

    private bool DrawPill(ImDrawListPtr drawList, Rect hit, Rect rect, string label, ButtonStyle style, string glyph,
        bool overlay, string id, float scale)
    {
        var hovered = overlay ? UiInteract.HoverWindowOnly(hit.Min, hit.Max) : UiInteract.Hover(hit.Min, hit.Max);
        var face = Button.Surface(drawList, rect, controls, style, ButtonRole.Normal, true, hovered, ImGui.GetID(id));
        var glyphSize = glyph.Length > 0 ? Typography.LineHeight(PrismType.Caption) : 0f;
        var lead = glyphSize > 0f ? glyphSize + Metrics.Space.Xs * scale : 0f;
        var largest = Button.LabelStyle(rect.Height);
        var room = MathF.Max(1f, rect.Width - Metrics.Space.Lg * scale - lead);
        var labelStyle = new TextStyle(
            Typography.FitScale(label, room, largest.Scale, PrismType.Caption.Scale, largest.Weight), largest.Weight);
        var fitted = Typography.FitText(label, room, labelStyle);
        var size = Typography.Measure(fitted, labelStyle);
        var textLeft = face.Face.Center.X - (size.X + lead) * 0.5f;
        if (glyphSize > 0f)
        {
            PhoneIcon.Draw(drawList, new Vector2(textLeft + glyphSize * 0.5f, face.Face.Center.Y), glyph,
                face.LabelInk, glyphSize);
        }

        Typography.Draw(drawList, new Vector2(textLeft + lead, face.Face.Center.Y - size.Y * 0.5f), fitted,
            face.LabelInk, labelStyle);
        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    private void TryOnGlam(GlamDto glam, string key)
    {
        glamour.TryOn(glam.Gear ?? Array.Empty<GlamGearDto>());
        StartWearing(key, glam.Title);
        if (glam.Id.Length > 0)
        {
            wearingGlamId = glam.Id;
            glams.TriedOn(glam);
        }

        if (!glamour.HasGlamourer || !glamour.TryingOn)
        {
            return;
        }

        tryOnKey = key;
        tryOnAt = ImGui.GetTime();
        UiFeedback.Play(UiSound.Success);
    }

    private void AddToWardrobe(GlamDto glam, string key)
    {
        wardrobe.Add(new WardrobeOutfit(Guid.NewGuid().ToString("N"), glam.Title, string.Empty, Guid.Empty,
            glam.Gear ?? Array.Empty<GlamGearDto>(), false, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), key));
        ShellToast.Show(Loc.T(L.Prism.InWardrobe));
    }

    private void OpenGlamMenu(GlamDto glam, string key)
    {
        glamMenuCount = 0;
        if (postedCover.Length > 0 && glam.Id == postedId)
        {
            AddGlamMenuItem(GlamMenuAction.Share, Loc.T(L.Share.Action), PhoneIcons.Share, false);
        }

        if (!wardrobe.Loading && wardrobe.FindByGlam(key) is null && !(glamour.HasGlamourer && Wearing(key)))
        {
            AddGlamMenuItem(GlamMenuAction.Wardrobe, Loc.T(L.Prism.AddToWardrobe), PhoneIcons.Plus, false);
        }

        var mine = glam.Author?.Id == glams.MyId;
        AddGlamMenuItem(mine ? GlamMenuAction.Delete : GlamMenuAction.Report,
            Loc.T(mine ? L.Common.Delete : L.Prism.ReportGlam), mine ? PhoneIcons.Trash : PhoneIcons.Flag, true);
        glamMenu.Open();
    }

    private void AddGlamMenuItem(GlamMenuAction action, string label, string glyph, bool danger)
    {
        glamMenuActions[glamMenuCount] = action;
        glamMenuItems[glamMenuCount] = new ActionSheet.Item(label, glyph, danger);
        glamMenuCount++;
    }

    private void DrawGlamMenu(Rect full, GlamDto glam, string key)
    {
        if (!glamMenu.CapturesPointer)
        {
            return;
        }

        var picked = glamMenu.Draw(full, ActionSheetStyle.From(followedTheme!),
            glamMenuItems.AsSpan(0, glamMenuCount), Loc.T(L.Common.Cancel), false);
        if (picked >= 0)
        {
            RunGlamMenu(glamMenuActions[picked], glam, key);
        }
    }

    private void RunGlamMenu(GlamMenuAction action, GlamDto glam, string key)
    {
        switch (action)
        {
            case GlamMenuAction.Share:
                share.Offer(new ShareItem(ShareKind.Photo, postedCover, Id));
                break;
            case GlamMenuAction.Wardrobe:
                AddToWardrobe(glam, key);
                break;
            case GlamMenuAction.Delete:
                confirm.Ask(new ConfirmRequest
                {
                    Title = Loc.T(L.Prism.DeleteGlamTitle),
                    Message = Loc.T(L.Prism.DeleteGlamBody),
                    ConfirmLabel = Loc.T(L.Common.Delete),
                    CancelLabel = Loc.T(L.Common.Cancel),
                    Danger = true,
                    Confirm = () =>
                    {
                        glams.Delete(glam);
                        GoBack();
                    },
                });
                break;
            default:
                report.Open(new ReportPrompt
                {
                    Title = Loc.T(L.Prism.ReportGlam),
                    Submit = (reason, done) => glams.Report(glam.Id, reason, done),
                });
                break;
        }
    }

    private float DrawGlamHeading(ImDrawListPtr drawList, float left, float right, float top, GlamDto glam,
        float scale)
    {
        var width = right - left;
        if (!glamour.HasGlamourer)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.TryOnWithoutGlamourer),
                PrismArt.Muted, PrismType.Caption, width) + Metrics.Space.Md * scale;
        }

        top += PrismType.DrawHero(drawList, new Vector2(left, top), glam.Title, width, PrismArt.Fore) +
               Metrics.Space.Xs * scale;
        top = DrawByline(drawList, left, right, top, glam.Author, scale);
        top += Typography.DrawWrappedLeft(new Vector2(left, top), MetaLine(glam), PrismArt.Muted, PrismType.Caption,
            width);
        if (glam.InspiredBy.Length > 0)
        {
            var tap = Metrics.Size.TapTarget * scale;
            var label = Loc.T(L.Prism.InspiredBy);
            var size = Typography.Measure(label, PrismType.Caption);
            var row = new Rect(new Vector2(left, top), new Vector2(left + MathF.Min(size.X, width), top + tap));
            var hovered = UiInteract.Hover(row.Min, row.Max);
            Typography.Draw(drawList, new Vector2(left, row.Center.Y - size.Y * 0.5f),
                Typography.FitText(label, width, PrismType.Caption), hovered ? PrismArt.Muted : PrismArt.Fore,
                PrismType.Caption);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenGlam(new GlamDto(glam.InspiredBy));
            }

            top = row.Max.Y;
        }

        return DrawTags(left, top, width, glam.Tags, scale) + Metrics.Space.Xl * scale;
    }

    private float DrawByline(ImDrawListPtr drawList, float left, float right, float top, GlamAuthorDto? author,
        float scale)
    {
        if (author is null)
        {
            return top;
        }

        var tap = Metrics.Size.TapTarget * scale;
        var middle = top + tap * 0.5f;
        var radius = PostAvatarUnits * scale * 0.5f;
        DrawAuthorAvatar(drawList, new Vector2(left + radius, middle), radius, AuthorName(author), author.AvatarUrl,
            TextStyles.Caption2.Scale);
        var handleLeft = left + radius * 2f + Metrics.Space.Sm * scale;
        var other = author.Id.Length > 0 && author.Id != glams.MyId;
        var labelStyle = Button.LabelStyle(Button.SmallHeight * scale);
        var followWidth = other
            ? MathF.Max(Typography.Measure(Loc.T(L.Prism.Follow), labelStyle).X,
                Typography.Measure(Loc.T(L.Prism.Following), labelStyle).X) + Metrics.Space.Lg * 2f * scale
            : 0f;
        var handle = Typography.FitText(AuthorLine(author),
            MathF.Max(1f, right - handleLeft - followWidth - Metrics.Space.Sm * scale), PrismType.Headline);
        var handleWidth = Typography.Measure(handle, PrismType.Headline).X;
        Typography.Draw(drawList,
            new Vector2(handleLeft, middle - Typography.LineHeight(PrismType.Headline) * 0.5f), handle, PrismArt.Fore,
            PrismType.Headline);
        if (author.Id.Length > 0 && UiInteract.HoverClick(new Vector2(left, top),
                new Vector2(handleLeft + handleWidth, top + tap)))
        {
            OpenProfile(author);
        }

        if (!other)
        {
            return top + tap;
        }

        var following = Following(author);
        var half = Button.SmallHeight * scale * 0.5f;
        var hit = new Rect(new Vector2(right - followWidth, top), new Vector2(right, top + tap));
        var face = new Rect(new Vector2(hit.Min.X, middle - half), new Vector2(right, middle + half));
        if (DrawPill(drawList, hit, face, Loc.T(following ? L.Prism.Following : L.Prism.Follow),
                following ? ButtonStyle.Tinted : ButtonStyle.Solid, string.Empty, false, FollowId, scale))
        {
            followed[author.Id] = !following;
            glams.SetFollow(author, !following);
        }

        return top + tap;
    }

    private bool Following(GlamAuthorDto author)
    {
        if (followed.TryGetValue(author.Id, out var known))
        {
            return known;
        }

        return glams.Profile is { } profile && profile.User?.Id == author.Id && profile.Following;
    }

    private string MetaLine(GlamDto glam)
    {
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var key = HashCode.Combine(glam.Vanilla, glam.Race, glam.Gender, glam.TryOns, glam.CreatedAtUnix, minute);
        if (metaText.IsCurrent(key))
        {
            return metaText.Value;
        }

        var line = Loc.T(glam.Vanilla ? L.Prism.VanillaStamp : L.Prism.ModdedStamp);
        var body = BodyLine(glam.Race, glam.Gender);
        if (body.Length > 0)
        {
            line = string.Concat(line, GlamMetaSeparator, body);
        }

        if (glam.TryOns > 0)
        {
            line = string.Concat(line, GlamMetaSeparator, TriedOnLine(glam.TryOns));
        }

        if (glam.CreatedAtUnix > 0)
        {
            line = string.Concat(line, GlamMetaSeparator, TimeText.Ago(glam.CreatedAtUnix));
        }

        return metaText.Store(key, line);
    }

    private float DrawTags(float left, float top, float width, string[]? tags, float scale)
    {
        if (tags is not { Length: > 0 })
        {
            return top;
        }

        if (!ReferenceEquals(tags, tagRailFor))
        {
            tagRailFor = tags;
            tagRailLabels = new string[tags.Length];
            tagRailActive = new bool[tags.Length];
            for (var index = 0; index < tags.Length; index++)
            {
                tagRailLabels[index] = TagLabel(tags[index]);
            }

            tagRail.Reset();
        }

        var row = new Rect(new Vector2(left, top + Metrics.Space.Sm * scale),
            new Vector2(left + width, top + Metrics.Space.Sm * scale + ChipRail.RowHeight * scale));
        var tapped = tagRail.Draw(row, ui, tagRailLabels, tagRailActive);
        if (tapped >= 0)
        {
            OpenTagGlams(tags[tapped]);
        }

        return row.Max.Y;
    }

    private static string AuthorName(GlamAuthorDto? author) =>
        author is null ? string.Empty : author.DisplayName.Length > 0 ? author.DisplayName : author.Handle;

    private void DrawAuthorAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string name, string? avatarUrl,
        float monogramScale) =>
        AvatarView.Draw(drawList, center, radius, PrismArt.Muted, Initials.Of(name), monogramScale,
            images.Avatar(avatarUrl, radius * 2f), AvatarSegments);

    private string TriedOnLine(long tryOns)
    {
        var key = HashCode.Combine(tryOns, Loc.Current.Code);
        return triedText.IsCurrent(key)
            ? triedText.Value
            : triedText.Store(key, Loc.Plural(L.Prism.TriedOnCount, (int)Math.Min(tryOns, int.MaxValue)));
    }

    private string TagLabel(string tag)
    {
        if (!tagLabels.TryGetValue(tag, out var label))
        {
            label = "#" + tag;
            tagLabels[tag] = label;
        }

        return label;
    }

    private float DrawMoreFrom(ImDrawListPtr drawList, Rect full, float top, GlamDto glam, float scale)
    {
        if (glam.Id.Length == 0)
        {
            return top;
        }

        glams.OpenMoreFrom(glam);
        var more = glams.MoreFrom(glam.Id);
        if (more.Length == 0 || glam.Author is not { } author)
        {
            return top;
        }

        var name = author.DisplayName.Length > 0 ? author.DisplayName : AuthorLine(author);
        var key = HashCode.Combine(name, Loc.Current.Code);
        var title = moreText.IsCurrent(key) ? moreText.Value : moreText.Store(key, Loc.T(L.Prism.MoreFrom, name));
        var gap = Metrics.Space.Xxs * scale;
        top = DrawSectionTitle(drawList, full.Min.X + Metrics.Space.Lg * scale, full.Max.X - Metrics.Space.Lg * scale,
            top, title, string.Empty, scale) + Metrics.Space.Sm * scale;
        var column = (full.Width - gap * 3f) * 0.5f;
        var pitch = column / GlamStore.PhotoRatio + GridLine(scale) + Metrics.Space.Sm * scale;
        for (var index = 0; index < more.Length; index++)
        {
            var item = more[index];
            var min = new Vector2(full.Min.X + gap + index % 2 * (column + gap), top + index / 2 * pitch);
            var photo = new Rect(min, min + new Vector2(column, column / GlamStore.PhotoRatio));
            if (DrawGridTile(drawList, photo, item.Photos is { Length: > 0 } photos ? photos[0].Url : string.Empty,
                    item.Title, string.Empty, null, PhoneIcons.Wand,
                    item.TryOns > 0 ? NumberText.Compact(item.TryOns) : string.Empty, scale))
            {
                OpenGlam(item);
            }
        }

        return top + (more.Length + 1) / 2 * pitch;
    }

    private string BodyLine(int race, int gender)
    {
        if (race <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(race, gender, Loc.Current.Code);
        if (bodyText.IsCurrent(key))
        {
            return bodyText.Value;
        }

        var name = gameData.RaceName((uint)race, gender == 1);
        return bodyText.Store(key,
            name.Length == 0 ? string.Empty : Loc.T(gender == 1 ? L.Prism.BodyFemale : L.Prism.BodyMale, name));
    }

    private float DrawPieceSection(ImDrawListPtr drawList, float left, float right, float top, GlamGearDto[] gear,
        float scale)
    {
        if (pieceOrder.Length == 0)
        {
            return top;
        }

        var owned = ownedCountText.IsCurrent(ownedPieces)
            ? ownedCountText.Value
            : ownedCountText.Store(ownedPieces, Loc.T(L.Prism.OwnedCount, ownedPieces));
        top = DrawSectionTitle(drawList, left, right, top, PieceCountLine(), owned, scale);
        if (ownedPieces < totalPieces && !statsStorage)
        {
            top += Metrics.Space.Xxs * scale + Typography.DrawWrappedLeft(new Vector2(left, top + Metrics.Space.Xxs * scale),
                Loc.T(L.Prism.OpenStorage), PrismArt.Muted, PrismType.Caption, right - left);
        }

        top += Metrics.Space.Xs * scale;
        for (var index = 0; index < pieceOrder.Length; index++)
        {
            top = DrawPieceRow(drawList, left, right, top, gear[pieceOrder[index]], index, scale);
        }

        return top + Metrics.Space.Xl * scale;
    }

    private float DrawPieceRow(ImDrawListPtr drawList, float left, float right, float top, GlamGearDto piece, int index,
        float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + GlamRowUnits * scale));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var tap = Metrics.Size.TapTarget * scale;
        var wand = new Vector2(right - tap * 0.25f, row.Center.Y);
        var hit = new Rect(row.Min, new Vector2(wand.X - tap * 0.5f, row.Max.Y));
        var hovered = HighlightRow(drawList, hit, scale);
        var icon = PieceTileUnits * scale;
        var iconMin = new Vector2(left, row.Center.Y - icon * 0.5f);
        var radius = Metrics.Radius.Sm * scale;
        var name = string.Empty;
        if (GearItem(piece.ItemId, out var found, out var iconId))
        {
            name = found;
            GameIconTile.Draw(drawList, textures, iconId, iconMin, iconMin + new Vector2(icon, icon), radius, scale);
        }

        DrawOwnedMark(drawList, iconMin + new Vector2(icon, icon) - new Vector2(radius, radius) * 0.5f,
            OwnedState(piece.ItemId), PrismArt.Fore, PrismArt.Back, scale);
        ImGui.PushID(index);
        if (RoundButton.Icon(drawList, wand, tap * 0.5f, PhoneIcons.Wand, controls, ButtonStyle.Plain,
                glyphInk: PrismArt.Muted))
        {
            TryOnPiece(piece);
        }

        ImGui.PopID();
        Tooltip(wand, tap * 0.5f, Loc.T(L.Prism.TryOnPiece), false);
        var textLeft = iconMin.X + icon + Metrics.Space.Md * scale;
        DrawRowText(drawList, textLeft, hit.Max.X - Metrics.Space.Sm * scale, row.Center.Y, name, piece, null, scale);
        if (index < pieceOrder.Length - 1)
        {
            PrismArt.Hairline(drawList, textLeft, right, row.Max.Y, PrismArt.Faint, scale);
        }

        if (UiInteract.Click(hit.Min, hit.Max, hovered))
        {
            OpenItemGlams(piece.ItemId);
        }

        return row.Max.Y;
    }

    private bool HighlightRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        if (!UiInteract.Hover(row.Min, row.Max))
        {
            return false;
        }

        var bleed = new Vector2(Metrics.Space.Xs * scale, 0f);
        drawList.AddRectFilled(row.Min - bleed, row.Max + bleed,
            ImGui.GetColorU32(Surfaces.Fill(controls, FillLevel.Quaternary)), Metrics.Radius.Md * scale);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    private static void Tooltip(Vector2 center, float radius, string label, bool overlay)
    {
        var rect = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var hovered = overlay
            ? !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(rect.Min, rect.Max)
            : UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            HoverTooltip.Enqueue(rect, label, 1f, HoverLabelSide.Below);
        }
    }

    private void DrawRowText(ImDrawListPtr drawList, float left, float right, float middle, string name,
        GlamGearDto? piece, string? second, float scale)
    {
        var room = MathF.Max(1f, right - left);
        var dyed = piece is not null && (piece.Stain0 != 0 || piece.Stain1 != 0);
        var below = dyed || second is { Length: > 0 };
        var headline = Typography.LineHeight(PrismType.Headline);
        var caption = Typography.LineHeight(PrismType.Caption);
        var top = middle - (headline + (below ? caption : 0f)) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(name, room, PrismType.Headline),
            PrismArt.Fore, PrismType.Headline);
        top += headline;
        if (!below)
        {
            return;
        }

        var textLeft = left;
        if (dyed)
        {
            var swatch = SwatchUnits * scale;
            for (var stainIndex = 0; stainIndex < 2; stainIndex++)
            {
                var stain = stainIndex == 0 ? piece!.Stain0 : piece!.Stain1;
                if (stain != 0)
                {
                    DrawSwatch(drawList, new Vector2(textLeft + swatch, top + caption * 0.5f), gameData.StainColor(stain),
                        PrismArt.Faint, scale);
                    textLeft += swatch * 2f + Metrics.Space.Xxs * scale;
                }
            }

            textLeft += Metrics.Space.Xxs * scale;
            second = DyeLine(piece!.Stain0, piece.Stain1);
        }

        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(second!, MathF.Max(1f, right - textLeft), PrismType.Caption), PrismArt.Muted,
            PrismType.Caption);
    }

    private float DrawModSection(ImDrawListPtr drawList, float left, float right, float top, GlamGearDto[] gear,
        float scale)
    {
        if (totalMods == 0)
        {
            return top;
        }

        var installed = totalMods - missingMods;
        var caption = !glamour.HasPenumbra ? Loc.T(L.Prism.ModsNeedPenumbra)
            : installedText.IsCurrent(installed) ? installedText.Value
            : installedText.Store(installed, Loc.T(L.Prism.InstalledCount, installed));
        top = DrawSectionTitle(drawList, left, right, top, ModCountLine(), caption, scale) + Metrics.Space.Xs * scale;
        var drawn = 0;
        for (var orderIndex = 0; orderIndex <= pieceOrder.Length; orderIndex++)
        {
            for (var index = 0; index < gear.Length; index++)
            {
                var character = gear[index].ItemId == 0;
                var wanted = orderIndex < pieceOrder.Length ? index == pieceOrder[orderIndex] : character;
                if (!wanted || gear[index].Mods is not { } mods)
                {
                    continue;
                }

                for (var modIndex = 0; modIndex < mods.Length; modIndex++)
                {
                    top = DrawModRow(drawList, left, right, top, mods[modIndex],
                        character ? GearSlots.None : gear[index].Slot, ++drawn < totalMods, scale);
                }
            }
        }

        return top + Metrics.Space.Xl * scale;
    }

    private float DrawModRow(ImDrawListPtr drawList, float left, float right, float top, GlamModDto mod, int slot,
        bool separated, float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(right, top + GlamRowUnits * scale));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var hovered = HighlightRow(drawList, row, scale);
        var icon = PieceTileUnits * scale;
        var tile = new Rect(new Vector2(left, row.Center.Y - icon * 0.5f),
            new Vector2(left + icon, row.Center.Y + icon * 0.5f));
        PrismArt.SlotTile(drawList, tile, slot, 0, scale);
        var (_, allowed, installed) = ModLine(mod);
        var side = installed ? Loc.T(L.Prism.Installed) : allowed ? Loc.T(L.Prism.GetMod) : string.Empty;
        var sideRight = right;
        if (side.Length > 0)
        {
            var size = Typography.Measure(side, PrismType.Caption);
            var height = Button.SmallHeight * scale;
            var min = new Vector2(right - size.X - (installed ? 0f : Metrics.Space.Md * 2f * scale),
                row.Center.Y - height * 0.5f);
            if (!installed)
            {
                drawList.AddRectFilled(min, new Vector2(right, min.Y + height),
                    ImGui.GetColorU32(Surfaces.Fill(controls, FillLevel.Secondary)), height * 0.5f);
            }

            Typography.Draw(drawList, new Vector2(right - size.X - (installed ? 0f : Metrics.Space.Md * scale),
                row.Center.Y - size.Y * 0.5f), side, installed ? PrismArt.Muted : PrismArt.Fore, PrismType.Caption);
            sideRight = min.X - Metrics.Space.Sm * scale;
        }

        var textLeft = tile.Max.X + Metrics.Space.Md * scale;
        DrawRowText(drawList, textLeft, sideRight, row.Center.Y, mod.Name, null, ModByline(mod, slot), scale);
        if (separated)
        {
            PrismArt.Hairline(drawList, textLeft, right, row.Max.Y, PrismArt.Faint, scale);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenMod(mod, allowed && !installed);
        }

        return row.Max.Y;
    }

    private string ModByline(GlamModDto mod, int slot)
    {
        if (modBylines.TryGetValue(mod, out var line))
        {
            return line;
        }

        var place = Loc.T(slot switch
        {
            (int)GearSlot.MainHand => L.Inventory.SlotMainHand,
            (int)GearSlot.OffHand => L.Inventory.SlotOffHand,
            (int)GearSlot.Head => L.Inventory.SlotHead,
            (int)GearSlot.Body => L.Inventory.SlotBody,
            (int)GearSlot.Hands => L.Inventory.SlotHands,
            (int)GearSlot.Legs => L.Inventory.SlotLegs,
            (int)GearSlot.Feet => L.Inventory.SlotFeet,
            (int)GearSlot.Ears => L.Inventory.SlotEars,
            (int)GearSlot.Neck => L.Inventory.SlotNeck,
            (int)GearSlot.Wrists => L.Inventory.SlotWrists,
            (int)GearSlot.RingRight or (int)GearSlot.RingLeft => L.Inventory.SlotRings,
            _ => L.Prism.CharacterHeading,
        });
        line = mod.Author.Length > 0 ? string.Concat(Loc.T(L.Prism.ModAuthor, mod.Author), GlamMetaSeparator, place) : place;
        modBylines[mod] = line;
        return line;
    }

    private float DrawGearList(ImDrawListPtr drawList, float left, float top, float width, GlamGearDto[] gear,
        bool interactive, float scale)
    {
        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].ItemId != 0)
            {
                top = DrawGearRow(drawList, left, top, width, gear[index], interactive, scale);
            }
        }

        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].ItemId == 0 && gear[index].Mods is { Length: > 0 } mods)
            {
                top = DrawSectionLabel(drawList, left, top + Metrics.Space.Lg * scale, width, 1,
                    Loc.T(L.Prism.CharacterHeading), scale);
                top = DrawModChips(drawList, left, top, width, mods, interactive, scale);
            }
        }

        return top;
    }

    private float DrawGearRow(ImDrawListPtr drawList, float left, float top, float width, GlamGearDto piece,
        bool interactive, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = GearIconUnits * scale;
        if (GearItem(piece.ItemId, out var name, out var iconId))
        {
            GameIconTile.Draw(drawList, textures, iconId, new Vector2(left, top), new Vector2(left + icon, top + icon), 0f,
                scale);
        }

        var dyes = DyeLine(piece.Stain0, piece.Stain1);
        var owned = interactive && screen == PrismScreen.GlamDetail && ownedIds.Contains(piece.ItemId);
        var lines = dyes.Length > 0 || owned ? 2 : 1;
        var textTop = top + MathF.Max(0f, (icon - labelLine * lines) * 0.5f);
        var textLeft = left + icon + Metrics.Space.Md * scale;
        var tryLabel = Loc.T(L.Prism.TryOnGlam);
        var tryWidth = interactive ? PrismType.Measure(tryLabel, PrismFace.Label, labelPixels).X : 0f;
        var room = width - (textLeft - left) - (interactive ? tryWidth + Metrics.Space.Md * scale : 0f);
        var fitted = PrismType.FitLabel(name, labelPixels, room);
        if (!interactive)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, textTop), fitted, PrismArt.Fore, PrismFace.Label,
                labelPixels);
        }
        else if (PrismArt.Link(drawList, new Vector2(textLeft, textTop), fitted, PrismArt.Fore, scale))
        {
            OpenItemGlams(piece.ItemId);
        }

        if (interactive &&
            PrismArt.Link(drawList, new Vector2(left + width - tryWidth, textTop), tryLabel, PrismArt.Muted, scale))
        {
            glamour.TryOnPiece(piece);
            StartWearing(string.Empty, name);
        }

        if (owned)
        {
            PrismType.DrawRight(drawList, new Vector2(left + width, textTop + labelLine), Loc.T(L.Prism.Owned),
                PrismArt.Muted, PrismFace.Label, labelPixels);
        }

        if (dyes.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, textTop + labelLine),
                PrismType.FitLabel(dyes, labelPixels, room), PrismArt.Muted, PrismFace.Label, labelPixels);
        }

        var bottom = top + MathF.Max(icon, labelLine * 2f) + Metrics.Space.Sm * scale;
        if (piece.Mods is { Length: > 0 } mods)
        {
            bottom = DrawModChips(drawList, textLeft, bottom, width - (textLeft - left), mods, interactive, scale);
        }

        PrismArt.Hairline(drawList, left, left + width, bottom, PrismArt.Faint, scale);
        return bottom + Metrics.Space.Md * scale;
    }

    private string DyeLine(byte stain0, byte stain1)
    {
        var key = stain0 | stain1 << 8;
        if (!dyeLines.TryGetValue(key, out var line))
        {
            var first = gameData.StainName(stain0);
            var second = gameData.StainName(stain1);
            line = first.Length == 0 ? second : second.Length == 0 ? first : string.Concat(first, ", ", second);
            dyeLines[key] = line;
        }

        return line;
    }

    private float DrawModChips(ImDrawListPtr drawList, float left, float top, float width, GlamModDto[] mods,
        bool interactive, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var pitch = PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale;
        var getKey = Loc.Current.Code.GetHashCode(StringComparison.Ordinal);
        var get = getText.IsCurrent(getKey) ? getText.Value : getText.Store(getKey, Loc.T(L.Prism.GetMod) + " " + Arrow);
        var getWidth = PrismType.Measure(get, PrismFace.Label, labelPixels).X;
        for (var index = 0; index < mods.Length; index++)
        {
            var mod = mods[index];
            var (line, allowed, installed) = ModLine(mod);
            installed &= interactive;
            allowed &= interactive && !installed;
            var have = installed ? Loc.T(L.Prism.Installed) : string.Empty;
            var haveWidth = installed ? PrismType.Measure(have, PrismFace.Label, labelPixels).X : 0f;
            var side = allowed ? getWidth : haveWidth;
            var room = width - (side > 0f ? side + Metrics.Space.Md * scale : 0f);
            var fitted = PrismType.FitLabel(line, labelPixels, room);
            if (!interactive)
            {
                PrismType.Draw(drawList, new Vector2(left, top), fitted, PrismArt.Fore, PrismFace.Label, labelPixels);
            }
            else if (PrismArt.Link(drawList, new Vector2(left, top), fitted, PrismArt.Fore, scale, pitch))
            {
                OpenModGlams(mod);
            }

            if (installed)
            {
                PrismType.DrawRight(drawList, new Vector2(left + width, top), have, PrismArt.Muted, PrismFace.Label,
                    labelPixels);
            }

            if (allowed && PrismArt.Link(drawList, new Vector2(left + width - getWidth, top), get, PrismArt.Muted, scale,
                    pitch))
            {
                UrlActions.AskThenOpen(mod.Link);
            }

            top += pitch;
        }

        return top + Metrics.Space.Sm * scale;
    }

    private (string Line, bool Allowed, bool Installed) ModLine(GlamModDto mod)
    {
        if (!ReferenceEquals(linesLanguage, Loc.Current.Code))
        {
            linesLanguage = Loc.Current.Code;
            modLines.Clear();
            modBylines.Clear();
            dyeLines.Clear();
        }

        if (!modLines.TryGetValue(mod, out var cached))
        {
            cached = (mod.Author.Length > 0 ? Loc.T(L.Prism.ModBy, mod.Name, mod.Author) : mod.Name,
                ModLinks.IsAllowed(mod.Link), glamour.HasMod(mod));
            modLines[mod] = cached;
        }

        return cached;
    }
}

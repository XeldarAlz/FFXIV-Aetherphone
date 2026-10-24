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
    private const string GlamMetaSeparator = " · ";
    private const string LikeSlotTemplate = "9.9K";
    private const float LikeRingReach = 1.6f;
    private const float LikeRingUnits = 1.5f;
    private const float ButtonRingReach = 0.45f;
    private const float ButtonRingPeak = 0.7f;
    private const float ButtonRingUnits = 1.4f;
    private const float TryOnShardShare = 0.42f;
    private const float TryOnShardAngle = -1.785f;
    private const float TryOnShardSpread = 0.53f;
    private const float TryOnShardSpeed = 420f;
    private const int AvatarSegments = 32;
    private const int GlamMenuSize = 3;
    private const float LikeBloomSeconds = 0.9f;
    private const float LikeHoldSeconds = 0.45f;
    private const float LikeStamp = 0.35f;
    private const float PostAvatarUnits = 24f;
    private const float BodyFactShare = 0.3f;
    private const float GlamRowUnits = 56f;
    private const float PagerSlopUnits = 12f;
    private const float PagerSlopBias = 1.2f;
    private const float DotsShare = 0.3f;
    private const float PillHandoverSeconds = 0.18f;

    private readonly DoubleTapLike photoLike = new();
    private readonly DeferredTap photoTap = new();
    private readonly Pager photoPager = new(Motion.PageSettle);
    private readonly ActionSheet glamMenu = new();
    private readonly ActionSheet.Item[] glamMenuItems = new ActionSheet.Item[GlamMenuSize];
    private readonly GlamMenuAction[] glamMenuActions = new GlamMenuAction[GlamMenuSize];
    private readonly string[] pillLabels = new string[3];
    private readonly Dictionary<string, string> handleLines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tagLabels = new(StringComparer.Ordinal);
    private readonly ChipRail tagRail = new();
    private readonly Dictionary<uint, (string Name, uint Icon)> itemRows = new();
    private readonly HashSet<uint> ownedIds = new();
    private readonly Dictionary<int, string> dyeLines = new();
    private readonly Dictionary<GlamModDto, (string Side, bool Allowed, bool Installed)> modLines = new();
    private readonly Dictionary<(GlamModDto Mod, int Slot), string> modBylines = new();
    private string? statsFor;
    private bool statsStale;
    private GlamGearDto[]? statsGear;
    private bool statsStorage;
    private CachedText triedText;
    private CachedText raceText;
    private CachedText moreText;
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
    private double saveLightAt;
    private string saveLightId = string.Empty;
    private Spring pinReveal;
    private double pillSwapAt;
    private string pillLabel = string.Empty;
    private string pillLeaving = string.Empty;
    private bool pillSaved;
    private bool pillLeavingSaved;
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
        pinReveal.SnapTo(0f);
        pillLabel = string.Empty;
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

    private void DrawGlamDetail(ImDrawListPtr drawList, Rect full, Rect content, float scale)
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
        var key = WearKey(glam);
        RefreshPieces(key, glam.Gear ?? Array.Empty<GlamGearDto>());
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
            new Vector2(content.Min.X + ButtonWidth([retry], content.Width, scale),
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
                PrismArt.Muted, PrismType.Caption, right - left) + Metrics.Space.Xl * scale;
        }

        cursor = DrawMoreFrom(drawList, full, cursor, glam, scale);
        DrawActionRow(drawList, full, rowTop, content.Min.Y, glam, key, scale);
        ImGui.Dummy(new Vector2(full.Width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private void RefreshPieces(string key, GlamGearDto[] gear)
    {
        var storage = OwnedItems.StorageLoaded;
        var sameGlam = statsFor == key && ReferenceEquals(statsGear, gear);
        if (sameGlam && statsStorage == storage && !statsStale)
        {
            return;
        }

        if (!sameGlam)
        {
            cardTile = -1;
            cardPiece = -1;
        }

        statsFor = key;
        statsGear = gear;
        statsStorage = storage;
        statsStale = false;
        modLines.Clear();
        modBylines.Clear();
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
        DrawPublished(drawList, photo, glam, Cover(glam), scale);
        var dots = new Vector2(photo.Center.X, photo.Max.Y - (Metrics.Space.Md + PrismArt.ChipUnits * 0.5f) * scale);
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
        DrawPhotoBack(drawList, photo, topInset, scale);

        if (glam.Id.Length == 0)
        {
            return;
        }

        if (DrawPhotoButton(drawList, photo, topInset, 1, PhoneIcons.Dots, Loc.T(L.Prism.More), scale))
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
            photoPager.AnimateTo(PhotoCarousel.DotAt(center.X, count, width * DotsShare, ImGui.GetMousePos().X), count);
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
            PrismArt.Refraction(drawList, frame, PrismMotion.At((float)(ImGui.GetTime() - seen), 0.1f, Motion.Sheet));
        }

        var pad = Metrics.Space.Md * scale;
        PrismArt.PhotoPill(drawList, frame.Max - new Vector2(pad, pad), frame.Width - pad * 2f, PhoneIcons.CircleCheck,
            Loc.T(L.Prism.Published), true, scale);
    }

    private void DrawPhotoLike(ImDrawListPtr drawList, Rect frame, GlamDto glam, float scale)
    {
        var age = (float)(ImGui.GetTime() - likeBloomAt);
        if (!likeBloomOnPhoto || glam.Id != likeBloomId || age >= LikeBloomSeconds)
        {
            return;
        }

        DrawLikeBloom(drawList, frame, age, scale);
    }

    private static void DrawLikeBloom(ImDrawListPtr drawList, Rect frame, float age, float scale)
    {
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

    private static void DrawLikeRing(ImDrawListPtr drawList, Vector2 center, float baseRadius, float age,
        float scale) =>
        DrawLightRing(drawList, center, baseRadius, age, LikeRingReach, 1f, LikeRingUnits * scale);

    private static void DrawLightRing(ImDrawListPtr drawList, Vector2 center, float baseRadius, float age, float reach,
        float peak, float thickness)
    {
        var grow = PrismMotion.At(age, 0f, Motion.Sheet);
        if (age <= 0f || grow >= PrismMotion.Settled)
        {
            return;
        }

        var mark = PrismMotion.Mark(drawList);
        PrismArt.SpectrumRing(drawList, center, baseRadius * (1f + reach * grow), grow * MathF.PI * 0.5f,
            MathF.Max(1f, thickness));
        PrismMotion.Shift(drawList, mark, Vector2.Zero, peak * (1f - grow));
    }

    private void DrawActionRow(ImDrawListPtr drawList, Rect full, float naturalTop, float pinTop, GlamDto glam,
        string key, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var radius = tap * 0.5f;
        var pinned = naturalTop < pinTop;
        var reveal = Math.Clamp(pinReveal.Step(pinned ? 1f : 0f, Motion.Appear,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds)), 0f, 1f);
        var top = pinned ? pinTop : naturalTop;
        var slide = (pinned ? reveal - 1f : reveal) * tap;
        var start = full.Min.X + Metrics.Space.Lg * scale;
        var right = full.Max.X - Metrics.Space.Lg * scale;
        var left = start;
        if (pinned)
        {
            var bar = new Rect(new Vector2(full.Min.X, ImGui.GetWindowPos().Y),
                new Vector2(full.Max.X, top + tap + Metrics.Space.Sm * scale));
            UiInteract.HoverOverlay(bar);
            drawList.AddRectFilled(bar.Min, bar.Max, ImGui.GetColorU32(PrismArt.Back));
            var backMark = PrismMotion.Mark(drawList);
            if (RoundButton.Icon(drawList, new Vector2(left + radius * 0.5f, top + radius), radius,
                    PhoneIcons.ChevronLeft, controls, ButtonStyle.Bare, Loc.T(L.Prism.Back), HoverLabelSide.Below,
                    overlay: true) && !UiInteract.InputBlocked)
            {
                LeaveScreen();
            }

            if (reveal < PrismMotion.Settled)
            {
                PrismMotion.Shift(drawList, backMark, new Vector2(slide, 0f), reveal);
            }

            left += tap;
        }

        var slot = Typography.Measure(LikeSlotTemplate, PrismType.Body).X;
        var saveOffset = radius * 1.5f + Metrics.Space.Xs * 2f * scale + slot;
        var iconsMark = PrismMotion.Mark(drawList);
        if (glam.Id.Length > 0)
        {
            DrawLikeButton(drawList, new Vector2(left + radius * 0.5f, top + radius), radius, slot, glam, pinned, scale);
            var save = new Vector2(left + saveOffset, top + radius);
            var own = glam.Author?.Id == glams.MyId;
            if (!own && RoundButton.Icon(drawList, save, radius,
                    glam.Saved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark, controls, ButtonStyle.Bare,
                    Loc.T(glam.Saved ? L.Prism.Saved : L.Prism.Save), HoverLabelSide.Below, overlay: pinned) &&
                !(pinned && UiInteract.InputBlocked))
            {
                saveLightId = glam.Saved ? string.Empty : glam.Id;
                saveLightAt = ImGui.GetTime();
                glams.ToggleSave(glam);
            }

            if (!own && glam.Id == saveLightId)
            {
                DrawLightRing(drawList, save, radius * 0.5f, (float)(ImGui.GetTime() - saveLightAt), ButtonRingReach,
                    ButtonRingPeak, ButtonRingUnits * scale);
            }
        }

        if (MathF.Abs(slide) > PrismMotion.GrowEpsilon)
        {
            PrismMotion.Shift(drawList, iconsMark, new Vector2(slide, 0f), 1f);
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
                ButtonStyle.Bare, Loc.T(L.Prism.Like), HoverLabelSide.Below, overlay: overlay) &&
            !(overlay && UiInteract.InputBlocked))
        {
            glams.ToggleLike(glam);
            likeBloomId = liked ? string.Empty : glam.Id;
            likeBloomAt = ImGui.GetTime();
            likeBloomOnPhoto = false;
        }

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
        var locked = settling || wearing && wardrobe.Loading || overlay && UiInteract.InputBlocked;
        if (saved is null)
        {
            PrismLight.Primary(drawList, rect, !locked, scale);
        }

        if (!DrawPill(drawList, rect, label, saved is not null, overlay, locked, scale))
        {
            return;
        }

        if (saved is not null)
        {
            OpenOutfit(saved.Id);
        }
        else if (wearing)
        {
            AddToWardrobe(glam, key);
        }
        else
        {
            if (TryOnGlam(glam, key))
            {
                shards.Burst(new Vector2(rect.Min.X + rect.Width * TryOnShardShare, rect.Min.Y), TryOnShardAngle,
                    TryOnShardSpread, TryOnShardSpeed, ShardLifeSeconds);
            }
        }
    }

    private bool DrawPill(ImDrawListPtr drawList, Rect rect, string label, bool saved, bool overlay, bool locked,
        float scale)
    {
        if (label != pillLabel)
        {
            pillLeaving = pillLabel;
            pillLeavingSaved = pillSaved;
            pillLabel = label;
            pillSaved = saved;
            pillSwapAt = ImGui.GetTime();
        }

        var age = (float)(ImGui.GetTime() - pillSwapAt);
        var arrive = pillLeaving.Length > 0 ? PrismMotion.At(age, PillHandoverSeconds, Motion.Appear) : 1f;
        var crossing = arrive < PrismMotion.Settled;
        var restyled = crossing && pillLeavingSaved != saved;
        var hovered = !locked &&
                      (overlay ? UiInteract.HoverWindowOnly(rect.Min, rect.Max) : UiInteract.Hover(rect.Min, rect.Max));
        var key = ImGui.GetID("prism.tryOn");
        var leaving = restyled ? PillFace(drawList, rect, pillLeavingSaved, hovered, key, 1f - arrive) : default;
        var face = PillFace(drawList, rect, saved, hovered, key, restyled ? arrive : 1f);
        if (crossing)
        {
            DrawPillLabel(drawList, restyled ? leaving : face, pillLeaving, pillLeavingSaved, rect,
                1f - PrismMotion.At(age, 0f, Motion.PressIn), scale);
        }

        DrawPillLabel(drawList, face, label, saved, rect, crossing ? arrive : 1f, scale);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private ButtonFace PillFace(ImDrawListPtr drawList, Rect rect, bool saved, bool hovered, uint key, float opacity) =>
        Button.Surface(drawList, rect, controls, saved ? ButtonStyle.Tinted : ButtonStyle.Solid, ButtonRole.Normal,
            true, hovered, key, opacity);

    private static void DrawPillLabel(ImDrawListPtr drawList, in ButtonFace face, string label, bool saved, Rect rect,
        float alpha, float scale)
    {
        var mark = PrismMotion.Mark(drawList);
        var ink = face.LabelInk with { W = 1f };
        var glyphSize = saved ? Typography.LineHeight(PrismType.Caption) : 0f;
        var lead = glyphSize > 0f ? glyphSize + Metrics.Space.Xs * scale : 0f;
        var room = MathF.Max(1f, rect.Width - Metrics.Space.Lg * scale - lead);
        var labelStyle = FitStyle(label, room, Button.LabelStyle(rect.Height));
        var fitted = Typography.FitText(label, room, labelStyle);
        var size = Typography.Measure(fitted, labelStyle);
        var textLeft = face.Face.Center.X - (size.X + lead) * 0.5f;
        if (glyphSize > 0f)
        {
            PhoneIcon.Draw(drawList, new Vector2(textLeft + glyphSize * 0.5f, face.Face.Center.Y), PhoneIcons.Check,
                ink, glyphSize);
        }

        Typography.Draw(drawList, new Vector2(textLeft + lead, face.Face.Center.Y - size.Y * 0.5f), fitted,
            ink, labelStyle);
        if (alpha < 1f)
        {
            PrismMotion.Shift(drawList, mark, Vector2.Zero, alpha);
        }
    }

    private bool TryOnGlam(GlamDto glam, string key)
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
            return false;
        }

        tryOnKey = key;
        tryOnAt = ImGui.GetTime();
        UiFeedback.Play(UiSound.Success);
        return true;
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

        var picked = glamMenu.Draw(full, ActionSheetStyle.From(ui),
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
        top += PrismType.DrawHero(drawList, new Vector2(left, top), glam.Title, width, PrismArt.Fore) +
               Metrics.Space.Xs * scale;
        top = DrawByline(drawList, left, right, top, glam, scale);
        var meta = MetaLine(glam);
        if (meta.Length > 0)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top), meta, PrismArt.Muted, PrismType.Caption, width);
        }
        if (!glamour.HasGlamourer)
        {
            top += Metrics.Space.Xs * scale + Typography.DrawWrappedLeft(
                new Vector2(left, top + Metrics.Space.Xs * scale), Loc.T(L.Prism.TryOnWithoutGlamourer),
                PrismArt.Muted, PrismType.Caption, width);
        }

        if (glam.Placing > 0 && glam.WeekId.Length > 0)
        {
            top = DrawWeekLine(drawList, left, right, top, glam, scale);
        }

        if (glam.InspiredBy.Length > 0)
        {
            var tap = Metrics.Size.TapTarget * scale;
            var label = Loc.T(L.Prism.InspiredBy);
            var size = Typography.Measure(label, PrismType.Caption);
            var row = new Rect(new Vector2(left, top),
                new Vector2(left + MathF.Max(MathF.Min(size.X, width), tap), top + tap));
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

    private float DrawByline(ImDrawListPtr drawList, float left, float right, float top, GlamDto glam, float scale)
    {
        if (glam.Author is not { } author)
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
        var followWidth = other ? tap : 0f;
        var facts = DrawGlamFacts(drawList, handleLeft, right, middle, glam, scale);
        var handle = Typography.FitText(AuthorLine(author),
            MathF.Max(1f, right - handleLeft - followWidth - facts - Metrics.Space.Sm * scale), PrismType.Headline);
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

        var following = glams.Follows(author) ?? false;
        if (RoundButton.Icon(drawList, new Vector2(handleLeft + handleWidth + tap * 0.5f, middle), tap * 0.5f,
                following ? PhoneIcons.UserCheck : PhoneIcons.UserPlus, controls, ButtonStyle.Bare,
                Loc.T(following ? L.Prism.Following : L.Prism.Follow), HoverLabelSide.Above,
                glyphInk: following ? PrismArt.Muted : null))
        {
            glams.SetFollow(author, !following);
        }

        return top + tap;
    }

    private string MetaLine(GlamDto glam)
    {
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / SecondsPerMinute;
        var key = HashCode.Combine(glam.TryOns, glam.CreatedAtUnix, minute, Loc.Current.Code);
        if (metaText.IsCurrent(key))
        {
            return metaText.Value;
        }

        var line = glam.TryOns > 0 ? TriedOnLine(glam.TryOns) : string.Empty;
        if (glam.CreatedAtUnix > 0)
        {
            var ago = TimeText.Ago(glam.CreatedAtUnix);
            line = line.Length > 0 ? string.Concat(line, GlamMetaSeparator, ago) : ago;
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
        var inner = full.Width - gap * 2f;
        for (var index = 0; index < more.Length; index++)
        {
            var item = more[index];
            if (DrawGridTile(drawList, GridCell(full.Min.X + gap, top, inner, index, scale), Cover(item), item.Title,
                    PhoneIcons.Heart, item.Likes > 0 ? NumberText.Compact(item.Likes) : string.Empty, scale))
            {
                OpenGlam(item);
            }
        }

        return top + GridHeight(inner, more.Length, scale);
    }

    private float DrawGlamFacts(ImDrawListPtr drawList, float left, float right, float middle, GlamDto glam,
        float scale)
    {
        var glyph = PrismArt.RowIconUnits * scale;
        var pitch = glyph + Metrics.Space.Sm * scale;
        var x = DrawBodyFact(drawList, left, right, middle, glam, glyph, scale);
        x = DrawFact(drawList, x, middle, glam.Vanilla ? PrismFact.Vanilla : PrismFact.Modded,
            Loc.T(glam.Vanilla ? L.Prism.VanillaStamp : L.Prism.ModdedStamp), glyph, pitch);
        return right - x;
    }

    private float DrawBodyFact(ImDrawListPtr drawList, float left, float right, float middle, GlamDto glam,
        float glyph, float scale)
    {
        var female = glam.Gender == FemaleGender;
        var race = RaceLabel(glam.Race, female);
        if (race.Length == 0)
        {
            return right;
        }

        var fitted = Typography.FitText(race, MathF.Max(1f, (right - left) * BodyFactShare), PrismType.Caption);
        var textLeft = right - Typography.Measure(fitted, PrismType.Caption).X;
        Typography.Draw(drawList, new Vector2(textLeft, middle - Typography.LineHeight(PrismType.Caption) * 0.5f),
            fitted, PrismArt.Muted, PrismType.Caption);
        var symbol = new Vector2(textLeft - Metrics.Space.Xxs * scale - glyph * 0.5f, middle);
        PrismArt.FactIcon(drawList, symbol, glyph, female ? PrismFact.Female : PrismFact.Male, PrismArt.Muted);
        var half = new Vector2(glyph * 0.5f, glyph * 0.5f);
        HoverTooltip.Show(new Rect(symbol - half, new Vector2(right, middle + half.Y)),
            Loc.T(female ? L.Prism.GenderFemale : L.Prism.GenderMale), HoverLabelSide.Above);
        return symbol.X - glyph * 0.5f - Metrics.Space.Sm * scale;
    }

    private static float DrawFact(ImDrawListPtr drawList, float right, float middle, PrismFact fact, string label,
        float size, float pitch)
    {
        var center = new Vector2(right - pitch * 0.5f, middle);
        PrismArt.FactIcon(drawList, center, size, fact, PrismArt.Muted);
        var half = new Vector2(pitch * 0.5f, pitch * 0.5f);
        HoverTooltip.Show(new Rect(center - half, center + half), label, HoverLabelSide.Above);
        return right - pitch;
    }

    private string RaceLabel(int race, bool female)
    {
        if (race <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(race, female, Loc.Current.Code);
        return raceText.IsCurrent(key) ? raceText.Value : raceText.Store(key, gameData.RaceName((uint)race, female));
    }

    private float DrawPieceSection(ImDrawListPtr drawList, float left, float right, float top, GlamGearDto[] gear,
        float scale)
    {
        if (pieceOrder.Length == 0)
        {
            return top;
        }

        var ownedKey = HashCode.Combine(ownedPieces, Loc.Current.Code);
        var owned = ownedCountText.IsCurrent(ownedKey)
            ? ownedCountText.Value
            : ownedCountText.Store(ownedKey, Loc.T(L.Prism.OwnedCount, ownedPieces));
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
        using (UiScope.PushId(index))
        {
            if (RoundButton.Icon(drawList, wand, tap * 0.5f, PhoneIcons.Wand, controls, ButtonStyle.Bare,
                    Loc.T(L.Prism.TryOnPiece), HoverLabelSide.Below, glyphInk: PrismArt.Muted))
            {
                TryOnPiece(piece, statsFor ?? string.Empty);
            }
        }

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

    private static bool HighlightRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        var bleed = new Vector2(Metrics.Space.Xs * scale, 0f);
        return HoverWash(drawList, row, new Rect(row.Min - bleed, row.Max + bleed), Metrics.Radius.Md * scale);
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
        var installedKey = HashCode.Combine(installed, Loc.Current.Code);
        var caption = !glamour.HasPenumbra ? Loc.T(L.Prism.ModsNeedPenumbra)
            : installedText.IsCurrent(installedKey) ? installedText.Value
            : installedText.Store(installedKey, Loc.Plural(L.Prism.InstalledCount, installed));
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
        PrismArt.SlotTile(drawList, tile, slot, scale);
        var (side, allowed, installed) = ModLine(mod);
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
        if (modBylines.TryGetValue((mod, slot), out var line))
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
        modBylines[(mod, slot)] = line;
        return line;
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

    private (string Side, bool Allowed, bool Installed) ModLine(GlamModDto mod)
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
            var allowed = ModLinks.IsAllowed(mod.Link);
            var installed = glamour.HasMod(mod);
            cached = (installed ? Loc.T(L.Prism.Installed) : allowed ? Loc.T(L.Prism.GetMod) : string.Empty, allowed,
                installed);
            modLines[mod] = cached;
        }

        return cached;
    }
}

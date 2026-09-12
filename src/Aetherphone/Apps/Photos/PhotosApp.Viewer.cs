using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Sharing;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private struct PhotoInfo
    {
        public string Name;
        public string Taken;
        public string Dimensions;
        public string Size;
    }

    private const float ViewerRowCenter = 22f;
    private const float ViewerEdgeInset = 16f;
    private const float ViewerIconRadius = 18f;
    private const float ViewerIconSize = 24f;
    private const float ViewerCaptionLift = 7f;
    private const float ViewerMetaDrop = 13f;
    private const float ViewerCaptionReserve = 100f;
    private const float ViewerArrowInset = 22f;
    private const float ViewerTopScrimExtra = 52f;
    private const float ViewerBottomScrimExtra = 96f;
    private const float ViewerActionRowHeight = 48f;
    private const float ViewerLoadingLift = 14f;
    private const float ViewerLoadingRadius = 13f;
    private const float InfoRowHeight = 40f;
    private const float InfoMinimumFraction = 0.3f;
    private const float InfoMaximumFraction = 0.6f;
    private const int InfoRowCount = 4;
    private const long KilobyteBytes = 1024L;
    private const long MegabyteBytes = 1024L * 1024L;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteMuted = new(1f, 1f, 1f, 0.74f);
    private static readonly Vector4 ViewerBackdrop = new(0.02f, 0.02f, 0.03f, 1f);

    private string metaLabel = string.Empty;
    private string metaPath = string.Empty;
    private int metaIndex = -1;
    private int metaCount = -1;
    private PhotoInfo info;

    private void DrawViewer(Rect screen)
    {
        var scale = UiScale.Current;
        if (viewerPaths.Length == 0)
        {
            router.Pop(false);
            return;
        }

        viewerIndex = Math.Clamp(viewerIndex, 0, viewerPaths.Length - 1);
        var path = viewerPaths[viewerIndex];
        var safe = ContentWithin(screen);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(ViewerBackdrop));

        var texture = GetFull(path) ?? thumbnails.Get(path);
        var controls = new Rect(safe.Min, new Vector2(safe.Max.X, safe.Max.Y - ViewerActionRowHeight * scale));
        if (texture is not null)
        {
            if (zoomView.Draw(screen, texture, frameTheme, 0f, controls: controls))
            {
                Plugin.PhotoWindow.Open(() => GetFull(path) ?? thumbnails.Get(path), this);
            }
        }
        else
        {
            LoadingPulse.Draw(new Vector2(screen.Center.X, screen.Center.Y - ViewerLoadingLift * scale),
                ViewerLoadingRadius * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
        }

        DrawViewerTopBar(drawList, screen, safe, path, scale);
        DrawViewerBottomBar(drawList, screen, safe, path, scale);
        if (viewerPaths.Length <= 1 || zoomView.IsZoomed)
        {
            return;
        }

        if (PhotosChrome.Arrow(drawList, new Vector2(safe.Min.X + ViewerArrowInset * scale, screen.Center.Y), true,
                Ink, scale))
        {
            viewerIndex = (viewerIndex - 1 + viewerPaths.Length) % viewerPaths.Length;
            zoomView.Reset();
        }

        if (PhotosChrome.Arrow(drawList, new Vector2(safe.Max.X - ViewerArrowInset * scale, screen.Center.Y), false,
                Ink, scale))
        {
            viewerIndex = (viewerIndex + 1) % viewerPaths.Length;
            zoomView.Reset();
        }
    }

    private void DrawViewerTopBar(ImDrawListPtr drawList, Rect screen, Rect safe, string path, float scale)
    {
        PhotosChrome.TopScrim(drawList, screen.Min, screen.Max, (frameTheme.TopZoneHeight + ViewerTopScrimExtra) * scale);
        var rowCenterY = safe.Min.Y + ViewerRowCenter * scale;
        var taken = ResolveTaken(path);
        var caption = Typography.FitText(DayLabel(taken), safe.Width - ViewerCaptionReserve * scale, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, rowCenterY - ViewerCaptionLift * scale), caption,
            White, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, rowCenterY + ViewerMetaDrop * scale),
            ViewerMeta(path, taken), WhiteMuted, TextStyles.Footnote);

        var chipRadius = SocialChrome.BackChipRadius * scale;
        var chipCenter = new Vector2(safe.Min.X + ViewerEdgeInset * scale + chipRadius, rowCenterY);
        if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, Ink))
        {
            router.Pop();
            return;
        }

        if (viewerInTrash || zoomView.IsZoomed)
        {
            return;
        }

        var editLabel = Loc.T(L.Photos.Edit);
        var editWidth = TextButton.Width(editLabel, scale);
        var editCenter = new Vector2(safe.Max.X - ViewerEdgeInset * scale - editWidth * 0.5f, rowCenterY);
        if (TextButton.Draw(editCenter, editLabel, White, scale))
        {
            OpenEditor(path);
        }
    }

    private void DrawViewerBottomBar(ImDrawListPtr drawList, Rect screen, Rect safe, string path, float scale)
    {
        PhotosChrome.BottomScrim(drawList, screen.Min, screen.Max,
            (frameTheme.BottomZoneHeight + ViewerBottomScrimExtra) * scale);
        var rowCenterY = safe.Max.Y - ViewerRowCenter * scale;
        var radius = ViewerIconRadius * scale;
        if (viewerInTrash)
        {
            if (ViewerAction(drawList, ViewerSlot(safe, 0, 3, rowCenterY), radius, PhoneIcons.ArrowBackUp,
                    Loc.T(L.Photos.Recover), WhiteMuted, scale))
            {
                RecoverPhotos(new[] { path });
                return;
            }

            if (ViewerAction(drawList, ViewerSlot(safe, 1, 3, rowCenterY), radius, PhoneIcons.InfoCircle,
                    Loc.T(L.Photos.Info), WhiteMuted, scale))
            {
                OpenInfoSheet(path);
                return;
            }

            if (ViewerAction(drawList, ViewerSlot(safe, 2, 3, rowCenterY), radius, PhoneIcons.Trash,
                    Loc.T(L.Photos.DeletePermanently), Ink.Danger, scale))
            {
                AskDeleteForever(new[] { path });
            }

            return;
        }

        var canShare = share.CanShare(ShareKind.Photo, Id);
        var slots = canShare ? 5 : 4;
        var slot = 0;
        if (canShare)
        {
            if (ViewerAction(drawList, ViewerSlot(safe, slot, slots, rowCenterY), radius, PhoneIcons.Share,
                    Loc.T(L.Share.Action), WhiteMuted, scale))
            {
                share.Offer(new ShareItem(ShareKind.Photo, path, Id));
                return;
            }

            slot++;
        }

        var favorite = IsFavorite(path);
        if (ViewerAction(drawList, ViewerSlot(safe, slot, slots, rowCenterY), radius,
                favorite ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                Loc.T(favorite ? L.Photos.Unfavorite : L.Photos.Favorite), favorite ? White : WhiteMuted, scale))
        {
            ToggleFavorite(path);
        }

        slot++;
        if (ViewerAction(drawList, ViewerSlot(safe, slot, slots, rowCenterY), radius, PhoneIcons.SquareRoundedPlus,
                Loc.T(L.Photos.AddToAlbum), WhiteMuted, scale))
        {
            OpenAddToAlbum(new[] { path });
            return;
        }

        slot++;
        if (ViewerAction(drawList, ViewerSlot(safe, slot, slots, rowCenterY), radius, PhoneIcons.InfoCircle,
                Loc.T(L.Photos.Info), WhiteMuted, scale))
        {
            OpenInfoSheet(path);
            return;
        }

        slot++;
        if (ViewerAction(drawList, ViewerSlot(safe, slot, slots, rowCenterY), radius, PhoneIcons.Trash,
                Loc.T(L.Photos.Delete), Ink.Danger, scale))
        {
            AskDeletePhotos(new[] { path });
        }
    }

    private static Vector2 ViewerSlot(Rect safe, int index, int count, float centerY) =>
        new(safe.Min.X + safe.Width / count * (index + 0.5f), centerY);

    private bool ViewerAction(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, string tooltip,
        Vector4 glyphInk, float scale) =>
        PhotosChrome.GlassIcon(drawList, center, radius, glyph, ViewerIconSize, tooltip, Ink, glyphInk, scale,
            HoverLabelSide.Above);

    private string ViewerMeta(string path, DateTime taken)
    {
        if (ReferenceEquals(metaPath, path) && metaIndex == viewerIndex && metaCount == viewerPaths.Length)
        {
            return metaLabel;
        }

        metaPath = path;
        metaIndex = viewerIndex;
        metaCount = viewerPaths.Length;
        var clock = TimeText.Clock(taken);
        metaLabel = viewerPaths.Length > 1
            ? string.Concat(clock, "  ·  ", (viewerIndex + 1).ToString(Loc.Culture), " / ",
                viewerPaths.Length.ToString(Loc.Culture))
            : clock;
        return metaLabel;
    }

    private void OpenInfoSheet(string path)
    {
        var taken = ResolveTaken(path);
        info.Name = Path.GetFileName(path);
        info.Taken = string.Concat(taken.ToString("D", Loc.Culture), ", ", TimeText.Clock(taken));
        info.Dimensions = string.Empty;
        info.Size = string.Empty;
        try
        {
            var (width, height) = ImageProcessor.IdentifyDimensions(path);
            info.Dimensions = string.Concat(width.ToString(Loc.Culture), " × ", height.ToString(Loc.Culture));
            info.Size = FormatBytes(new FileInfo(path).Length);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not read the details of {info.Name}");
        }

        infoSheet.Open();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= MegabyteBytes)
        {
            return Loc.T(L.Photos.SizeMegabytes, ((double)bytes / MegabyteBytes).ToString("0.0", Loc.Culture));
        }

        return Loc.T(L.Photos.SizeKilobytes, Math.Max(1L, bytes / KilobyteBytes).ToString(Loc.Culture));
    }

    private void DrawInfoSheet(Rect area)
    {
        infoSheet.Draw(area, frameTheme, Loc.T(L.Photos.Info), InfoSheetFraction(area), drawInfoSheet);
    }

    private static float InfoSheetFraction(Rect area)
    {
        var content = InfoRowCount * InfoRowHeight * UiScale.Current;
        var fraction = (content + SheetSurface.ChromeHeight()) / MathF.Max(1f, area.Height);
        return Math.Clamp(fraction, InfoMinimumFraction, InfoMaximumFraction);
    }

    private void DrawInfoSheetContent(Rect content)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = InfoRowHeight * scale;
        var y = content.Min.Y;
        DrawInfoRow(drawList, content, ref y, rowHeight, Loc.T(L.Photos.InfoTaken), info.Taken, true, scale);
        DrawInfoRow(drawList, content, ref y, rowHeight, Loc.T(L.Photos.InfoDimensions), info.Dimensions, true, scale);
        DrawInfoRow(drawList, content, ref y, rowHeight, Loc.T(L.Photos.InfoSize), info.Size, true, scale);
        DrawInfoRow(drawList, content, ref y, rowHeight, Loc.T(L.Photos.InfoName), info.Name, false, scale);
    }

    private void DrawInfoRow(ImDrawListPtr drawList, Rect content, ref float y, float rowHeight, string label,
        string value, bool hairline, float scale)
    {
        if (value.Length == 0)
        {
            return;
        }

        var centerY = y + rowHeight * 0.5f;
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(content.Min.X, centerY - labelSize.Y * 0.5f), label,
            frameTheme.TextMuted, TextStyles.Subheadline);
        var maxValue = MathF.Max(1f, content.Width - labelSize.X - Metrics.Space.Md * scale);
        var fitted = Typography.FitText(value, maxValue, TextStyles.Body);
        var valueSize = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(content.Max.X - valueSize.X, centerY - valueSize.Y * 0.5f), fitted,
            frameTheme.TextStrong, TextStyles.Body);
        y += rowHeight;
        if (hairline)
        {
            drawList.AddLine(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y),
                ImGui.GetColorU32(frameTheme.Separator), Metrics.Stroke.Hairline);
        }
    }
}

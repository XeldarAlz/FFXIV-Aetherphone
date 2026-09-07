using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float SwatchRadius = 22f;
    private const float SwatchCellHeight = 82f;
    private const float SwatchRingGap = 4f;
    private const float SwatchCheckGlyph = 20f;
    private const int SwatchColumns = 4;
    private const float PreviewHeight = 150f;
    private const float PreviewPad = 12f;
    private const float PreviewBubblePadX = 10f;
    private const float PreviewBubblePadY = 7f;
    private const float PreviewBubbleGap = 8f;
    private const float WallpaperTileHeight = 64f;
    private const float WallpaperTileGap = 8f;
    private const float WallpaperTileRounding = 12f;
    private const int WallpaperColumns = 4;
    private const int PhotoColumns = 3;
    private const float PhotoTileGap = 6f;
    private const float SelectionRing = 2.5f;
    private const int ScreenResumeFrameGap = 3;

    private static readonly TextStyle SwatchLabelStyle = TextStyles.Caption1;
    private static readonly TextStyle PreviewTextStyle = TextStyles.Subheadline;

    private string[] wallpaperPhotos = Array.Empty<string>();
    private int lastWallpaperFrame = -100;

    private void DrawSettingsTab(Rect area)
    {
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (session.IsSignedIn)
            {
                DrawMyProfileRow(drawList);
            }

            DrawSectionLabel(Loc.T(L.Message.TabChats));
            if (DrawSettingRow(drawList, PhoneIcons.Palette, activeTheme.Accent, Loc.T(L.Message.ChatTheme),
                    Loc.T(activeTheme.Name)))
            {
                router.Push(MessageRoute.ChatTheme);
            }

            if (DrawSettingRow(drawList, PhoneIcons.Wallpaper, TintTeal, Loc.T(L.Message.Wallpaper)))
            {
                router.Push(MessageRoute.DefaultWallpaper);
            }

            var starredCount = configuration.MessageStarredMessages.Count;
            if (DrawSettingRow(drawList, PhoneIcons.Star, TintGold, Loc.T(L.Message.StarredTitle),
                    starredCount > 0 ? starredCount.ToString(Loc.Culture) : string.Empty))
            {
                router.Push(MessageRoute.Starred);
            }

            var archivedCount = configuration.MessageArchivedChats.Count;
            if (DrawSettingRow(drawList, PhoneIcons.Archive, TintSlate, Loc.T(L.Message.Archived),
                    archivedCount > 0 ? archivedCount.ToString(Loc.Culture) : string.Empty, separator: false))
            {
                router.Push(MessageRoute.Archived);
            }

            DrawSectionLabel(Loc.T(L.Settings.General));
            if (DrawSettingRow(drawList, PhoneIcons.Bell, TintRed, Loc.T(L.Settings.Notifications)))
            {
                settingsLauncher.Request(SettingsPageKind.Notifications);
                navigation.Open("settings");
            }

            if (DrawSettingRow(drawList, PhoneIcons.Shield, TintAzure, Loc.T(L.Settings.Privacy)))
            {
                settingsLauncher.Request(SettingsPageKind.Privacy);
                navigation.Open("settings");
            }

            if (DrawSettingRow(drawList, PhoneIcons.Phone, TintGreen, Loc.T(L.Phone.SettingsTitle),
                    Loc.T(calls.Enabled ? L.Common.On : L.Common.Off)))
            {
                settingsLauncher.Request(SettingsPageKind.Calls);
                navigation.Open("settings");
            }

            if (DrawSettingRow(drawList, PhoneIcons.ShieldCheck, TintViolet, Loc.T(L.Friends.NewNumberTitle),
                    separator: false) && session.IsSignedIn)
            {
                router.Push(MessageRoute.Safety);
            }

            DrawInlineEmpty(drawList, Loc.T(L.Message.OpensSettings));
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawChatTheme(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Message.ChatTheme));
        var top = area.Min.Y + AppHeader.Height * scale;
        using (AppSurface.Begin(new Rect(new Vector2(area.Min.X, top), area.Max)))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            DrawHintParagraph(Loc.T(L.Message.ChatThemeHint), width);
            var previewOrigin = ImGui.GetCursorScreenPos();
            var previewRect = new Rect(previewOrigin, new Vector2(previewOrigin.X + width, previewOrigin.Y + PreviewHeight * scale));
            DrawChatPreview(drawList, previewRect, MessageWallpapers.Effective(configuration, string.Empty),
                configuration.MessageWallpaperPattern, activeTheme);
            ImGui.SetCursorScreenPos(previewOrigin);
            ImGui.Dummy(new Vector2(width, previewRect.Height + Metrics.Space.Lg * scale));

            var gridOrigin = ImGui.GetCursorScreenPos();
            var cellWidth = width / SwatchColumns;
            var themes = MessageThemes.All;
            var rows = (themes.Length + SwatchColumns - 1) / SwatchColumns;
            for (var index = 0; index < themes.Length; index++)
            {
                var column = index % SwatchColumns;
                var row = index / SwatchColumns;
                var cellMin = new Vector2(gridOrigin.X + column * cellWidth, gridOrigin.Y + row * SwatchCellHeight * scale);
                var cellMax = cellMin + new Vector2(cellWidth, SwatchCellHeight * scale);
                var swatchCenter = new Vector2((cellMin.X + cellMax.X) * 0.5f, cellMin.Y + SwatchRadius * scale + 6f * scale);
                var selected = string.Equals(themes[index].Id, activeTheme.Id, StringComparison.Ordinal);
                var hovered = UiInteract.Hover(cellMin, cellMax);
                var radius = SwatchRadius * scale * (hovered ? 1.06f : 1f);
                drawList.AddCircleFilled(swatchCenter, radius, ImGui.GetColorU32(themes[index].Accent), 40);
                if (selected)
                {
                    drawList.AddCircle(swatchCenter, radius + SwatchRingGap * scale, ImGui.GetColorU32(ink.TitleInk), 40,
                        SelectionRing * scale);
                    PhoneIcon.Draw(drawList, swatchCenter, PhoneIcons.Check, White, SwatchCheckGlyph * scale);
                }

                var label = Typography.FitText(Loc.T(themes[index].Name), cellWidth - 6f * scale, SwatchLabelStyle);
                Typography.DrawCentered(drawList, new Vector2(swatchCenter.X, swatchCenter.Y + SwatchRadius * scale + 14f * scale),
                    label, selected ? ink.TitleInk : ink.MutedInk, SwatchLabelStyle);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (UiInteract.Click(cellMin, cellMax, hovered) && !selected)
                {
                    configuration.MessageChatTheme = themes[index].Id;
                    configuration.Save();
                }
            }

            ImGui.SetCursorScreenPos(gridOrigin);
            ImGui.Dummy(new Vector2(width, rows * SwatchCellHeight * scale + 24f * scale));
        }
    }

    private void DrawHintParagraph(string text, float width)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 4f * scale, origin.Y + 4f * scale), text,
            ink.MutedInk, TextStyles.Footnote, width - 8f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private void DrawChatPreview(ImDrawListPtr drawList, Rect rect, string wallpaperId, bool pattern,
        in MessageTheme previewTheme)
    {
        var scale = UiScale.Current;
        MessageWallpapers.Paint(drawList, rect, wallpaperId, pattern, wallpaperImages);
        Squircle.Stroke(drawList, rect.Min, rect.Max, 0f, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
        var pad = PreviewPad * scale;
        var maxBubbleWidth = rect.Width * 0.72f;
        var incoming = Loc.T(L.Message.PreviewIncoming);
        var outgoing = Loc.T(L.Message.PreviewOutgoing);
        var incomingTop = rect.Min.Y + pad;
        var incomingBottom = DrawPreviewBubble(drawList, new Vector2(rect.Min.X + pad, incomingTop), incoming,
            MessageThemes.IncomingBubble, MessageThemes.IncomingInk, maxBubbleWidth, false, rect.Max.X - pad);
        var outgoingTop = incomingBottom + PreviewBubbleGap * scale;
        DrawPreviewBubble(drawList, new Vector2(rect.Max.X - pad, outgoingTop), outgoing,
            previewTheme.OutgoingBubble, MessageThemes.OutgoingInk, maxBubbleWidth, true, rect.Max.X - pad);
    }

    private float DrawPreviewBubble(ImDrawListPtr drawList, Vector2 anchor, string text, Vector4 fill, Vector4 textInk,
        float maxWidth, bool mine, float rightLimit)
    {
        var scale = UiScale.Current;
        var padX = PreviewBubblePadX * scale;
        var padY = PreviewBubblePadY * scale;
        var textSize = Typography.MeasureWrappedBlock(text, PreviewTextStyle, maxWidth - padX * 2f);
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var min = mine ? new Vector2(anchor.X - width, anchor.Y) : anchor;
        var max = min + new Vector2(width, height);
        var rounding = MessageThreadViewBase.BubbleRounding * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
        var tail = 7f * scale;
        if (mine)
        {
            drawList.AddTriangleFilled(new Vector2(max.X - rounding, min.Y), new Vector2(max.X + tail, min.Y),
                new Vector2(max.X, min.Y + rounding), ImGui.GetColorU32(fill));
        }
        else
        {
            drawList.AddTriangleFilled(new Vector2(min.X + rounding, min.Y), new Vector2(min.X - tail, min.Y),
                new Vector2(min.X, min.Y + rounding), ImGui.GetColorU32(fill));
        }

        Typography.DrawWrappedLeft(new Vector2(min.X + padX, min.Y + padY), text, textInk, PreviewTextStyle,
            maxWidth - padX * 2f);
        return max.Y;
    }

    private void DrawWallpaper(Rect area, string conversationId)
    {
        var scale = UiScale.Current;
        var frame = ImGui.GetFrameCount();
        if (frame - lastWallpaperFrame > ScreenResumeFrameGap)
        {
            wallpaperPhotos = library.List();
        }

        lastWallpaperFrame = frame;
        var scoped = conversationId.Length > 0;
        DrawScreenHeader(area, Loc.T(L.Message.Wallpaper));
        var top = area.Min.Y + AppHeader.Height * scale;
        var current = MessageWallpapers.Effective(configuration, conversationId);
        var hasOverride = scoped && configuration.MessageChatWallpapers.ContainsKey(conversationId);
        using (AppSurface.Begin(new Rect(new Vector2(area.Min.X, top), area.Max)))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            DrawHintParagraph(Loc.T(scoped ? L.Message.WallpaperChatOnly : L.Message.WallpaperHint), width);
            var previewOrigin = ImGui.GetCursorScreenPos();
            var previewRect = new Rect(previewOrigin, new Vector2(previewOrigin.X + width, previewOrigin.Y + PreviewHeight * scale));
            DrawChatPreview(drawList, previewRect, current, configuration.MessageWallpaperPattern, activeTheme);
            ImGui.SetCursorScreenPos(previewOrigin);
            ImGui.Dummy(new Vector2(width, previewRect.Height + Metrics.Space.Lg * scale));

            var optionRows = scoped ? 2 : 1;
            var optionCard = GroupCard.Begin(ui, optionRows, SettingRowHeight);
            var pattern = DrawCardSwitchRow(drawList, optionCard.NextRow(), PhoneIcons.Sparkles, TintViolet,
                Loc.T(L.Message.WallpaperPattern), configuration.MessageWallpaperPattern, "message.wallpaper.pattern");
            if (pattern != configuration.MessageWallpaperPattern)
            {
                configuration.MessageWallpaperPattern = pattern;
                configuration.Save();
            }

            if (scoped && DrawCardRow(drawList, optionCard.NextRow(), PhoneIcons.Wallpaper, TintTeal,
                    Loc.T(L.Message.WallpaperUseDefault), hasOverride ? string.Empty : Loc.T(L.Message.WallpaperDefault),
                    chevron: false) && hasOverride)
            {
                configuration.MessageChatWallpapers.Remove(conversationId);
                configuration.Save();
            }

            optionCard.End();
            DrawCardGap();

            DrawInsetSectionLabel(Loc.T(L.Message.WallpaperColors));
            DrawWallpaperColorGrid(drawList, width, current, conversationId);
            DrawInsetSectionLabel(Loc.T(L.Message.WallpaperPhotos));
            DrawWallpaperPhotoGrid(drawList, width, current, conversationId);
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawWallpaperColorGrid(ImDrawListPtr drawList, float width, string current, string conversationId)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var gap = WallpaperTileGap * scale;
        var tileWidth = (width - gap * (WallpaperColumns - 1)) / WallpaperColumns;
        var tileHeight = WallpaperTileHeight * scale;
        var colors = MessageWallpapers.Colors;
        var rows = (colors.Length + WallpaperColumns - 1) / WallpaperColumns;
        var selectedIsColor = !MessageWallpapers.IsPhoto(current);
        for (var index = 0; index < colors.Length; index++)
        {
            var column = index % WallpaperColumns;
            var row = index / WallpaperColumns;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var selected = selectedIsColor && (string.Equals(colors[index].Id, current, StringComparison.Ordinal)
                || (current.Length == 0 && index == 0));
            var hovered = UiInteract.Hover(min, max);
            Squircle.Fill(drawList, min, max, WallpaperTileRounding * scale, ImGui.GetColorU32(colors[index].Color));
            Squircle.Stroke(drawList, min, max, WallpaperTileRounding * scale,
                ImGui.GetColorU32(selected ? ink.TitleInk : hovered ? ink.ChipHover : ink.ChipStroke),
                selected ? SelectionRing * scale : 1f);
            if (selected)
            {
                PhoneIcon.Draw(drawList, (min + max) * 0.5f, PhoneIcons.Check, ink.TitleInk, SwatchCheckGlyph * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                SetWallpaper(conversationId, index == 0 ? string.Empty : colors[index].Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (tileHeight + gap) + Metrics.Space.Sm * scale));
    }

    private void DrawWallpaperPhotoGrid(ImDrawListPtr drawList, float width, string current, string conversationId)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        if (wallpaperPhotos.Length == 0)
        {
            var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 4f * scale, origin.Y), Loc.T(L.Common.NoPhotos),
                ink.MutedInk, TextStyles.Footnote, width - 8f * scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
            return;
        }

        var gap = PhotoTileGap * scale;
        var cell = (width - gap * (PhotoColumns - 1)) / PhotoColumns;
        var scrollY = ImGui.GetScrollY();
        var viewHeight = ImGui.GetWindowSize().Y;
        var windowTop = ImGui.GetWindowPos().Y;
        var selectedPath = MessageWallpapers.PhotoPath(current);
        for (var index = 0; index < wallpaperPhotos.Length; index++)
        {
            var column = index % PhotoColumns;
            var rowIndex = index / PhotoColumns;
            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowIndex * (cell + gap));
            var max = min + new Vector2(cell, cell);
            var localTop = min.Y - windowTop + scrollY;
            if (localTop + cell < scrollY - cell || localTop > scrollY + viewHeight + cell)
            {
                continue;
            }

            var hovered = UiInteract.Hover(min, max);
            var texture = wallpaperImages.Get(wallpaperPhotos[index]);
            var rounding = WallpaperTileRounding * scale;
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                    ImDrawFlags.RoundCornersAll);
            }

            var selected = string.Equals(wallpaperPhotos[index], selectedPath, StringComparison.Ordinal);
            if (selected)
            {
                Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ink.TitleInk), SelectionRing * scale);
                drawList.AddCircleFilled((min + max) * 0.5f, SwatchCheckGlyph * 0.8f * scale,
                    ImGui.GetColorU32(ink.Accent), 24);
                PhoneIcon.Draw(drawList, (min + max) * 0.5f, PhoneIcons.Check, White, SwatchCheckGlyph * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                SetWallpaper(conversationId, MessageWallpapers.PhotoId(wallpaperPhotos[index]));
            }
        }

        var rows = (wallpaperPhotos.Length + PhotoColumns - 1) / PhotoColumns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cell + gap)));
    }

    private void SetWallpaper(string conversationId, string id)
    {
        if (conversationId.Length > 0)
        {
            configuration.MessageChatWallpapers[conversationId] = id;
        }
        else
        {
            configuration.MessageWallpaper = id;
        }

        configuration.Save();
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float HeroAspect = 9f / 16f;
    private const float PromptTile = 56f;
    private const float RailCardWidth = 150f;
    private const float RailCardGap = 12f;
    private const float RailCaptionHeight = 40f;
    private const int RailCardLimit = 10;
    private const int NearbyRowLimit = 3;
    private const float NearbyRowHeight = 58f;
    private const float FabClearance = 76f;
    private const float SeekStepSeconds = 10f;
    private const float ReactionSize = 28f;
    private const float ReactionHitRadius = 20f;
    private const float UpNextCardHeight = 64f;
    private const float NearbyRefreshSeconds = 5f;

    private const int PlayerActionFullscreen = 0;
    private const int PlayerActionWindow = 1;
    private const int PlayerActionTracks = 2;
    private const int PlayerActionScreen = 3;
    private const int PlayerActionStop = 4;

    private readonly PanRail historyRail = new();
    private Spring heroActionsFade;
    private RefreshCadence nearbyCadence;
    private TextCache standbyText;
    private TextCache followingText;
    private TextCache upNextText;
    private TextCache upNextLabel;

    private bool CanDrive => !watchAlong.IsViewing || watchAlong.CanControlPlayback;

    private void DrawWatchTab(Rect body, float scale)
    {
        var idle = CurrentEntry is null && !video.HasMedia;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(Metrics.Space.Xs);
            if (idle)
            {
                DrawWatchIdle(scale);
            }
            else
            {
                DrawWatchPlayer(scale);
            }

            Gap(idle ? Metrics.Space.Lg : FabClearance);
        }

        if (!idle && ComposeFab.Draw(TabBar.ContentArea(body, scale), "##aetherstreamAddFab", ui.Accent,
                PhoneIcons.Plus,
                Loc.T(L.AetherStream.AddVideoTitle), "aetherstream.composer", phoneGlyph: true))
        {
            OpenAddSheet();
        }
    }

    private void DrawWatchIdle(float scale)
    {
        PollCopiedLink();
        if (nearbyCadence.Advance(ImGui.GetIO().DeltaTime, NearbyRefreshSeconds))
        {
            nearbyCadence.Reset();
            watchAlong.RequestNearbyStreams();
        }

        DrawPartyStandby(scale);
        DrawLocalMediaPrompt(scale);
        DrawPromptCard(scale);
        DrawCopiedLinkChip(scale);
        DrawImportProgress(scale);
        DrawUpNextCard(scale);
        DrawContinueWatching(scale);
        DrawNearbyParties(scale);
    }

    private void DrawPartyStandby(float scale)
    {
        string text;
        if (watchAlong.IsViewing)
        {
            text = standbyText.Format(Loc.T(L.AetherStream.StandbyViewer), watchAlong.HostName() ?? string.Empty);
        }
        else if (watchAlong.IsHosting && watchAlong.HasCompany)
        {
            var remaining = watchAlong.IdleGraceSeconds;
            text = watchAlong.IsPartyOpen || remaining <= 0f
                ? Loc.T(L.AetherStream.StandbyHostOpen)
                : standbyText.Clock(Loc.T(L.AetherStream.StandbyHostGrace), (int)MathF.Ceiling(remaining));
        }
        else
        {
            return;
        }

        var pad = Metrics.Space.Md * scale;
        var width = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f - 22f * scale;
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, width).Y;
        var card = BeginBlock(textHeight + pad * 2f);
        var drawList = ImGui.GetWindowDrawList();
        TintedCard(drawList, card, ui.Accent);
        LivePill.DrawLamp(drawList, new Vector2(card.Min.X + pad + 5f * scale, card.Center.Y), ui.Accent,
            (float)ImGui.GetTime(), scale);
        Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad + 22f * scale, card.Min.Y + pad), text, Ink.TitleInk,
            TextStyles.Subheadline, width);
        EndBlock();
        Gap(Metrics.Space.Md);
    }

    private void DrawPromptCard(float scale)
    {
        var viewing = watchAlong.IsViewing;
        var title = Loc.T(viewing ? L.AetherStream.PromptSuggestTitle : L.AetherStream.PromptTitle);
        var hint = Loc.T(viewing ? L.AetherStream.PromptSuggestHint : L.AetherStream.PromptHint);
        var pad = Metrics.Space.Xl * scale;
        var innerWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, innerWidth).Y;
        var tile = PromptTile * scale;
        var fieldHeight = FieldRowHeight * scale;
        var linkHeight = viewing ? 0f : Typography.LineHeight(TextStyles.SubheadlineEmphasized) + 22f * scale;
        var height = pad + tile + Metrics.Space.Lg * scale + titleHeight + Metrics.Space.Xs * scale + hintHeight
            + Metrics.Space.Xl * scale + fieldHeight + linkHeight + pad;

        var card = BeginBlock(height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, true);

        var top = card.Min.Y + pad;
        var tileMin = new Vector2(card.Center.X - tile * 0.5f, top);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), FontAwesomeIcon.Tv,
            AccentRing.Ink, tile * 0.48f);
        top += tile + Metrics.Space.Lg * scale;

        Typography.DrawCentered(drawList, new Vector2(card.Center.X, top + titleHeight * 0.5f),
            Typography.FitText(title, innerWidth, TextStyles.Title2), Ink.TitleInk, TextStyles.Title2);
        top += titleHeight + Metrics.Space.Xs * scale;

        Typography.DrawWrappedCentered(drawList, new Vector2(card.Center.X, top + hintHeight * 0.5f), hint,
            Ink.MutedInk, TextStyles.Subheadline, innerWidth);
        top += hintHeight + Metrics.Space.Xl * scale;

        var fieldRow = new Rect(new Vector2(card.Min.X + pad, top), new Vector2(card.Max.X - pad, top + fieldHeight));
        UiAnchors.Report("aetherstream.composer", fieldRow);
        if (DrawLinkField(fieldRow, "##aetherstreamIdleLink", "aetherstream.idle.paste", scale)
            && MediaInput.Normalize(linkInput).Length > 0)
        {
            SubmitInput(linkInput, QueueAddMode.PlayNow);
        }

        if (!viewing && TextLink(new Vector2(card.Center.X, top + fieldHeight + linkHeight * 0.5f),
                Loc.T(L.AetherStream.BrowseLocalFile), Ink.AccentLink))
        {
            addMode = QueueAddMode.PlayNow;
            PickLocalFile();
        }

        EndBlock();
    }

    private void DrawCopiedLinkChip(float scale)
    {
        if (copiedLink.Length == 0 || string.Equals(copiedLink, dismissedCopiedLink, StringComparison.Ordinal))
        {
            return;
        }

        Gap(Metrics.Space.Md);
        var row = BeginBlock(48f * scale);
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, row);
        var playRadius = 15f * scale;
        var playCenter = new Vector2(row.Max.X - Metrics.Space.Md * scale - playRadius, row.Center.Y);
        var textLeft = row.Min.X + Metrics.Space.Md * scale;
        var textWidth = playCenter.X - playRadius - Metrics.Space.Md * scale - textLeft;
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var hostHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (titleHeight + hostHeight) * 0.5f;
        var copiedLabel = Loc.T(watchAlong.IsViewing
            ? L.AetherStream.CopiedLinkSuggest
            : L.AetherStream.CopiedLinkPlay);
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(copiedLabel, textWidth, TextStyles.SubheadlineEmphasized), Ink.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(copiedLinkHost, textWidth, TextStyles.Footnote), Ink.MutedInk, TextStyles.Footnote);

        var overPlay = UiInteract.Hover(playCenter - new Vector2(playRadius, playRadius),
            playCenter + new Vector2(playRadius, playRadius));
        var pressed = HoverButton.Circle(drawList, "aetherstream.copied.play", playCenter, playRadius,
            watchAlong.IsViewing ? FontAwesomeIcon.PaperPlane : FontAwesomeIcon.Play, ui.Accent, WhiteInk,
            ImGui.GetIO().DeltaTime, 1f, true);
        var hovered = !overPlay && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (pressed || (!overPlay && UiInteract.Click(row.Min, row.Max, hovered)))
        {
            dismissedCopiedLink = copiedLink;
            SubmitInput(copiedLink, QueueAddMode.PlayNow);
        }

        EndBlock();
    }

    private void DrawImportProgress(float scale)
    {
        if (queue.ImportState != PlaylistImportState.Loading)
        {
            return;
        }

        Gap(Metrics.Space.Md);
        var row = BeginBlock(30f * scale);
        LoadingPulse.Caption(row.Center, Ink.MutedInk, ui.Accent, Loc.T(L.AetherStream.PlaylistImporting), 1f,
            TextStyles.Subheadline.Scale);
        EndBlock();
    }

    private void DrawContinueWatching(float scale)
    {
        if (watchAlong.IsViewing)
        {
            return;
        }

        var history = library.History;
        var count = Math.Min(history.Count, RailCardLimit);
        if (count == 0)
        {
            return;
        }

        Gap(Metrics.Space.Md);
        SectionLabel(Loc.T(L.AetherStream.ContinueWatching));

        var cardWidth = RailCardWidth * scale;
        var cardHeight = cardWidth * HeroAspect;
        var gap = RailCardGap * scale;
        var pad = PadX * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var railHeight = cardHeight + RailCaptionHeight * scale;
        var rail = new Rect(origin, origin + new Vector2(width, railHeight));
        var contentWidth = pad * 2f + count * cardWidth + (count - 1) * gap;
        var drawList = ImGui.GetWindowDrawList();
        VideoHistoryRecord? picked = null;

        historyRail.Begin(rail, contentWidth);
        for (var index = 0; index < count; index++)
        {
            var record = history[index];
            var left = rail.Min.X + pad + index * (cardWidth + gap) - historyRail.Offset;
            if (left > rail.Max.X || left + cardWidth < rail.Min.X)
            {
                continue;
            }

            var thumb = new Rect(new Vector2(left, rail.Min.Y), new Vector2(left + cardWidth, rail.Min.Y + cardHeight));
            var cardMax = new Vector2(thumb.Max.X, rail.Max.Y);
            var hovered = historyRail.Hover(thumb.Min, cardMax);
            DrawThumb(drawList, thumb, record.Url, record.ThumbnailUrl, Metrics.Radius.Md * scale);
            DrawResumeBar(drawList, thumb, record, scale);
            if (hovered)
            {
                Squircle.Fill(drawList, thumb.Min, thumb.Max, Metrics.Radius.Md * scale,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)));
                AppSkin.Icon(drawList, thumb.Center, IconGlyph.Of(FontAwesomeIcon.Play), WhiteInk, 1.1f);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            Typography.Draw(drawList, new Vector2(left, thumb.Max.Y + Metrics.Space.Xs * scale),
                Typography.FitText(record.Title, cardWidth, TextStyles.SubheadlineEmphasized), Ink.TitleInk,
                TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList,
                new Vector2(left, thumb.Max.Y + Metrics.Space.Xs * scale
                    + Typography.LineHeight(TextStyles.SubheadlineEmphasized)),
                Typography.FitText(record.Source, cardWidth, TextStyles.Footnote), Ink.MutedInk, TextStyles.Footnote);

            if (historyRail.Tapped(thumb.Min, cardMax, hovered))
            {
                picked = record;
            }
        }

        historyRail.End();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, railHeight));

        if (picked is not null)
        {
            PlayHistory(picked, QueueAddMode.PlayNow);
        }
    }

    private static void DrawResumeBar(ImDrawListPtr drawList, Rect thumb, VideoHistoryRecord record, float scale)
    {
        if (record.PositionSeconds <= 0d || record.DurationSeconds is not { } duration || duration <= 0d)
        {
            return;
        }

        var fraction = (float)Math.Clamp(record.PositionSeconds / duration, 0d, 1d);
        var inset = 6f * scale;
        var barY = thumb.Max.Y - inset - 3f * scale;
        var barMin = new Vector2(thumb.Min.X + inset, barY);
        var barMax = new Vector2(thumb.Max.X - inset, barY + 3f * scale);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.32f)), 2f * scale);
        drawList.AddRectFilled(barMin, new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y),
            ImGui.GetColorU32(Ink.Accent), 2f * scale);
    }

    private void PlayHistory(VideoHistoryRecord record, QueueAddMode mode)
    {
        var entry = AetherStreamQueue.FromRecord(new VideoQueueRecord
        {
            Url = record.Url,
            Title = record.Title,
            Source = record.Source,
            DurationSeconds = record.DurationSeconds,
            ThumbnailUrl = record.ThumbnailUrl,
        });
        if (mode != QueueAddMode.PlayNow)
        {
            queue.Insert(entry, mode);
            ShellToast.Show(Loc.T(mode == QueueAddMode.PlayNext
                ? L.AetherStream.PlayingNextToast
                : L.AetherStream.AddedToQueueToast));
            return;
        }

        CancelPendingJoin();
        queue.PlayNow(entry, record.PositionSeconds);
        activeTab = StreamTab.Watch;
    }

    private void DrawNearbyParties(float scale)
    {
        if (watchAlong.Mode != WatchAlongMode.None || watchAlong.IsJoining)
        {
            return;
        }

        var nearby = watchAlong.Nearby;
        var count = Math.Min(nearby.Count, NearbyRowLimit);
        if (count == 0)
        {
            return;
        }

        Gap(Metrics.Space.Md);
        SectionLabel(Loc.T(L.AetherStream.JoinNearbyHeader));
        for (var index = 0; index < count; index++)
        {
            DrawNearbyRow(nearby[index], scale);
        }
    }

    private void DrawNearbyRow(NearbyStream row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, NearbyRowHeight * scale, ui.HoverWash);
        var bounds = cell.Bounds;
        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(bounds.Min.X + PadX * scale + avatarRadius, bounds.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.DisplayName, string.Empty,
            row.AvatarUrl, remoteImages, lodestone, 0.8f, 28);

        var liveLabel = Loc.T(L.Common.Live);
        var pillWidth = LivePill.Width(liveLabel, scale);
        var pillOrigin = new Vector2(bounds.Max.X - PadX * scale - pillWidth,
            bounds.Center.Y - LivePill.Height(scale) * 0.5f);
        LivePill.Draw(drawList, pillOrigin, liveLabel, theme.Danger, (float)ImGui.GetTime(), scale);

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = pillOrigin.X - Metrics.Space.Md * scale - textLeft;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var handleHeight = row.Handle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = bounds.Center.Y - (nameHeight + handleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(row.DisplayName, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);
        if (row.Handle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
                Typography.FitText(row.Handle, textWidth - 12f * scale, TextStyles.Footnote), Ink.MutedInk,
                TextStyles.Footnote);
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
        if (cell.Tapped)
        {
            watchAlong.Join(row.HostId);
            activeTab = StreamTab.Party;
        }
    }

    private void DrawWatchPlayer(float scale)
    {
        DrawHero(scale);
        DrawPlaybackNotices(scale);
        DrawLocalMediaPrompt(scale);
        DrawNowPlayingTitle(scale);
        DrawProgress(scale);
        if (CanDrive)
        {
            DrawTransport(scale);
        }

        if (watchAlong.IsViewing)
        {
            DrawFollowing(scale);
        }

        DrawVolume(scale);
        DrawReactionBar(scale);
        DrawImportProgress(scale);
        DrawUpNextCard(scale);
    }

    private void DrawHero(float scale)
    {
        var width = ScrollLayout.StableContentWidth() - PadX * 2f * scale;
        var hero = BeginBlock(width * HeroAspect);
        var rounding = Metrics.Radius.Card * scale;
        var drawList = ImGui.GetWindowDrawList();
        var current = CurrentEntry;

        Elevation.Card(drawList, hero.Min, hero.Max, rounding, scale);
        Squircle.Fill(drawList, hero.Min, hero.Max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)));
        var liveHandle = screen.Engine.ScreenViewHandle;
        if (liveHandle != nint.Zero && video.HasMedia && video.FrameVersion > 0)
        {
            drawList.AddImageRounded(new ImTextureID(liveHandle), hero.Min, hero.Max, Vector2.Zero, Vector2.One,
                0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        }
        else if (VideoThumbnailResolver.Get(remoteImages, http, current?.Url, current?.ThumbnailUrl) is { } thumbnail)
        {
            drawList.AddImageRounded(thumbnail.Handle, hero.Min, hero.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                rounding, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            AppSkin.Icon(drawList, hero.Center, IconGlyph.Of(FontAwesomeIcon.Tv), Ink.FaintInk, 1.8f);
        }

        Squircle.Stroke(drawList, hero.Min, hero.Max, rounding, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
        drawList.PushClipRect(hero.Min, hero.Max, true);
        VideoStageOverlay.DrawReactions(drawList, hero, watchAlong.Reactions, scale);
        drawList.PopClipRect();
        DrawHeroOverlay(drawList, hero, scale);
        EndBlock();
    }

    private void DrawHeroOverlay(ImDrawListPtr drawList, Rect hero, float scale)
    {
        var delta = ImGui.GetIO().DeltaTime;
        var rounding = Metrics.Radius.Card * scale;
        var loading = video.State == VideoPlaybackState.Loading;
        var presentable = video.HasMedia && !loading;
        var hovered = presentable && UiInteract.Hover(hero.Min, hero.Max);
        var eased = Math.Clamp(heroActionsFade.Step(hovered ? 1f : 0f, Motion.HoverLift, delta), 0f, 1f);
        if (eased > 0.01f)
        {
            Squircle.Fill(drawList, hero.Min, hero.Max, rounding,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.34f * eased)));
        }

        if (loading)
        {
            Squircle.Fill(drawList, hero.Min, hero.Max, rounding, ImGui.GetColorU32(StageBacking));
            LoadingPulse.Draw(new Vector2(hero.Center.X, hero.Center.Y - 10f * scale), 14f * scale, ui.Accent,
                WhiteInk, Loc.T(L.AetherStream.LoadingVideo), 1f, 0.8f, drawList);
        }

        if (watchAlong.InParty)
        {
            var label = Loc.T(L.Common.Live);
            var origin = hero.Min + new Vector2(Metrics.Space.Md * scale, Metrics.Space.Md * scale);
            var size = new Vector2(LivePill.Width(label, scale), LivePill.Height(scale));
            Squircle.Fill(drawList, origin, origin + size, size.Y * 0.34f, ImGui.GetColorU32(StageBacking));
            LivePill.Draw(drawList, origin, label, theme.Danger, (float)ImGui.GetTime(), scale);
        }

        DrawHeroFacepile(drawList, hero, scale);
        if (eased <= 0.01f)
        {
            return;
        }

        if (HoverButton.Circle(drawList, "aetherstream.hero.fullscreen", hero.Center, 22f * scale,
                FontAwesomeIcon.Expand, StageBacking, WhiteInk, delta, eased, hovered,
                Loc.T(L.AetherStream.Fullscreen)))
        {
            EnterTheater();
        }
    }

    private void DrawHeroFacepile(ImDrawListPtr drawList, Rect hero, float scale)
    {
        var watchers = watchAlong.Watching();
        if (watchers.Count < 2)
        {
            return;
        }

        var radius = 11f * scale;
        var step = radius * 1.5f;
        var shown = Math.Min(watchers.Count, 3);
        var right = hero.Max.X - Metrics.Space.Md * scale - radius;
        var centerY = hero.Max.Y - Metrics.Space.Md * scale - radius;
        for (var index = 0; index < shown; index++)
        {
            var participant = watchers[watchers.Count - 1 - index];
            var center = new Vector2(right - step * index, centerY);
            drawList.AddCircleFilled(center, radius + 1.5f * scale, ImGui.GetColorU32(StageBacking), 24);
            AvatarView.DrawRemote(drawList, center, radius, theme, participant.DisplayName, string.Empty,
                participant.AvatarUrl, remoteImages, lodestone, 0.6f, 16);
        }
    }

    private void DrawNowPlayingTitle(float scale)
    {
        Gap(Metrics.Space.Md);
        var current = CurrentEntry;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var sourceHeight = Typography.LineHeight(TextStyles.Footnote);
        var row = BeginBlock(titleHeight + sourceHeight + 2f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var moreRadius = 16f * scale;
        var moreCenter = new Vector2(row.Max.X - moreRadius, row.Center.Y);
        var textWidth = moreCenter.X - moreRadius - Metrics.Space.Md * scale - row.Min.X;
        Marquee.DrawLeftAuto(drawList, "aetherstream.nowPlaying.title",
            current?.Title ?? Loc.T(L.AetherStream.NothingPlaying), row.Min.X, row.Min.Y, textWidth,
            TextStyles.Title3, Ink.TitleInk);
        var source = current?.Subtitle ?? string.Empty;
        if (source.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(row.Min.X, row.Min.Y + titleHeight + 2f * scale),
                Typography.FitText(source, textWidth, TextStyles.Footnote), Ink.MutedInk, TextStyles.Footnote);
        }

        if (HoverButton.Circle(drawList, "aetherstream.player.more", moreCenter, moreRadius,
                FontAwesomeIcon.EllipsisH, Ink.ButtonFill, Ink.TitleInk, ImGui.GetIO().DeltaTime, 1f, true,
                Loc.T(L.AetherStream.MoreOptions)))
        {
            OpenPlayerActions();
        }

        EndBlock();
    }

    private void OpenPlayerActions()
    {
        BeginActions(SheetPurpose.Player, string.Empty);
        if (video.HasMedia)
        {
            AddAction(PlayerActionFullscreen, Loc.T(L.AetherStream.Fullscreen));
            AddAction(PlayerActionWindow, Loc.T(L.AetherStream.OpenScreenWindow));
            AddAction(PlayerActionTracks, Loc.T(L.AetherStream.TracksTitle));
        }

        AddAction(PlayerActionScreen, Loc.T(L.AetherStream.ScreenPlacement));
        if (!watchAlong.IsViewing)
        {
            AddAction(PlayerActionStop, Loc.T(L.AetherStream.Stop), danger: true);
        }

        actions.Open();
    }

    private void HandlePlayerAction(int code)
    {
        switch (code)
        {
            case PlayerActionFullscreen:
                EnterTheater();
                return;
            case PlayerActionWindow:
                screenWindow.IsOpen = true;
                return;
            case PlayerActionTracks:
                OpenTracksSheet();
                return;
            case PlayerActionScreen:
                router.Push(new StreamRoute(StreamScreen.Screen));
                return;
            case PlayerActionStop:
                queue.StopPlayback();
                return;
        }
    }

    private void EnterTheater()
    {
        CloseSheets();
        AppLandscape.Request(Id);
    }

    private void DrawProgress(float scale)
    {
        Gap(Metrics.Space.Sm);
        var labelHeight = Typography.LineHeight(TextStyles.Caption1);
        var block = BeginBlock(24f * scale + labelHeight + 2f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var progress = video.Progress;
        var duration = progress.Duration;
        var sliderRow = new Rect(block.Min, new Vector2(block.Max.X, block.Min.Y + 24f * scale));
        var shown = progress.Position;
        if (CanDrive && duration > 0f)
        {
            var result = Slider.Draw("aetherstream.progress", sliderRow, progress.Fraction, accentedTheme, 0f, 0f);
            if (result.Released)
            {
                SeekTo(result.Value * duration);
            }

            if (result.Dragging || result.Released)
            {
                shown = result.Value * duration;
            }
        }
        else
        {
            var track = new Rect(new Vector2(block.Min.X, sliderRow.Center.Y - 2f * scale),
                new Vector2(block.Max.X, sliderRow.Center.Y + 2f * scale));
            PassiveProgress(drawList, track, progress.Fraction, ui.Accent, Palette.WithAlpha(Ink.MutedInk, 0.3f),
                0.6f);
        }

        var labelY = sliderRow.Max.Y + 2f * scale;
        Typography.Draw(drawList, new Vector2(block.Min.X, labelY), TimeText.MinutesSeconds((int)shown), Ink.MutedInk,
            TextStyles.Caption1);
        var remaining = TimeText.MinutesSeconds((int)MathF.Max(0f, duration - shown));
        var remainingWidth = Typography.Measure(remaining, TextStyles.Caption1).X;
        Typography.Draw(drawList, new Vector2(block.Max.X - remainingWidth, labelY), remaining, Ink.MutedInk,
            TextStyles.Caption1);
        EndBlock();
    }

    private void SeekTo(float seconds)
    {
        if (watchAlong.IsViewing)
        {
            watchAlong.ControlSeek(seconds);
            return;
        }

        video.Seek(MathF.Max(0f, seconds));
    }

    private void TogglePlayback()
    {
        var paused = video.Progress.Paused;
        if (watchAlong.IsViewing)
        {
            watchAlong.ControlPause(!paused);
            return;
        }

        if (video.HasMedia)
        {
            video.Pause(!paused);
            return;
        }

        if (video.State == VideoPlaybackState.Failed)
        {
            RetryPlayback(false);
        }
        else if (queue.Current is null && queue.HasNext)
        {
            queue.Advance();
        }
    }

    private void SkipToNext()
    {
        if (watchAlong.IsViewing)
        {
            watchAlong.ControlNext();
            return;
        }

        queue.Advance();
    }

    private void DrawTransport(float scale)
    {
        var row = BeginBlock(64f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var delta = ImGui.GetIO().DeltaTime;
        var progress = video.Progress;
        var center = row.Center;
        var sideOffset = 84f * scale;
        if (HoverButton.Circle(drawList, "aetherstream.seek.back", new Vector2(center.X - sideOffset, center.Y),
                20f * scale, FontAwesomeIcon.UndoAlt, AppSkin.Transparent, Ink.TitleInk, delta, 1f, true,
                Loc.T(L.AetherStream.SeekBack)))
        {
            SeekTo(progress.Position - SeekStepSeconds);
        }

        var playing = video.HasMedia && !progress.Paused;
        var playRadius = 28f * scale;
        drawList.AddCircleFilled(center, playRadius, ImGui.GetColorU32(ui.Accent), 48);
        if (TransportButton.Draw(center, playRadius, playing ? TransportAction.Pause : TransportAction.Play,
                ui.Accent, WhiteInk, 1f, true))
        {
            TogglePlayback();
        }

        if (HoverButton.Circle(drawList, "aetherstream.seek.forward", new Vector2(center.X + sideOffset, center.Y),
                20f * scale, FontAwesomeIcon.RedoAlt, AppSkin.Transparent, Ink.TitleInk, delta, 1f, true,
                Loc.T(L.AetherStream.SeekForward)))
        {
            SeekTo(progress.Position + SeekStepSeconds);
        }

        EndBlock();
    }

    private void DrawFollowing(float scale)
    {
        Gap(Metrics.Space.Sm);
        var row = BeginBlock(SmallButtonHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var resyncLabel = Loc.T(L.AetherStream.Resync);
        var resyncWidth = Typography.Measure(resyncLabel, SmallButtonStyle).X + 28f * scale;
        var resync = new Rect(new Vector2(row.Max.X - resyncWidth, row.Min.Y), row.Max);
        var label = followingText.Format(Loc.T(L.AetherStream.ViewingStream), watchAlong.HostName() ?? string.Empty);
        var labelHeight = Typography.LineHeight(TextStyles.Subheadline);
        LivePill.DrawLamp(drawList, new Vector2(row.Min.X + 5f * scale, row.Center.Y), theme.Danger,
            (float)ImGui.GetTime(), scale);
        Typography.Draw(drawList, new Vector2(row.Min.X + 18f * scale, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, resync.Min.X - Metrics.Space.Md * scale - row.Min.X - 18f * scale,
                TextStyles.Subheadline), Ink.MutedInk, TextStyles.Subheadline);
        if (SmallButton(resync, resyncLabel, false))
        {
            watchAlong.ResyncNow();
        }

        EndBlock();
    }

    private void DrawVolume(float scale)
    {
        Gap(Metrics.Space.Xs);
        var row = BeginBlock(26f * scale);
        var result = VolumeSlider.Draw("aetherstream.volume", row, configuration.VideoVolume, accentedTheme);
        if (result.Dragging && Math.Abs(result.Value - configuration.VideoVolume) > 0.001f)
        {
            configuration.VideoVolume = result.Value;
            video.SetVolume((int)(result.Value * 100f));
        }

        if (result.Released)
        {
            configuration.VideoVolume = result.Value;
            video.SetVolume((int)(result.Value * 100f));
            configuration.Save();
        }

        EndBlock();
    }

    private bool ReactionsAvailable =>
        watchAlong.ServerSupportsParty && (watchAlong.IsViewing || (watchAlong.IsHosting && watchAlong.HasCompany));

    private void DrawReactionBar(float scale)
    {
        if (!ReactionsAvailable)
        {
            return;
        }

        Gap(Metrics.Space.Md);
        var row = BeginBlock(ReactionHitRadius * 2f * scale);
        DrawReactionButtons(ImGui.GetWindowDrawList(), row, 1f, scale);
        EndBlock();
    }

    private void DrawReactionButtons(ImDrawListPtr drawList, Rect row, float alpha, float scale)
    {
        var tokens = PartyReactions.Tokens;
        var slot = row.Width / tokens.Length;
        var hitRadius = MathF.Min(ReactionHitRadius * scale, slot * 0.5f);
        for (var index = 0; index < tokens.Length; index++)
        {
            var center = new Vector2(row.Min.X + slot * (index + 0.5f), row.Center.Y);
            var extent = new Vector2(hitRadius, hitRadius);
            var hovered = UiInteract.Hover(center - extent, center + extent);
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var grow = PressFx.Scale(tokens[index], pressed, PressFx.ControlPressedScale) * (hovered ? 1.14f : 1f);
            if (hovered)
            {
                drawList.AddCircleFilled(center, hitRadius, ImGui.GetColorU32(Palette.WithAlpha(WhiteInk, 0.10f * alpha)), 32);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            ReactionArt.Draw(drawList, tokens[index], center, ReactionSize * scale * grow, alpha, 1f);
            if (UiInteract.Click(center - extent, center + extent, hovered))
            {
                watchAlong.SendReaction(index);
            }
        }
    }

    private void DrawUpNextCard(float scale)
    {
        string title;
        string? url;
        string? thumbnailUrl = null;
        int remaining;
        if (watchAlong.IsViewing)
        {
            var hostQueue = watchAlong.HostQueue;
            if (hostQueue.Count == 0)
            {
                return;
            }

            title = hostQueue[0].Title;
            url = hostQueue[0].Url;
            remaining = hostQueue.Count;
        }
        else
        {
            var entries = queue.Entries;
            if (entries.Count == 0)
            {
                return;
            }

            title = entries[0].Title;
            url = entries[0].Url;
            thumbnailUrl = entries[0].ThumbnailUrl;
            remaining = entries.Count;
        }

        Gap(Metrics.Space.Lg);
        var card = BeginBlock(UpNextCardHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);
        var pad = Metrics.Space.Md * scale;
        var thumbHeight = card.Height - pad * 1.4f;
        var thumbMin = new Vector2(card.Min.X + pad * 0.7f, card.Center.Y - thumbHeight * 0.5f);
        var thumb = new Rect(thumbMin, thumbMin + new Vector2(thumbHeight * ThumbAspect, thumbHeight));
        DrawThumb(drawList, thumb, url, thumbnailUrl, Metrics.Radius.Sm * scale);

        var skipRadius = 16f * scale;
        var skipCenter = new Vector2(card.Max.X - pad - skipRadius, card.Center.Y);
        var canSkip = CanDrive;
        var startsQueue = !watchAlong.IsViewing && queue.Current is null;
        var overSkip = canSkip && UiInteract.Hover(skipCenter - new Vector2(skipRadius, skipRadius),
            skipCenter + new Vector2(skipRadius, skipRadius));
        var textLeft = thumb.Max.X + pad;
        var textRight = canSkip ? skipCenter.X - skipRadius - pad : card.Max.X - pad;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var top = card.Center.Y - (labelHeight + titleHeight) * 0.5f;
        var label = remaining > 1
            ? upNextText.Format(Loc.T(L.AetherStream.UpNextCount), remaining)
            : Loc.T(L.AetherStream.UpNext);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(upNextLabel.Upper(label), textRight - textLeft, TextStyles.FootnoteEmphasized),
            Ink.AccentLink, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + labelHeight),
            Typography.FitText(title, textRight - textLeft, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);

        if (canSkip && HoverButton.Circle(drawList, "aetherstream.upnext.skip", skipCenter, skipRadius,
                startsQueue ? FontAwesomeIcon.Play : FontAwesomeIcon.StepForward, Ink.ButtonFill, Ink.TitleInk,
                ImGui.GetIO().DeltaTime, 1f, true,
                Loc.T(startsQueue ? L.AetherStream.PlayNow : L.AetherStream.SkipToNext)))
        {
            SkipToNext();
        }

        var hovered = !overSkip && UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!overSkip && UiInteract.Click(card.Min, card.Max, hovered))
        {
            activeTab = StreamTab.Library;
            librarySegment = LibrarySegment.UpNext;
        }

        EndBlock();
    }
}

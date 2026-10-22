using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float NoticeSeconds = 6f;
    private const float NoticeBannerHeight = 56f;
    private const float NoticeGlyph = 16f;
    private const float RsvpRowHeight = 58f;
    private const float RsvpCheckRadius = 13f;
    private const float StatusTileHeight = 64f;
    private const float StatusTileGap = 10f;
    private const float ActionTileHeight = 68f;
    private const float ActionTileGap = 10f;
    private const float InfoRowHeight = 50f;
    private const float LocationLineHeight = 24f;
    private const float LocationGlyph = 14f;
    private const float FeedbackGap = 8f;
    private const int MaxLocationLines = 5;
    private const int MaxActionTiles = 3;

    private static readonly int[] QuickStatusCodes =
    {
        MusterStatuses.OnMyWay, MusterStatuses.RunningLate, MusterStatuses.Here, MusterStatuses.WhereExactly,
    };

    private readonly string[] locationLines = new string[MaxLocationLines];
    private readonly LocationTone[] locationTones = new LocationTone[MaxLocationLines];
    private readonly LocationAction[] actionKinds = new LocationAction[MaxActionTiles];
    private string? detailFetchId;
    private MusterDto? detailFetched;
    private bool detailLoading;
    private bool rsvpBusy;
    private bool rsvpFailed;
    private int statusBusyCode;
    private bool statusFailed;
    private string travelNotice = string.Empty;
    private float travelNoticeTimer;
    private TravelCache travelCache;

    private enum LocationTone : byte
    {
        Primary,
        Secondary,
        Muted,
        Here,
    }

    private enum LocationAction : byte
    {
        Flag,
        Travel,
        Invite,
    }

    private struct TravelCache
    {
        public string MusterId;
        public int WorldId;
        public uint TerritoryId;
        public int TargetWorldId;
        public int TargetTerritoryId;
        public TravelDestination Destination;
        public string Label;
    }

    private void ResetDetailState()
    {
        detailFetchId = null;
        detailFetched = null;
        detailLoading = false;
        rsvpBusy = false;
        rsvpFailed = false;
        statusBusyCode = 0;
        statusFailed = false;
        travelNotice = string.Empty;
        travelNoticeTimer = 0f;
        travelCache = default;
    }

    private void DrawDetail(in PhoneContext context, MusterRoute route)
    {
        var musterId = route.MusterId ?? string.Empty;
        var muster = ResolveMuster(musterId);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        using (UiScope.PushId("muster.detail"))
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            float bottom;
            if (muster is null)
            {
                bottom = DrawDetailMissing(drawList, musterId, origin, width, scale);
            }
            else
            {
                bottom = DrawDetailBody(drawList, muster, origin, width, scale);
            }

            MusterArt.Reserve(origin, width, bottom + MusterArt.BottomPad * scale);
        }

        var title = muster is null ? DisplayName : Loc.T(MusterCategories.Label(muster.Category));
        AppHeader.EndLargeTitle(in navBar, context, "muster.detail.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, route.BackTitle, back);
    }

    private float DrawDetailMissing(ImDrawListPtr drawList, string musterId, Vector2 origin, float width,
        float scale)
    {
        EnsureDetailFetch(musterId);
        var centerX = origin.X + width * 0.5f;
        var top = origin.Y + StateTop * scale;
        if (detailLoading)
        {
            LoadingPulse.Spinner(new Vector2(centerX, top + 40f * scale), 13f * scale, ui.Accent);
            return top + 80f * scale;
        }

        return MusterArt.StateScreen(drawList, ui, centerX, top, width, FontAwesomeIcon.MapMarkerAlt,
            Loc.T(L.Muster.UnavailableTitle), Loc.T(L.Muster.UnavailableHint), scale);
    }

    private float DrawDetailBody(ImDrawListPtr drawList, MusterDto muster, Vector2 origin, float width, float scale)
    {
        var nowUnix = NowUnix();
        var mine = IsMine(muster);
        var cursorY = DrawDetailHero(drawList, muster, origin.X, origin.Y, width, nowUnix, !mine, scale);
        cursorY = DrawNoticeBanner(drawList, muster, origin.X, cursorY, width, nowUnix, scale);
        if (!mine)
        {
            cursorY = DrawRsvp(drawList, muster, origin.X, cursorY + MusterArt.CardGap * scale, width, scale);
        }

        if (mine)
        {
            cursorY = DrawListing(drawList, muster, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
        }

        cursorY = DrawWhere(drawList, muster, origin.X, cursorY + MusterArt.SectionGap * scale, width, !mine, scale);
        return mine
            ? cursorY
            : DrawReportRow(drawList, muster, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
    }

    private MusterDto? ResolveMuster(string musterId)
    {
        if (store.Mine is { } mine && mine.Id == musterId)
        {
            return mine;
        }

        var found = Find(store.ContactMusters, musterId) ?? Find(store.GoingMusters, musterId)
            ?? Find(store.Directory, musterId);
        if (found is not null)
        {
            return found;
        }

        var fetched = detailFetched;
        return fetched is not null && fetched.Id == musterId ? fetched : null;
    }

    private static MusterDto? Find(MusterDto[] source, string musterId)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id == musterId)
            {
                return source[index];
            }
        }

        return null;
    }

    private void EnsureDetailFetch(string musterId)
    {
        if (string.Equals(detailFetchId, musterId, StringComparison.Ordinal))
        {
            return;
        }

        detailFetchId = musterId;
        detailFetched = null;
        detailLoading = true;
        store.FetchDetail(musterId, muster =>
        {
            if (!string.Equals(detailFetchId, musterId, StringComparison.Ordinal))
            {
                return;
            }

            detailFetched = muster;
            detailLoading = false;
        });
    }

    private float DrawDetailHero(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        long nowUnix, bool showHost, float scale)
    {
        var key = new TranslationKey(TranslationSurface.Muster, muster.Id);
        var description = muster.Description.Length > 0
            ? translation.View(key, muster.Description).Text
            : Loc.T(MusterCategories.Label(muster.Category));
        var live = muster.StartsAtUnix <= nowUnix;
        var span = MathF.Max(1f, muster.EndsAtUnix - muster.StartsAtUnix);
        var poster = new MusterPoster
        {
            Category = muster.Category,
            Eyebrow = labels.Range(muster),
            Title = description,
            Status = labels.Hero(muster, nowUnix),
            Live = live,
            Going = showHost && IsGoing(muster),
            HostName = showHost ? MusterText.HostLabel(muster) : string.Empty,
            HostWorld = muster.HostWorld,
            HostFrameId = muster.HostFrameId,
            Identity = MusterText.Identity(muster),
            Place = string.Empty,
            Count = labels.Count(muster),
            Full = muster.MaxAttendees > 0 && muster.RsvpCount >= muster.MaxAttendees,
            ShowProgress = live,
            Progress = live ? Math.Clamp((nowUnix - muster.StartsAtUnix) / span, 0f, 1f) : 0f,
        };
        var origin = new Vector2(left, top);
        var height = MusterArt.PosterHeight(in poster, width, 0, scale);
        MusterArt.Poster(drawList, KeyFor("hero", muster.Id), in poster, origin, width, 0, theme, images, lodestone,
            false, scale);
        var cursorY = top + height;
        if (muster.Description.Length == 0)
        {
            return cursorY;
        }

        var linkHeight = TranslateLink.Height(translation, key, muster.Lang, scale);
        if (linkHeight <= 0f)
        {
            return cursorY;
        }

        var pad = Metrics.Space.Lg * scale;
        TranslateLink.Draw(translation, confirm, key, muster.Lang, muster.Description,
            new Vector2(left + pad, cursorY + Metrics.Space.Xs * scale), width - pad * 2f, ui.MutedInk, ui.Accent,
            scale);
        return cursorY + Metrics.Space.Xs * scale + linkHeight;
    }

    private float DrawNoticeBanner(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        long nowUnix, float scale)
    {
        if (muster.HostNotice == MusterNotices.None)
        {
            return top;
        }

        var bannerTop = top + MusterArt.CardGap * scale;
        var min = new Vector2(left, bannerTop);
        var max = new Vector2(left + width, bannerTop + NoticeBannerHeight * scale);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var centerY = (min.Y + max.Y) * 0.5f;
        var tileSize = 32f * scale;
        var tileMin = new Vector2(min.X + pad, centerY - tileSize * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
            tileSize * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize, tileSize) * 0.5f, NoticeIcon(muster.HostNotice),
            AccentRing.Ink, NoticeGlyph * scale);
        var textLeft = tileMin.X + tileSize + MusterArt.TextGap * scale;
        MusterArt.Labels(drawList, textLeft, max.X - pad, centerY, NoticeLabel(muster.HostNotice),
            labels.NoticeAgo(muster, nowUnix), ui.TitleInk, ui.MutedInk, scale);
        return max.Y;
    }

    private static FontAwesomeIcon NoticeIcon(int notice) =>
        notice switch
        {
            MusterNotices.StartingNow => FontAwesomeIcon.Bullhorn,
            MusterNotices.MovedSpots => FontAwesomeIcon.MapMarkedAlt,
            _ => FontAwesomeIcon.Moon,
        };

    private static string NoticeLabel(int notice) =>
        notice switch
        {
            MusterNotices.StartingNow => Loc.T(L.Muster.NoticeStartingNow),
            MusterNotices.MovedSpots => Loc.T(L.Muster.NoticeMovedSpots),
            _ => Loc.T(L.Muster.NoticeWrappingUp),
        };

    private float DrawRsvp(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width, float scale)
    {
        var going = IsGoing(muster);
        float bottom;
        if (!going)
        {
            var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + MusterArt.PillHeight * scale));
            UiAnchors.Report("muster.rsvp", rect);
            if (rsvpBusy)
            {
                ui.PaintAccentPill(rect, string.Empty, false, false, TextStyles.Headline);
                LoadingPulse.Spinner(rect.Center, 9f * scale, AccentRing.Ink);
            }
            else if (MusterArt.Action(ui, rect, Loc.T(L.Muster.ImGoing), true))
            {
                SetRsvp(muster.Id, true);
            }

            bottom = rect.Max.Y;
        }
        else
        {
            bottom = DrawGoingCard(drawList, muster, left, top, width, scale);
            bottom = DrawStatusTiles(drawList, muster, left, bottom + MusterArt.CardGap * scale, width, scale);
        }

        var failure = rsvpFailed ? Loc.T(L.Muster.RsvpFailed) : statusFailed ? Loc.T(L.Muster.StatusFailed) : string.Empty;
        return DrawFeedback(left, bottom, width, failure, scale);
    }

    private float DrawFeedback(float left, float top, float width, string text, float scale)
    {
        if (text.Length == 0)
        {
            return top;
        }

        var gap = FeedbackGap * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(left + Metrics.Space.Lg * scale, top + gap), text,
            ui.Theme.Danger, TextStyles.Footnote, width - Metrics.Space.Lg * 2f * scale);
        return top + gap + height;
    }

    private float DrawGoingCard(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        float scale)
    {
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + RsvpRowHeight * scale);
        UiAnchors.Report("muster.rsvp", new Rect(min, max));
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var centerY = (min.Y + max.Y) * 0.5f;
        var radius = RsvpCheckRadius * scale;
        var checkCenter = new Vector2(min.X + pad + radius, centerY);
        drawList.AddCircleFilled(checkCenter, radius, ImGui.GetColorU32(MusterArt.LiveColor), 24);
        ProgressRing.CenterIcon(drawList, checkCenter, FontAwesomeIcon.Check, AccentRing.Ink, radius);
        var actionLabel = Loc.T(L.Muster.CantMakeIt);
        var actionSize = Typography.Measure(actionLabel, TextStyles.Body);
        var actionRect = new Rect(new Vector2(max.X - pad - actionSize.X - Metrics.Space.Sm * scale, min.Y),
            new Vector2(max.X, max.Y));
        var textLeft = checkCenter.X + radius + MusterArt.TextGap * scale;
        var title = Typography.FitText(Loc.T(L.Muster.GoingSection),
            MathF.Max(1f, actionRect.Min.X - Metrics.Space.Sm * scale - textLeft), TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - titleHeight * 0.5f), title, ui.TitleInk,
            TextStyles.Headline);
        if (rsvpBusy)
        {
            LoadingPulse.Spinner(new Vector2(max.X - pad - 9f * scale, centerY), 8f * scale, ui.Accent);
            return max.Y;
        }

        var hovered = UiInteract.Hover(actionRect.Min, actionRect.Max);
        var ink = hovered ? Palette.Lighten(ui.Theme.Danger, 0.12f) : ui.Theme.Danger;
        Typography.Draw(drawList, new Vector2(max.X - pad - actionSize.X, centerY - actionSize.Y * 0.5f), actionLabel,
            ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(actionRect.Min, actionRect.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            SetRsvp(muster.Id, false);
        }

        return max.Y;
    }

    private void SetRsvp(string musterId, bool going)
    {
        rsvpBusy = true;
        rsvpFailed = false;
        statusFailed = false;
        store.SetRsvp(musterId, going, ok =>
        {
            rsvpBusy = false;
            rsvpFailed = !ok;
            UiFeedback.Play(ok ? going ? UiSound.Success : UiSound.ToggleOff : UiSound.Caution);
        });
    }

    private float DrawStatusTiles(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        float scale)
    {
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(left + Metrics.Space.Lg * scale, top), Loc.T(L.Muster.YourStatus),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        var gridTop = top + labelHeight + Metrics.Space.Sm * scale;
        var gap = StatusTileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = StatusTileHeight * scale;
        var current = store.MyStatus(muster.Id);
        UiAnchors.Report("muster.status", new Rect(new Vector2(left, gridTop),
            new Vector2(left + width, gridTop + tileHeight * 2f + gap)));
        for (var index = 0; index < QuickStatusCodes.Length; index++)
        {
            var code = QuickStatusCodes[index];
            var column = index % 2;
            var rowIndex = index / 2;
            var min = new Vector2(left + column * (tileWidth + gap), gridTop + rowIndex * (tileHeight + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var busy = statusBusyCode == code;
            var tapped = MusterArt.ActionTile(drawList, ui, KeyFor("status", code, muster.Id), rect,
                MusterArt.StatusIcon(code), StatusLabel(code), MusterArt.StatusColor(code), code == current,
                statusBusyCode == 0, scale);
            if (busy)
            {
                LoadingPulse.Spinner(new Vector2(rect.Max.X - 14f * scale, rect.Min.Y + 14f * scale), 6f * scale,
                    MusterArt.StatusColor(code));
            }

            if (tapped && code != current)
            {
                SetStatus(muster.Id, code);
            }
        }

        return gridTop + tileHeight * 2f + gap;
    }

    private void SetStatus(string musterId, int code)
    {
        statusBusyCode = code;
        statusFailed = false;
        rsvpFailed = false;
        store.SetStatus(musterId, code, ok =>
        {
            statusBusyCode = 0;
            statusFailed = !ok;
            if (!ok)
            {
                UiFeedback.Play(UiSound.Caution);
            }
        });
    }

    private float DrawListing(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        float scale)
    {
        var max = new Vector2(left + width, top + InfoRowHeight * scale);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var centerY = (top + max.Y) * 0.5f;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var label = Loc.T(L.Muster.FactListing);
        var labelWidth = Typography.Measure(label, TextStyles.Body).X;
        Typography.Draw(drawList, new Vector2(left + pad, centerY - lineHeight * 0.5f), label, ui.TitleInk,
            TextStyles.Body);
        var value = Typography.FitText(Loc.T(muster.IsPublic ? L.Muster.ListedPublicly : L.Muster.ListedPrivately),
            MathF.Max(1f, width - pad * 3f - labelWidth), TextStyles.Body);
        var valueWidth = Typography.Measure(value, TextStyles.Body).X;
        Typography.Draw(drawList, new Vector2(max.X - pad - valueWidth, centerY - lineHeight * 0.5f), value,
            ui.MutedInk, TextStyles.Body);
        return max.Y;
    }

    private float DrawWhere(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        bool includeTravel, float scale)
    {
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.WhereSection), ui.TitleInk);
        var destination = includeTravel ? ResolveTravel(muster) : default;
        var lineCount = CollectLocationLines(muster, destination.Kind == TravelKind.AlreadyThere);
        if (lineCount > 0)
        {
            var pad = Metrics.Space.Lg * scale;
            var lineHeight = LocationLineHeight * scale;
            var max = new Vector2(left + width, cursorY + pad * 2f + lineCount * lineHeight);
            ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale);
            var glyph = LocationGlyph * scale;
            ProgressRing.CenterIcon(drawList, new Vector2(left + pad + glyph * 0.5f, cursorY + pad + lineHeight * 0.5f),
                FontAwesomeIcon.MapMarkerAlt, ui.Accent, glyph);
            var textLeft = left + pad + glyph + MusterArt.TextGap * scale;
            for (var index = 0; index < lineCount; index++)
            {
                var style = StyleFor(locationTones[index]);
                var lineTop = cursorY + pad + index * lineHeight;
                var fitted = Typography.FitText(locationLines[index], MathF.Max(1f, max.X - pad - textLeft), style);
                var height = Typography.LineHeight(style);
                Typography.Draw(drawList, new Vector2(textLeft, lineTop + (lineHeight - height) * 0.5f), fitted,
                    InkFor(locationTones[index]), style);
            }

            cursorY = max.Y;
        }

        return DrawLocationActions(drawList, muster, destination, left, cursorY, width, includeTravel, scale);
    }

    private int CollectLocationLines(MusterDto muster, bool alreadyThere)
    {
        var count = 0;
        if (muster.Spot.Length > 0)
        {
            AddLocationLine(ref count, muster.Spot, LocationTone.Primary);
        }

        var place = MusterText.Place(muster);
        if (place.Length > 0 && !string.Equals(place, muster.Spot, StringComparison.Ordinal))
        {
            AddLocationLine(ref count, place, count == 0 ? LocationTone.Primary : LocationTone.Secondary);
        }

        var housing = MusterText.HousingLine(muster);
        if (housing.Length > 0)
        {
            AddLocationLine(ref count, housing, LocationTone.Secondary);
        }

        var coordinates = MusterText.Coordinates(muster);
        if (coordinates.Length > 0)
        {
            AddLocationLine(ref count, coordinates, LocationTone.Muted);
        }

        if (alreadyThere)
        {
            AddLocationLine(ref count, Loc.T(L.Muster.YoureHere), LocationTone.Here);
        }

        return count;
    }

    private void AddLocationLine(ref int count, string text, LocationTone tone)
    {
        if (count >= MaxLocationLines)
        {
            return;
        }

        locationLines[count] = text;
        locationTones[count] = tone;
        count++;
    }

    private static TextStyle StyleFor(LocationTone tone) =>
        tone switch
        {
            LocationTone.Primary => TextStyles.BodyEmphasized,
            LocationTone.Here => TextStyles.SubheadlineEmphasized,
            LocationTone.Muted => TextStyles.Subheadline,
            _ => TextStyles.Body,
        };

    private Vector4 InkFor(LocationTone tone) =>
        tone switch
        {
            LocationTone.Primary => ui.TitleInk,
            LocationTone.Here => MusterArt.LiveColor,
            LocationTone.Muted => ui.MutedInk,
            _ => ui.BodyInk,
        };

    private TravelDestination ResolveTravel(MusterDto muster)
    {
        var worldId = store.CurrentWorldId;
        var territoryId = store.CurrentTerritoryId;
        if (string.Equals(travelCache.MusterId, muster.Id, StringComparison.Ordinal) && travelCache.WorldId == worldId
            && travelCache.TerritoryId == territoryId && travelCache.TargetWorldId == muster.WorldId
            && travelCache.TargetTerritoryId == muster.TerritoryId)
        {
            return travelCache.Destination;
        }

        var destination = TravelPlanner.Resolve((uint)muster.TerritoryId, (uint)muster.WorldId, (uint)worldId,
            territoryId);
        travelCache = new TravelCache
        {
            MusterId = muster.Id,
            WorldId = worldId,
            TerritoryId = territoryId,
            TargetWorldId = muster.WorldId,
            TargetTerritoryId = muster.TerritoryId,
            Destination = destination,
            Label = TravelPlanner.CanGo(in destination) ? TravelPlanner.Label(in destination) : string.Empty,
        };
        return destination;
    }

    private float DrawLocationActions(ImDrawListPtr drawList, MusterDto muster, TravelDestination destination,
        float left, float top, float width, bool includeTravel, float scale)
    {
        var count = 0;
        if (muster.MapId != 0)
        {
            actionKinds[count++] = LocationAction.Flag;
        }

        if (includeTravel && TravelPlanner.CanGo(in destination))
        {
            actionKinds[count++] = LocationAction.Travel;
        }

        var mine = store.Mine is { } hosted && hosted.Id == muster.Id;
        if (muster.IsPublic || mine)
        {
            actionKinds[count++] = LocationAction.Invite;
        }

        if (count == 0)
        {
            return top;
        }

        var gap = ActionTileGap * scale;
        var tilesTop = top + MusterArt.CardGap * scale;
        var tileWidth = (width - gap * (count - 1)) / count;
        var tileHeight = ActionTileHeight * scale;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(left + index * (tileWidth + gap), tilesTop);
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var kind = actionKinds[index];
            var tapped = MusterArt.ActionTile(drawList, ui, KeyFor("action", (int)kind, muster.Id), rect,
                ActionIcon(kind), ActionLabel(kind), ui.Accent, false, true, scale);
            if (kind == LocationAction.Travel && travelCache.Label.Length > 0)
            {
                HoverTooltip.Show(rect, travelCache.Label, HoverLabelSide.Above);
            }

            if (!tapped)
            {
                continue;
            }

            RunLocationAction(kind, muster, in destination);
        }

        var bottom = tilesTop + tileHeight;
        if (travelNoticeTimer > 0f && travelNotice.Length > 0)
        {
            bottom = DrawFeedback(left, bottom, width, travelNotice, scale);
        }

        return bottom;
    }

    private static FontAwesomeIcon ActionIcon(LocationAction kind) =>
        kind switch
        {
            LocationAction.Flag => FontAwesomeIcon.Flag,
            LocationAction.Travel => FontAwesomeIcon.Route,
            _ => FontAwesomeIcon.Link,
        };

    private static string ActionLabel(LocationAction kind) =>
        kind switch
        {
            LocationAction.Flag => Loc.T(L.Muster.FlagOnMap),
            LocationAction.Travel => Loc.T(L.Muster.Travel),
            _ => Loc.T(L.Muster.CopyInvite),
        };

    private void RunLocationAction(LocationAction kind, MusterDto muster, in TravelDestination destination)
    {
        switch (kind)
        {
            case LocationAction.Flag:
                var location = MusterText.Location(muster);
                LocationShare.OpenMap(in location);
                return;
            case LocationAction.Travel:
                TravelTo(in destination);
                return;
            default:
                ImGui.SetClipboardText(MusterShare.Compose(muster.Id));
                ShellToast.Show();
                return;
        }
    }

    private void TravelTo(in TravelDestination destination)
    {
        travelNotice = string.Empty;
        travelNoticeTimer = 0f;
        var outcome = TravelPlanner.Go(in destination);
        if (outcome == LifestreamOutcome.Started)
        {
            return;
        }

        if (outcome == LifestreamOutcome.NotInstalled)
        {
            ImGui.SetClipboardText(TravelPlanner.Command(in destination));
            ShellToast.Show();
            return;
        }

        UiFeedback.Play(UiSound.Caution);
        travelNotice = TravelPlanner.Notice(outcome, in destination);
        travelNoticeTimer = NoticeSeconds;
    }

    private float DrawReportRow(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + MusterArt.FieldRowHeight * scale));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);
        var hovered = MusterArt.RowWash(drawList, ui, row, scale);
        Typography.DrawCentered(drawList, row.Center, Loc.T(L.Muster.ReportTitle), ui.Theme.Danger, TextStyles.Body);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            OpenReport(muster.Id);
        }

        return row.Max.Y;
    }

    private void OpenReport(string musterId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Muster.ReportTitle),
            Submit = (reason, done) => SubmitReport(musterId, reason, done),
        });
    }

    private void SubmitReport(string musterId, ReportReason reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await api.Safety.ReportAsync("muster", musterId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Muster] report failed");
            }

            done(ok);
        });
    }
}

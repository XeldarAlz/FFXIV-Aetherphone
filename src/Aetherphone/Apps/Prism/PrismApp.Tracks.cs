using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal readonly record struct TrackCardPhoto(string Url, PrismPlacingDto? Winner, GlamDto? Showcase, bool LastWeek);

internal sealed partial class PrismApp
{
    private const int MaxTracks = 3;
    private const float CardHeightPerWidth = 4f / 3f;
    private const float CardMarkShare = 0.4f;
    private const float CardMarkHeightShare = 0.38f;
    private const float CardEdgeRest = 0.55f;
    private const float CardGlintUnits = 3f;
    private const float CardPressScale = 0.97f;

    private static readonly string ResultsGlyph = IconGlyph.Of(FontAwesomeIcon.Trophy);
    private static readonly string GalleryGlyph = IconGlyph.Of(FontAwesomeIcon.Th);
    private static readonly string[] CardIds = { "prism.trackCard.0", "prism.trackCard.1", "prism.trackCard.2" };
    private static readonly TrackCardPhoto NoTrackPhoto = new(string.Empty, null, null, false);

    private readonly CachedText[] trackActionTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackCreditTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackEntryTexts = new CachedText[MaxTracks];
    private readonly Spring[] cardHover = new Spring[MaxTracks];
    private PrismWeekDto? lastWeekAsked;
    private string resultsWaitTrack = string.Empty;

    private bool HasTracks => store.Tracks.Length > 1;

    private bool VanillaEntry => HasTracks && store.Track == PrismTracks.Vanilla;

    private void DrawContest(ImDrawListPtr drawList, Rect full, Rect content, PrismWeekDto week, float scale)
    {
        if (!ReferenceEquals(week, lastWeekAsked))
        {
            lastWeekAsked = week;
            store.LoadLastWeekResults();
        }

        FollowResultsWait();
        var open = week.Id.Length > 0 && week.Phase.Length > 0;
        DrawArenaBackdrop(drawList, full, content.Min.Y + (Metrics.Space.Xs + Metrics.Size.TapTarget) * scale, week,
            open, scale);
        var top = DrawContestHeader(drawList, content, week, open, scale);
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = content.Width;
        var left = origin.X + content.Min.X - full.Min.X;
        var cursor = DrawArenaHero(list, left, origin.Y, width, week, open, scale);
        arenaBackdropBottom = cursor - body.Min.Y;
        if (open)
        {
            cursor = DrawContestPrompt(list, left, cursor, width, week.Phase, scale);
            cursor = DrawTrackCards(list, left, cursor, width, week, scale);
        }

        cursor = DrawLastWeekPodiums(list, left, cursor, width, week, scale);
        cursor = DrawRewardsLink(list, left, cursor, width, scale) + PrismArt.BottomMargin(scale);
        ImGui.Dummy(new Vector2(content.Width, cursor - origin.Y));
    }

    private float DrawContestHeader(ImDrawListPtr drawList, Rect content, PrismWeekDto week, bool open, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var radius = tap * 0.5f;
        var top = content.Min.Y + Metrics.Space.Xs * scale;
        var guide = new Vector2(content.Max.X - radius, top + radius);
        var rewards = guide - new Vector2(tap, 0f);
        var results = rewards - new Vector2(tap, 0f);
        var left = content.Min.X;
        var style = PrismType.Title;
        Typography.Draw(drawList, new Vector2(left, top + (tap - Typography.LineHeight(style)) * 0.5f),
            Typography.FitText(Loc.T(L.Prism.TabWeek), MathF.Max(1f, results.X - radius - left), style),
            PrismArt.Fore, style);
        if (IconButton(drawList, guide, PhoneIcons.HelpCircle, Loc.T(L.Prism.HowItWorks), scale,
                HoverLabelSide.Below))
        {
            StartGuide(tab);
        }

        if (IconButton(drawList, rewards, RewardsGlyph, Loc.T(L.Prism.Rewards), scale, HoverLabelSide.Below))
        {
            OpenRewards();
        }

        DrawResultsButton(drawList, results, week, open, scale);
        return top + tap;
    }

    private void DrawResultsButton(ImDrawListPtr drawList, Vector2 center, PrismWeekDto week, bool open, float scale)
    {
        if (ShownResults() is not { } results)
        {
            return;
        }

        if (open && week.Phase == PrismPhases.Results)
        {
            if (IconButton(drawList, center, GalleryGlyph, Loc.T(L.Prism.AllGlams), scale, HoverLabelSide.Below))
            {
                OpenGallery(results.WeekId, store.Track, results.Title);
            }

            return;
        }

        if (IconButton(drawList, center, ResultsGlyph, Loc.T(L.Prism.LastWeekWinners), scale, HoverLabelSide.Below))
        {
            OpenResults();
        }
    }

    private float DrawTrackCards(ImDrawListPtr drawList, float left, float top, float width, PrismWeekDto week,
        float scale)
    {
        var tracks = store.Tracks;
        var count = tracks.Length > 1 ? Math.Min(tracks.Length, MaxTracks) : 1;
        var gap = Metrics.Space.Sm * scale;
        var cardWidth = (width - gap * (count - 1)) / count;
        var cardBottom = top + cardWidth * CardHeightPerWidth;
        var bottom = cardBottom;
        for (var index = 0; index < count; index++)
        {
            var track = count > 1 ? tracks[index] : null;
            var x = left + index * (cardWidth + gap);
            var photo = TrackPhoto(track, week);
            DrawTrackCard(drawList, new Rect(new Vector2(x, top), new Vector2(x + cardWidth, cardBottom)), index, track,
                week, photo, scale);
            bottom = MathF.Max(bottom, DrawTrackNotes(drawList, x, cardBottom + Metrics.Space.Sm * scale, cardWidth,
                index, track, week, photo, scale));
        }

        return bottom;
    }

    private TrackCardPhoto TrackPhoto(PrismTrackDto? track, PrismWeekDto week)
    {
        if (PageEntry(track, week) is { ImageUrl.Length: > 0 } entry)
        {
            return new TrackCardPhoto(entry.ImageUrl, null, null, false);
        }

        var results = track is null || track.Id == store.Track ? ShownResults() : store.LastWeekResults(track.Id);
        if (CoverPlacing(results) is { ImageUrl.Length: > 0 } winner)
        {
            return new TrackCardPhoto(winner.ImageUrl, winner, null, results!.WeekId == week.PreviousWeekId);
        }

        if (track is not null && glams.Showcase(track.Id == PrismTracks.Vanilla) is { } glam)
        {
            return new TrackCardPhoto(Cover(glam), null, glam, false);
        }

        return NoTrackPhoto;
    }

    private static PrismEntryDto? PageEntry(PrismTrackDto? track, PrismWeekDto week) =>
        track is null ? week.MyEntry : track.MyEntry;

    private void DrawTrackCard(ImDrawListPtr drawList, Rect card, int index, PrismTrackDto? track, PrismWeekDto week,
        in TrackCardPhoto photo, float scale)
    {
        var entry = PageEntry(track, week);
        var action = ContestAction(index, week.Phase, entry is not null);
        var enabled = action.Length > 0 && (week.Phase == PrismPhases.Results || !store.Busy);
        var hovered = enabled && UiInteract.Hover(card.Min, card.Max);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var hover = Math.Clamp(cardHover[index].Step(hovered ? 1f : 0f, Motion.HoverLift, delta), 0f, 1f);
        if (ImGui.IsRectVisible(card.Min, card.Max))
        {
            var mark = PrismMotion.Mark(drawList);
            DrawTrackCardFace(drawList, card, index, track, week, photo, entry, action, enabled, hovered, scale);
            var glow = track?.Id == PrismTracks.Vanilla ? PrismGlow.Pearl : PrismGlow.Spectrum;
            PrismLight.Edge(drawList, card, 0f, glow, PrismLight.EdgeAlpha * (CardEdgeRest + (1f - CardEdgeRest) * hover),
                scale);
            var glint = MathF.Max(1f, CardGlintUnits * scale);
            PrismLight.Strip(drawList, card.Min.X, card.Max.X, card.Max.Y - glint, glint, PrismLight.TabAlpha * hover);
            if (hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                PrismMotion.Grow(drawList, mark, card.Center, CardPressScale);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            RunTrackCard(track, week.Phase, entry);
        }
    }

    private void DrawTrackCardFace(ImDrawListPtr drawList, Rect card, int index, PrismTrackDto? track,
        PrismWeekDto week, in TrackCardPhoto photo, PrismEntryDto? entry, string action, bool enabled, bool hovered,
        float scale)
    {
        var onCardPhoto = photo.Url.Length > 0;
        if (onCardPhoto)
        {
            DrawLook(drawList, card, photo.Url, false);
        }
        else
        {
            PrismLight.Week(drawList, card, week.Id, PrismArt.Page, PrismArt.Fore, card.Width * CardMarkShare,
                CardMarkHeightShare);
        }

        var inset = Metrics.Space.Md * scale;
        var pillHeight = Button.RegularHeight * scale;
        var nameLine = track is null ? 0f : Typography.LineHeight(PrismType.Headline);
        var pillBlock = action.Length > 0 ? pillHeight : 0f;
        var block = nameLine + (nameLine > 0f && pillBlock > 0f ? Metrics.Space.Sm * scale : 0f) + pillBlock;
        if (block <= 0f)
        {
            return;
        }

        if (onCardPhoto)
        {
            PrismArt.Scrim(drawList, card, block + inset, scale);
        }

        PrismArt.OverPhoto(onCardPhoto);
        var room = card.Width - inset * 2f;
        var y = card.Max.Y - inset - block;
        if (track is not null)
        {
            DrawTrackName(drawList, new Vector2(card.Min.X + inset, y), room, track.Id, scale);
        }

        if (pillBlock > 0f)
        {
            var busy = store.Busy && week.Phase == PrismPhases.Entering && entry is null &&
                       (track is null || track.Id == store.Track);
            var label = busy ? Loc.T(L.Prism.Uploading) : action;
            var pill = new Rect(new Vector2(card.Min.X + inset, card.Max.Y - inset - pillHeight),
                new Vector2(card.Max.X - inset, card.Max.Y - inset));
            Button.DrawLabel(drawList, Button.Surface(drawList, pill, onCardPhoto ? photoControls : controls,
                entry is not null ? ButtonStyle.Gray : ButtonStyle.Solid, ButtonRole.Normal, enabled, hovered,
                ImGui.GetID(CardIds[index])), label);
        }

        PrismArt.OverPhoto(false);
    }

    private static void DrawTrackName(ImDrawListPtr drawList, Vector2 topLeft, float room, string trackId, float scale)
    {
        var line = Typography.LineHeight(PrismType.Headline);
        var glyph = PrismArt.InlineIconUnits * scale;
        PrismArt.FactIcon(drawList, new Vector2(topLeft.X + glyph * 0.5f, topLeft.Y + line * 0.5f), glyph,
            trackId == PrismTracks.Vanilla ? PrismFact.Vanilla : PrismFact.Modded, PrismArt.Fore);
        var textLeft = topLeft.X + glyph + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, topLeft.Y),
            Typography.FitText(PrismTracks.Name(trackId), MathF.Max(1f, room - glyph - Metrics.Space.Xs * scale),
                PrismType.Headline), PrismArt.Fore, PrismType.Headline);
    }

    private float DrawTrackNotes(ImDrawListPtr drawList, float left, float top, float width, int index,
        PrismTrackDto? track, PrismWeekDto week, in TrackCardPhoto photo, float scale)
    {
        var line = Typography.LineHeight(PrismType.Caption);
        var reach = Metrics.Space.Xs * scale;
        var rowGap = Metrics.Space.Sm * scale;
        var entry = PageEntry(track, week);
        if (entry is not null)
        {
            var end = DrawGlyphCaption(drawList, new Vector2(left, top), left + width, StatusGlyph(entry.Status),
                EntryLine(index, entry), PrismArt.Muted, scale);
            if (UiInteract.HoverClick(new Vector2(left, top - reach), new Vector2(end, top + line + reach)))
            {
                SelectPage(track);
                Go(PrismScreen.Entry);
            }

            top += line + rowGap;
        }

        if (photo.Winner is { } winner)
        {
            var end = DrawGlyphCaption(drawList, new Vector2(left, top), left + width,
                photo.LastWeek ? PhoneIcons.Crown : PhoneIcons.Photo, CreditLine(index, winner, photo.LastWeek),
                PrismArt.Muted, scale);
            TapForOutfit(new Vector2(left, top - reach), new Vector2(end, top + line + reach), winner);
            top += line + rowGap;
        }
        else if (photo.Showcase is { } glam)
        {
            var end = DrawGlyphCaption(drawList, new Vector2(left, top), left + width, PhoneIcons.Photo,
                ShowcaseLine(index, glam), PrismArt.Muted, scale);
            if (UiInteract.HoverClick(new Vector2(left, top - reach), new Vector2(end, top + line + reach)))
            {
                OpenGlam(glam);
            }

            top += line + rowGap;
        }

        var rule = track is null ? string.Empty : TrackRule(track.Id);
        return rule.Length == 0
            ? top
            : top + Typography.DrawWrappedLeft(new Vector2(left, top), rule, PrismArt.Muted, PrismType.Caption, width);
    }

    private void RunTrackCard(PrismTrackDto? track, string phase, PrismEntryDto? entry)
    {
        SelectPage(track);
        if (phase == PrismPhases.Results)
        {
            if (ShownResults() is not null)
            {
                OpenResults();
                return;
            }

            resultsWaitTrack = store.Track;
            return;
        }

        if (phase == PrismPhases.Entering && entry is not null)
        {
            Go(PrismScreen.Entry);
        }
        else if (phase == PrismPhases.Entering)
        {
            StartPick();
        }
        else
        {
            StartJudging();
        }
    }

    private void FollowResultsWait()
    {
        if (resultsWaitTrack.Length == 0)
        {
            return;
        }

        if (resultsWaitTrack != store.Track)
        {
            resultsWaitTrack = string.Empty;
            return;
        }

        if (ShownResults() is null)
        {
            return;
        }

        resultsWaitTrack = string.Empty;
        OpenResults();
    }

    private void SelectPage(PrismTrackDto? track)
    {
        if (track is not null)
        {
            store.SelectTrack(track.Id);
        }
    }

    private static string TrackRule(string id) => id switch
    {
        PrismTracks.Vanilla => Loc.T(L.Prism.TrackVanillaRule),
        PrismTracks.Modded => Loc.T(L.Prism.TrackModdedRule),
        _ => string.Empty,
    };

    private string ContestAction(int index, string phase, bool entered)
    {
        var key = HashCode.Combine(phase, entered, Loc.Current.Code);
        if (trackActionTexts[index].IsCurrent(key))
        {
            return trackActionTexts[index].Value;
        }

        return trackActionTexts[index].Store(key, phase switch
        {
            PrismPhases.Entering when entered => Loc.T(L.Prism.SeeYourGlam),
            PrismPhases.Entering => Loc.T(L.Prism.Enter),
            PrismPhases.Judging => Loc.T(L.Prism.JudgeHeat),
            PrismPhases.Final => Loc.T(L.Prism.JudgeFinal),
            PrismPhases.Results => Loc.T(L.Prism.SeeResults),
            _ => string.Empty,
        });
    }

    private string EntryLine(int index, PrismEntryDto entry)
    {
        var key = HashCode.Combine(entry.Status, Loc.Current.Code);
        return trackEntryTexts[index].IsCurrent(key)
            ? trackEntryTexts[index].Value
            : trackEntryTexts[index].Store(key,
                Loc.T(L.Prism.WithTrack, Loc.T(L.Prism.YourLook), Loc.T(StatusName(entry.Status))));
    }

    private string CreditLine(int index, PrismPlacingDto winner, bool lastWeek)
    {
        var key = HashCode.Combine(winner.Handle, winner.DisplayName, lastWeek, Loc.Current.Code);
        return trackCreditTexts[index].IsCurrent(key)
            ? trackCreditTexts[index].Value
            : trackCreditTexts[index].Store(key, Loc.T(lastWeek ? L.Prism.LastWeekWinner : L.Prism.CoverBy,
                AtHandle(winner.Handle, winner.DisplayName)));
    }

    private string ShowcaseLine(int index, GlamDto glam)
    {
        var handle = glam.Author?.Handle ?? string.Empty;
        var name = glam.Author?.DisplayName ?? string.Empty;
        var key = HashCode.Combine(glam.Id, handle, name, Loc.Current.Code);
        return trackCreditTexts[index].IsCurrent(key)
            ? trackCreditTexts[index].Value
            : trackCreditTexts[index].Store(key, Loc.T(L.Prism.TrackGlamBy, AtHandle(handle, name)));
    }
}

using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int MaxTracks = 3;
    private const float TrackNameUnits = PrismType.HeadingUnits;

    private readonly CachedText[] trackTabTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackCountTexts = new CachedText[MaxTracks];
    private CachedText trackTitleText;
    private bool trackJudging;

    private bool HasTracks => store.Tracks.Length > 1;

    private bool VanillaEntry => HasTracks && store.Track == PrismTracks.Vanilla;

    private void StartTrackChoice(bool judging)
    {
        trackJudging = judging;
        Go(PrismScreen.Track);
    }

    private void ChooseTrack(string id)
    {
        store.SelectTrack(id);
        if (trackJudging)
        {
            StartJudging();
        }
        else if (store.Week?.MyEntry is not null)
        {
            Go(PrismScreen.Entry);
        }
        else
        {
            StartPick();
        }
    }

    private float DrawTrackTabs(ImDrawListPtr drawList, Rect content, float top, float scale, out float right)
    {
        var x = content.Min.X + PrismArt.Gutter(scale);
        right = x;
        var tracks = store.Tracks;
        if (tracks.Length < 2)
        {
            return top;
        }

        var labelPixels = PrismArt.LabelPixels();
        var line = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var pad = MathF.Max(0f, (Metrics.Size.TapTarget * scale - line) * 0.5f);
        var count = Math.Min(tracks.Length, MaxTracks);
        for (var index = 0; index < count; index++)
        {
            var track = tracks[index];
            var label = TrackTab(index, track);
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            var selected = track.Id == store.Track;
            var min = new Vector2(x, top - pad);
            var max = new Vector2(x + width, top + line + pad);
            var hovered = !selected && UiInteract.Hover(min, max);
            PrismType.Draw(drawList, new Vector2(x, top), label, selected || hovered ? PrismArt.Fore : PrismArt.Muted,
                PrismFace.Label, labelPixels);
            if (selected)
            {
                PrismArt.Hairline(drawList, x, x + width, top + line + Metrics.Space.Xs * scale, PrismArt.Fore, scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!selected && UiInteract.Click(min, max, hovered))
            {
                store.SelectTrack(track.Id);
            }

            right = x + width;
            x = right + Metrics.Space.Xl * scale;
        }

        return top + line + Metrics.Space.Lg * scale;
    }

    private void DrawTrackChoice(ImDrawListPtr drawList, Rect content, float scale)
    {
        var tracks = store.Tracks;
        if (tracks.Length < 2)
        {
            Go(PrismScreen.Cover, -1);
            return;
        }

        var header = DrawHeader(drawList, content, string.Empty, IssueLine(store.Week?.Issue ?? 0), true, scale);
        var body = new Rect(new Vector2(content.Min.X, header), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var top = DrawSectionTitle(list, left, origin.Y, width, Loc.T(L.Prism.ChooseTrack), scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var namePixels = TrackNameUnits * scale;
        var pad = Metrics.Space.Lg * scale;
        var inner = width - pad * 2f;
        var count = Math.Min(tracks.Length, MaxTracks);
        for (var index = 0; index < count; index++)
        {
            var track = tracks[index];
            var rule = TrackRule(track.Id);
            var ruleHeight = rule.Length > 0
                ? Typography.MeasureWrappedBlock(rule, TextStyles.Subheadline, inner).Y + Metrics.Space.Sm * scale
                : 0f;
            var height = pad * 2f + labelLine + PrismType.LineHeight(PrismFace.Headline, namePixels) + ruleHeight +
                         labelLine;
            var block = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
            var hovered = UiInteract.Hover(block.Min, block.Max);
            var mine = track.MyEntry is not null;
            list.AddRect(block.Min, block.Max, ImGui.GetColorU32(hovered ? PrismArt.Fore : PrismArt.Faint), 0f,
                ImDrawFlags.None, PrismArt.HairlineWidth(scale));
            var cursor = new Vector2(block.Min.X + pad, block.Min.Y + pad);
            PrismType.Draw(list, cursor, Padded[index + 1], PrismArt.Muted, PrismFace.Label, labelPixels);
            cursor.Y += labelLine;
            PrismType.Draw(list, cursor, PrismTracks.Name(track.Id), PrismArt.Fore, PrismFace.Headline, namePixels);
            cursor.Y += PrismType.LineHeight(PrismFace.Headline, namePixels);
            if (rule.Length > 0)
            {
                cursor.Y += Typography.DrawWrappedLeft(cursor, rule, PrismArt.Muted, TextStyles.Subheadline, inner) +
                            Metrics.Space.Sm * scale;
            }

            PrismType.Draw(list, cursor, mine ? Loc.T(L.Prism.YourLook) : TrackCount(index, track.EntryCount),
                mine ? PrismArt.Fore : PrismArt.Muted, PrismFace.Label, labelPixels);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(block.Min, block.Max, hovered))
            {
                ChooseTrack(track.Id);
                return;
            }

            top = block.Max.Y + Metrics.Space.Md * scale;
        }

        top += Metrics.Space.Sm * scale + Typography.DrawWrappedLeft(new Vector2(left, top + Metrics.Space.Sm * scale),
            Loc.T(L.Prism.TrackMovedNote), PrismArt.Muted, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, top - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private static string TrackRule(string id) => id switch
    {
        PrismTracks.Vanilla => Loc.T(L.Prism.TrackVanillaRule),
        PrismTracks.Modded => Loc.T(L.Prism.TrackModdedRule),
        _ => string.Empty,
    };

    private string TrackTab(int index, PrismTrackDto track)
    {
        var key = HashCode.Combine(track.Id, track.EntryCount, Loc.Current.Code);
        return trackTabTexts[index].IsCurrent(key)
            ? trackTabTexts[index].Value
            : trackTabTexts[index].Store(key,
                string.Concat(PrismTracks.Name(track.Id), " ", track.EntryCount.ToString("N0", Loc.Culture)));
    }

    private string TrackCount(int index, int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        return trackCountTexts[index].IsCurrent(key)
            ? trackCountTexts[index].Value
            : trackCountTexts[index].Store(key, Loc.Plural(L.Prism.LooksThisIssue, count));
    }

    private string TrackTitle(string title)
    {
        if (!HasTracks)
        {
            return title;
        }

        var key = HashCode.Combine(title, store.Track, Loc.Current.Code);
        return trackTitleText.IsCurrent(key)
            ? trackTitleText.Value
            : trackTitleText.Store(key, Loc.T(L.Prism.WithTrack, title, PrismTracks.Name(store.Track)));
    }
}

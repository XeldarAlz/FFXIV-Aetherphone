using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Timers;

internal sealed partial class TimersApp
{
    private sealed class GroupText
    {
        public CachedText Seen;
    }

    private readonly Dictionary<string, GroupText> groupTexts = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, GroupText> characterTexts = new();

    private void DrawRetainers(long nowUnix, float width, float scale)
    {
        var bellClicked = SectionHeader(Loc.T(L.Timers.Retainers), width, scale, "timers.bell.ventures",
            configuration.NotifyRetainerVentures, out var headerRect, out var bellRect);
        ReportAnchor("timers.bell", bellRect);
        if (bellClicked)
        {
            configuration.NotifyRetainerVentures = !configuration.NotifyRetainerVentures;
            SaveToggle();
        }

        TimerBoard.OrderCharacters(timers.Characters, timers.CurrentContentId, orderedCharacters);
        if (orderedCharacters.Count == 0)
        {
            var stateRect = DrawState(FontAwesomeIcon.Briefcase, Loc.T(L.Timers.RetainersEmptyTitle),
                Loc.T(L.Timers.RetainersEmptyBody), width, scale);
            ReportAnchor("timers.retainers", new Rect(headerRect.Min, stateRect.Max));
            return;
        }

        for (var index = 0; index < orderedCharacters.Count; index++)
        {
            var cardRect = DrawCharacter(orderedCharacters[index], nowUnix, width, scale);
            if (index == 0)
            {
                ReportAnchor("timers.retainers", new Rect(headerRect.Min, cardRect.Max));
            }
        }

        ImGui.Dummy(new Vector2(0f, (TimersArt.SectionGap - TimersArt.CardGap) * scale));
    }

    private Rect DrawCharacter(TimerCharacterRecord character, long nowUnix, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var retainers = character.Retainers;
        var height = (TimersArt.GroupHeaderHeight + retainers.Count * TimersArt.RowHeight) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        if (!ImGui.IsRectVisible(origin, max))
        {
            textCursor += retainers.Count;
            Advance(origin, width, height, TimersArt.CardGap, scale);
            return new Rect(origin, max);
        }

        var drawList = ImGui.GetWindowDrawList();
        TimersArt.Card(drawList, ui, origin, max, scale);
        var text = CharacterText(character.ContentId);
        var subtitle = TimerLabels.Seen(ref text.Seen, character.RetainersSeenUnix, character.World);
        DrawGroupHeader(drawList, origin, width, character.Name, subtitle, scale);

        var pad = Metrics.Space.Lg * scale;
        var top = origin.Y + TimersArt.GroupHeaderHeight * scale;
        for (var index = 0; index < retainers.Count; index++)
        {
            TimersArt.Hairline(drawList, ui, origin.X + pad, max.X, top);
            var row = new Rect(new Vector2(origin.X + pad, top),
                new Vector2(max.X - pad, top + TimersArt.RowHeight * scale));
            DrawRetainerRow(drawList, row, retainers[index], nowUnix, scale);
            top += TimersArt.RowHeight * scale;
        }

        Advance(origin, width, height, TimersArt.CardGap, scale);
        return new Rect(origin, max);
    }

    private void DrawRetainerRow(ImDrawListPtr drawList, Rect row, TimerRetainerRecord retainer, long nowUnix,
        float scale)
    {
        var text = NextText();
        if (retainer.CompleteUnix <= 0)
        {
            DrawRow(drawList, row, FontAwesomeIcon.Briefcase, ui.MutedInk, 0f, false, retainer.Name, string.Empty,
                Loc.T(L.Timers.NoVenture), ui.MutedInk, scale);
            return;
        }

        var start = retainer.DurationSeconds > 0 ? retainer.CompleteUnix - retainer.DurationSeconds : 0;
        if (retainer.CompleteUnix <= nowUnix)
        {
            DrawRow(drawList, row, FontAwesomeIcon.Briefcase, TimersArt.ReadyInk, 1f, true, retainer.Name,
                text.EndFor(retainer.CompleteUnix), Loc.T(L.Timers.Ready), TimersArt.ReadyInk, scale);
            return;
        }

        DrawRow(drawList, row, FontAwesomeIcon.Briefcase, ui.Accent,
            TimerLedger.Progress(start, retainer.CompleteUnix, nowUnix), start > 0, retainer.Name,
            text.EndFor(retainer.CompleteUnix), text.CountdownFor(retainer.CompleteUnix - nowUnix), ui.TitleInk,
            scale);
    }

    private void DrawVoyages(long nowUnix, float width, float scale)
    {
        if (SectionHeader(Loc.T(L.Timers.Voyages), width, scale, "timers.bell.voyages", configuration.NotifyVoyages,
                out _, out _))
        {
            configuration.NotifyVoyages = !configuration.NotifyVoyages;
            SaveToggle();
        }

        var workshops = timers.Workshops;
        var drawn = 0;
        for (var index = 0; index < workshops.Count; index++)
        {
            if (workshops[index].Vessels.Count == 0)
            {
                continue;
            }

            DrawWorkshop(workshops[index], nowUnix, width, scale);
            drawn++;
        }

        if (drawn == 0)
        {
            DrawState(FontAwesomeIcon.Anchor, Loc.T(L.Timers.VoyagesEmptyTitle), Loc.T(L.Timers.VoyagesEmptyBody),
                width, scale);
            return;
        }

        ImGui.Dummy(new Vector2(0f, (TimersArt.SectionGap - TimersArt.CardGap) * scale));
    }

    private void DrawWorkshop(TimerWorkshopRecord workshop, long nowUnix, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var vessels = workshop.Vessels;
        var height = (TimersArt.GroupHeaderHeight + vessels.Count * TimersArt.RowHeight) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        if (!ImGui.IsRectVisible(origin, max))
        {
            textCursor += vessels.Count;
            Advance(origin, width, height, TimersArt.CardGap, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        TimersArt.Card(drawList, ui, origin, max, scale);
        var text = WorkshopText(workshop.Key);
        var subtitle = TimerLabels.Seen(ref text.Seen, workshop.SeenUnix, workshop.World);
        var title = workshop.Company.Length > 0 ? workshop.Company : Loc.T(L.Timers.Voyages);
        DrawGroupHeader(drawList, origin, width, title, subtitle, scale);

        var pad = Metrics.Space.Lg * scale;
        var top = origin.Y + TimersArt.GroupHeaderHeight * scale;
        for (var index = 0; index < vessels.Count; index++)
        {
            TimersArt.Hairline(drawList, ui, origin.X + pad, max.X, top);
            var row = new Rect(new Vector2(origin.X + pad, top),
                new Vector2(max.X - pad, top + TimersArt.RowHeight * scale));
            DrawVesselRow(drawList, row, vessels[index], nowUnix, scale);
            top += TimersArt.RowHeight * scale;
        }

        Advance(origin, width, height, TimersArt.CardGap, scale);
    }

    private void DrawVesselRow(ImDrawListPtr drawList, Rect row, TimerVesselRecord vessel, long nowUnix, float scale)
    {
        var text = NextText();
        var icon = vessel.Airship ? FontAwesomeIcon.Plane : FontAwesomeIcon.Anchor;
        if (vessel.ReturnUnix <= 0)
        {
            DrawRow(drawList, row, icon, ui.MutedInk, 0f, false, vessel.Name, string.Empty, Loc.T(L.Timers.Docked),
                ui.MutedInk, scale);
            return;
        }

        if (vessel.ReturnUnix <= nowUnix)
        {
            DrawRow(drawList, row, icon, TimersArt.ReadyInk, 1f, true, vessel.Name, text.EndFor(vessel.ReturnUnix),
                Loc.T(L.Timers.Ready), TimersArt.ReadyInk, scale);
            return;
        }

        DrawRow(drawList, row, icon, ui.Accent, TimerLedger.Progress(vessel.RegisterUnix, vessel.ReturnUnix, nowUnix),
            vessel.RegisterUnix > 0, vessel.Name, text.EndFor(vessel.ReturnUnix),
            text.CountdownFor(vessel.ReturnUnix - nowUnix), ui.TitleInk, scale);
    }

    private void DrawGroupHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, string subtitle,
        float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var centerY = origin.Y + TimersArt.GroupHeaderHeight * 0.5f * scale;
        TimersArt.Labels(drawList, origin.X + pad, origin.X + width - pad, centerY, title, subtitle, ui.TitleInk,
            ui.MutedInk, scale);
    }

    private void DrawRow(ImDrawListPtr drawList, Rect row, FontAwesomeIcon icon, Vector4 tint, float fraction,
        bool ring, string title, string subtitle, string value, Vector4 valueInk, float scale)
    {
        var dialCenter = new Vector2(TimersArt.DialCenterX(row.Min.X, scale), row.Center.Y);
        TimersArt.Dial(drawList, dialCenter, icon, tint, fraction, ring, scale);
        var valueWidth = TimersArt.Value(drawList, row.Max.X, row.Center.Y, value, valueInk);
        var textLeft = row.Min.X + TimersArt.DialSpan(scale);
        var textRight = valueWidth > 0f ? TimersArt.LabelRight(row.Max.X - valueWidth, scale) : row.Max.X;
        TimersArt.Labels(drawList, textLeft, textRight, row.Center.Y, title, subtitle, ui.TitleInk, ui.MutedInk,
            scale);
    }

    private Rect DrawState(FontAwesomeIcon icon, string title, string body, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = TimersArt.StateHeight(title, body, width, scale);
        var rect = new Rect(origin, origin + new Vector2(width, height));
        if (ImGui.IsRectVisible(rect.Min, rect.Max))
        {
            TimersArt.State(ImGui.GetWindowDrawList(), ui, origin, width, height, icon, title, body, scale);
        }

        Advance(origin, width, height, TimersArt.SectionGap, scale);
        return rect;
    }

    private GroupText CharacterText(ulong contentId)
    {
        if (!characterTexts.TryGetValue(contentId, out var text))
        {
            text = new GroupText();
            characterTexts[contentId] = text;
        }

        return text;
    }

    private GroupText WorkshopText(string key)
    {
        if (!groupTexts.TryGetValue(key, out var text))
        {
            text = new GroupText();
            groupTexts[key] = text;
        }

        return text;
    }
}

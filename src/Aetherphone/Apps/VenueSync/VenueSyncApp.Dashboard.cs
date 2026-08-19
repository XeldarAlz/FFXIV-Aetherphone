using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private const float StatusCardHeight = 90f;
    private const float ActionRowHeight = 64f;

    private void DrawDashboard(Rect area)
    {
        var scale = UiScale.Current;
        var gearReserve = 44f * scale;
        AppHeader.DrawTitleWithReserve(area, "venuesync.dashboard.title", DisplayName, gearReserve, ui.TitleInk,
            scale);
        var gearCenter = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (AppSkin.IconButton(gearCenter, 14f * scale, FontAwesomeIcon.Cog.ToIconString(), theme.TextMuted,
                AppSkin.Transparent, 0.9f, theme))
        {
            router.Push(VenueSyncRoute.Settings);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawStatusCard(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawActionsCard(scale);
        }
    }

    private void DrawStatusCard(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = StatusCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Lg * scale, elevated: true);
        Material.TopGlow(drawList, card.Min, card.Max, Metrics.Radius.Lg * scale, ui.Accent, 0.55f, 0.10f);

        var pad = Metrics.Space.Lg * scale;
        var activeShift = FindActiveShift();
        if (activeShift is not null)
        {
            var venueLabel = string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueName)
                ? Loc.T(L.VenueSync.NoVenueSelected)
                : configuration.VenueSyncSelectedVenueName;
            var buttonWidth = 100f * scale;
            var iconSize = 42f * scale;
            var iconCenter = new Vector2(card.Min.X + pad + iconSize * 0.5f, card.Center.Y);
            IconTile.Draw(iconCenter, iconSize, IconTile.Surface(theme.ToggleOn), FontAwesomeIcon.Clock);

            var textLeft = iconCenter.X + iconSize * 0.5f + Metrics.Space.Md * scale;
            var textTop = card.Center.Y - 20f * scale;
            var textMaxWidth = MathF.Max(1f, card.Max.X - pad - buttonWidth - Metrics.Space.Md * scale - textLeft);

            Marquee.DrawLeftAuto("venuesync.dashboard.venue", venueLabel, textLeft, textTop, textMaxWidth,
                TextStyles.Headline, theme.TextStrong);
            DrawStatusPill(drawList, new Vector2(textLeft, textTop + 22f * scale), Loc.T(L.VenueSync.OnShift),
                ui.Accent, scale);

            var buttonHeight = 32f * scale;
            var button = new Rect(new Vector2(card.Max.X - pad - buttonWidth, card.Center.Y - buttonHeight * 0.5f),
                new Vector2(card.Max.X - pad, card.Center.Y + buttonHeight * 0.5f));

            if (AppSkin.DangerPillButton(button, Loc.T(L.VenueSync.ClockOut), theme))
            {
                var shiftId = activeShift.Id;
                _ = ClockOutFireAndForget(shiftId);
            }
        }
        else
        {
            var iconSize = 42f * scale;
            var iconCenter = new Vector2(card.Min.X + pad + iconSize * 0.5f, card.Center.Y);
            IconTile.Draw(iconCenter, iconSize, IconTile.Surface(theme.TextMuted), FontAwesomeIcon.Clock);
            var textLeft = iconCenter.X + iconSize * 0.5f + Metrics.Space.Md * scale;
            var textMaxWidth = MathF.Max(1f, card.Max.X - pad - textLeft);
            Marquee.DrawLeftAuto("venuesync.dashboard.offshift", Loc.T(L.VenueSync.OffShift), textLeft,
                card.Center.Y - 8f * scale, textMaxWidth, TextStyles.Headline, theme.TextMuted);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private async Task ClockOutFireAndForget(string shiftId)
    {
        var result = await client.ClockOutAsync(shiftId, CancellationToken.None).ConfigureAwait(false);
        if (result is { Success: true })
        {
            state.EnsureShiftsFresh(true);
        }
    }

    private VenueSyncShift? FindActiveShift()
    {
        var shifts = state.Shifts?.Shifts;
        if (shifts is null)
        {
            return null;
        }

        foreach (var shift in shifts)
        {
            if (shift.Status == "ACTIVE")
            {
                return shift;
            }
        }

        return null;
    }

    private void DrawActionsCard(float scale)
    {
        var openShiftsCount = state.Shifts?.OpenShifts.Count ?? 0;
        var card = GroupCard.Begin(theme, 2, ActionRowHeight, showSeparators: false, glowAccent: ui.Accent,
            fillOverride: ui.Palette.CardFill);

        var logSaleRow = card.NextRow();
        if (DrawActionRow(card, logSaleRow, FontAwesomeIcon.Coins, ui.Accent, Loc.T(L.VenueSync.LogASale),
                Loc.T(L.VenueSync.RecordATransaction), null, "venuesync.dashboard.logsale"))
        {
            router.Push(VenueSyncRoute.Sales);
        }

        var upcomingSubtitle = openShiftsCount == 0
            ? Loc.T(L.VenueSync.NoneOpenRightNow)
            : Loc.T(L.VenueSync.TapToViewAndClaim);
        var upcomingRow = card.NextRow();
        if (DrawActionRow(card, upcomingRow, FontAwesomeIcon.Clock, ui.Accent, Loc.T(L.VenueSync.UpcomingShifts),
                upcomingSubtitle, openShiftsCount.ToString(), "venuesync.dashboard.upcoming"))
        {
            router.Push(VenueSyncRoute.Shifts);
        }

        card.End();

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var sessionRow = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionRowHeight * scale));
        var count = state.SessionSalesCount;
        var sessionValue = count == 0
            ? Loc.T(L.VenueSync.NoSalesYet)
            : $"{Loc.Plural(L.VenueSync.SaleCount, count)} · {state.SessionSalesTotal:N0}g";
        DrawActionRow(default, sessionRow, FontAwesomeIcon.Coins, theme.TextMuted, Loc.T(L.VenueSync.ThisSession),
            sessionValue, null, "venuesync.dashboard.session", clickable: false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionRowHeight * scale));
    }

    private bool DrawActionRow(GroupCard card, Rect row, FontAwesomeIcon icon, Vector4 tint, string title,
        string subtitle, string? trailingCount, string idSuffix, bool clickable = true)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = clickable && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.14f : 0.07f;
            card.DrawHoverHighlight(new Vector4(1f, 1f, 1f, alpha));
        }

        var iconSize = 42f * scale;
        var iconCenter = new Vector2(row.Min.X + iconSize * 0.5f, row.Center.Y);
        IconTile.Draw(iconCenter, iconSize, IconTile.Surface(tint), icon);

        var textLeft = row.Min.X + iconSize + 14f * scale;
        var trailingWidth = trailingCount is null
            ? 0f
            : Typography.Measure(trailingCount, TextStyles.Caption2).X + 16f * scale + 10f * scale;
        var textRight = row.Max.X - trailingWidth;
        var textWidth = MathF.Max(1f, textRight - textLeft);

        Marquee.DrawLeftAuto($"actionrow-title-{idSuffix}", title, textLeft, row.Center.Y - 16f * scale, textWidth,
            TextStyles.Headline, theme.TextStrong);
        Marquee.DrawLeftAuto($"actionrow-subtitle-{idSuffix}", subtitle, textLeft, row.Center.Y + 4f * scale,
            textWidth, TextStyles.Footnote, theme.TextMuted);

        if (trailingCount is not null)
        {
            DrawCountBadge(drawList, new Vector2(row.Max.X, row.Center.Y), trailingCount, tint, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return clickable && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawStatusPill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 tint,
        float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var padX = 8f * scale;
        var height = textSize.Y + 6f * scale;
        var width = textSize.X + padX * 2f;
        var max = new Vector2(topLeft.X + width, topLeft.Y + height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.15f)));
        Squircle.Stroke(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.34f)),
            1f * scale);
        Typography.Draw(drawList, new Vector2(topLeft.X + padX, topLeft.Y + (height - textSize.Y) * 0.5f), label,
            tint, TextStyles.Caption1);
    }

    private static void DrawCountBadge(ImDrawListPtr drawList, Vector2 rightCenter, string text, Vector4 accent,
        float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var padX = 8f * scale;
        var padY = 4f * scale;
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        var min = new Vector2(max.X - width, rightCenter.Y - height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.2f)));
        Typography.Draw(drawList, new Vector2(min.X + padX, rightCenter.Y - textSize.Y * 0.5f), text, accent,
            TextStyles.Caption2);
    }
}

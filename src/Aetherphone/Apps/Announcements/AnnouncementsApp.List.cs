using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp
{
    private const float CardRounding = 18f;
    private const float CardPadding = 15f;
    private const float CardGap = 10f;
    private const float DividerGap = 12f;
    private const float FooterGap = 10f;
    private const int MaxTitleLines = 2;
    private const int MaxLeadLines = 6;
    private const int MaxPreviewLines = 2;

    private readonly PullToRefresh listRefresh = new();

    private void DrawList(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, DisplayName, navigation.Back);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        UiAnchors.Report("announcements.feed", body);

        var announcements = store.Announcements;
        using (var surface = AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            listRefresh.Draw(body, surface.Pull, surface.Dragging, store.Loading, ui.MutedInk, store.Refresh);
            if (announcements.Length == 0)
            {
                DrawEmptyState(body, scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
            var previousUnix = 0L;
            for (var index = 0; index < announcements.Length; index++)
            {
                var announcement = announcements[index];
                if (index == 0 || !TimeText.SameLocalDay(previousUnix, announcement.CreatedAtUnix))
                {
                    ui.SectionHeading(TimeText.DayLabel(announcement.CreatedAtUnix), index == 0 ? 0f : Metrics.Space.Xs);
                }

                previousUnix = announcement.CreatedAtUnix;
                DrawCard(announcement, scale, index == 0 ? MaxLeadLines : MaxPreviewLines, index == 0);
                ImGui.Dummy(new Vector2(0f, CardGap * scale));
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawEmptyState(Rect body, float scale)
    {
        if (store.Loading && !store.LoadedOnce)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 120f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.EmptyTitle),
            Loc.T(L.Announcements.EmptyHint));
    }

    private void DrawCard(AnnouncementDto announcement, float scale, int maxBodyLines, bool isLead)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CardPadding * scale;
        var unread = store.IsUnread(announcement);
        var text = AnnouncementText.For(announcement);
        var innerWidth = width - pad * 2f;

        var titleLines = ClampLines(text.Title, TextStyles.Headline, innerWidth, MaxTitleLines);
        var bodyLines = ClampLines(text.Body, TextStyles.Subheadline, innerWidth, maxBodyLines);
        var titleLineHeight = LineHeight(TextStyles.Headline);
        var bodyLineHeight = LineHeight(TextStyles.Subheadline);
        var stamp = TimeText.Clock(announcement.CreatedAtUnix);
        var stampSize = Typography.Measure(stamp, TextStyles.Footnote);
        var footerHeight = MathF.Max(stampSize.Y, PillHeight(scale));
        var bodyBlock = bodyLines.Length > 0 ? Metrics.Space.Xxs * scale + bodyLines.Length * bodyLineHeight : 0f;
        var cardHeight = pad + titleLines.Length * titleLineHeight + bodyBlock + DividerGap * scale + 1f
            + FooterGap * scale + footerHeight + pad;

        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        var rounding = CardRounding * scale;
        ui.Card(drawList, origin, cardMax, rounding, true);
        if (unread)
        {
            Squircle.Fill(drawList, origin, cardMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
            Squircle.Stroke(drawList, origin, cardMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.42f)), Metrics.Stroke.Thin * scale);
        }

        if (isLead)
        {
            UiAnchors.Report("announcements.card", new Rect(origin, cardMax));
        }

        var textLeft = origin.X + pad;
        var cursorY = origin.Y + pad;
        for (var index = 0; index < titleLines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[index], ui.TitleInk,
                TextStyles.Headline);
            cursorY += titleLineHeight;
        }

        if (bodyLines.Length > 0)
        {
            cursorY += Metrics.Space.Xxs * scale;
            for (var index = 0; index < bodyLines.Length; index++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), bodyLines[index], ui.BodyInk,
                    TextStyles.Subheadline);
                cursorY += bodyLineHeight;
            }
        }

        cursorY += DividerGap * scale;
        drawList.AddLine(new Vector2(textLeft, cursorY), new Vector2(cardMax.X - pad, cursorY),
            ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.22f)), MathF.Max(1f, scale));
        cursorY += FooterGap * scale;

        var footerCenterY = cursorY + footerHeight * 0.5f;
        var stampLeft = textLeft;
        if (unread)
        {
            stampLeft += DrawNewPill(drawList, new Vector2(textLeft, footerCenterY), scale) + Metrics.Space.Sm * scale;
        }

        Typography.Draw(drawList, new Vector2(stampLeft, footerCenterY - stampSize.Y * 0.5f), stamp, ui.MutedInk,
            TextStyles.Footnote);

        var hovered = ImGui.IsMouseHoveringRect(origin, cardMax);
        DrawChevron(drawList, new Vector2(cardMax.X - pad, footerCenterY), 5f * scale, Metrics.Stroke.Thin * scale,
            hovered ? ui.TitleInk : Palette.WithAlpha(ui.MutedInk, 0.7f));
        if (hovered)
        {
            Squircle.Fill(drawList, origin, cardMax, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(origin, cardMax, hovered))
        {
            router.Push(AnnouncementsRoute.Detail(announcement.Id));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private float DrawNewPill(ImDrawListPtr drawList, Vector2 leftCenter, float scale)
    {
        var label = Loc.T(L.Announcements.NewBadge);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var height = PillHeight(scale);
        var width = labelSize.X + Metrics.Space.Lg * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.Accent));
        Typography.Draw(drawList, new Vector2(min.X + (width - labelSize.X) * 0.5f, leftCenter.Y - labelSize.Y * 0.5f),
            label, OnAccentInk, TextStyles.FootnoteEmphasized);
        return width;
    }

    private static float PillHeight(float scale) =>
        Typography.Measure("Ag", TextStyles.FootnoteEmphasized).Y + Metrics.Space.Xs * scale;
}
